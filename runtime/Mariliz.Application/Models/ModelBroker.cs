using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Mariliz.Application.Execution;

namespace Mariliz.Application.Models;

/// <summary>Resposta do proxy: o código HTTP e o corpo (<c>ai.model.response.v1</c> ou um erro simples).</summary>
public sealed record BrokerReply(int HttpStatus, JsonObject Body);

/// <summary>O que o runtime de IA recebe para falar com o proxy desta execução. O token não é a chave do provedor.</summary>
public sealed record ModelGrant(string Url, string Token, int TokenBudget);

/// <summary>Tokens gastos por uma execução, como o proxy contou (não como o runtime informou).</summary>
public sealed record ModelUsage(int Calls, int InputTokens, int OutputTokens, double CostUsd)
{
    public static readonly ModelUsage None = new(0, 0, 0, 0);
}

/// <summary>Como o motor abre e fecha o acesso ao modelo de uma execução.</summary>
public interface IModelAccess
{
    bool Configured { get; }

    /// <summary>Abre o acesso de uma execução, com um teto de tokens. O endereço só existe em 127.0.0.1.</summary>
    ModelGrant Open(string executionId, int tokenBudget);

    /// <summary>Fecha o acesso e devolve o que foi gasto. Fechar duas vezes devolve o mesmo total.</summary>
    ModelUsage Close(string executionId);
}

/// <summary>
/// Decide cada chamada de modelo de uma execução. Um token por execução, o modelo e a chave escolhidos aqui, o orçamento
/// de tokens cobrado antes e depois de cada chamada e o uso registrado. Sem custo em dinheiro: o teto da D4 é de tokens.
/// </summary>
public sealed class ModelBroker(ModelGateway gateway)
{
    public const string Schema = "ai.model.request.v1";
    private static readonly HashSet<string> Purposes = new(StringComparer.Ordinal) { "classify", "plan", "architecture", "judge", "rewrite-query" };
    private static readonly HashSet<string> Roles = new(StringComparer.Ordinal) { "system", "user", "assistant" };

    private readonly object _gate = new();
    private readonly Dictionary<string, Grant> _byToken = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Grant> _byExecution = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ModelUsage> _closed = new(StringComparer.Ordinal);

    public bool Configured => gateway.Status().Configured;

    /// <summary>Abre o acesso de uma execução e devolve o token que só vale para ela.</summary>
    public string Open(string executionId, int tokenBudget)
    {
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(33)).Replace('+', '-').Replace('/', '_');
        var grant = new Grant(executionId, Math.Max(0, tokenBudget), token);
        lock (_gate)
        {
            _byToken[token] = grant;
            _byExecution[executionId] = grant;
            _closed.Remove(executionId);
        }
        return token;
    }

    public ModelUsage Close(string executionId)
    {
        lock (_gate)
        {
            if (!_byExecution.Remove(executionId, out var grant))
                return _closed.GetValueOrDefault(executionId, ModelUsage.None);
            _byToken.Remove(grant.Token);
            return _closed[executionId] = grant.Usage();
        }
    }

    public async Task<BrokerReply> HandleAsync(string? bearer, JsonObject? request, CancellationToken ct)
    {
        var grant = Find(bearer);
        if (grant is null) return new BrokerReply(401, new JsonObject { ["error"] = "unauthorized" });
        if (!TryParse(request, out var executionId, out var chat)) return new BrokerReply(400, new JsonObject { ["error"] = "invalid_body" });
        if (executionId != grant.ExecutionId) return Error("MODEL_FORBIDDEN");

        if (!grant.TryReserve(chat.MaxTokens)) return Error("MODEL_BUDGET_EXCEEDED");
        ChatCompletion completion;
        try
        {
            completion = await gateway.ChatAsync(chat, ct);
        }
        catch (EngineException ex)
        {
            grant.Release(chat.MaxTokens);
            return Error(ex.Code is "MODEL_NOT_CONFIGURED" or "MODEL_TIMEOUT" ? ex.Code : "MODEL_UNAVAILABLE");
        }
        catch (OperationCanceledException)
        {
            grant.Release(chat.MaxTokens);
            throw;
        }

        grant.Settle(chat.MaxTokens, completion.InputTokens, completion.OutputTokens);
        return new BrokerReply(200, new JsonObject
        {
            ["schema"] = "ai.model.response.v1",
            ["status"] = "ok",
            ["text"] = completion.Text,
            ["model"] = completion.Model,
            ["modelVersion"] = completion.ModelVersion,
            ["inputTokens"] = completion.InputTokens,
            ["outputTokens"] = completion.OutputTokens,
            ["costUsd"] = 0.0
        });
    }

    private Grant? Find(string? bearer)
    {
        if (string.IsNullOrEmpty(bearer)) return null;
        var presented = Encoding.UTF8.GetBytes(bearer);
        lock (_gate)
        {
            foreach (var (token, grant) in _byToken)
                if (CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(token), presented))
                    return grant;
        }
        return null;
    }

    private static BrokerReply Error(string code) => new(200, new JsonObject
    {
        ["schema"] = "ai.model.response.v1",
        ["status"] = "error",
        ["error"] = new JsonObject { ["code"] = code }
    });

    private static bool TryParse(JsonObject? body, out string executionId, out ChatRequest chat)
    {
        executionId = "";
        chat = new ChatRequest([], 0, false);
        if (body is null || Text(body, "schema") != Schema) return false;
        executionId = Text(body, "executionId");
        if (executionId.Length == 0 || !Purposes.Contains(Text(body, "purpose"))) return false;
        if (body["maxTokens"] is not JsonValue t || !t.TryGetValue<int>(out var maxTokens) || maxTokens is < 1 or > ModelGateway.MaxChatTokens) return false;
        if (body["messages"] is not JsonArray array || array.Count is < 1 or > 16) return false;

        var messages = new List<ChatMessage>();
        foreach (var node in array)
        {
            if (node is not JsonObject item || !Roles.Contains(Text(item, "role"))) return false;
            var content = Text(item, "content");
            if (content.Length is 0 or > 32000) return false;
            messages.Add(new ChatMessage(Text(item, "role"), content));
        }

        chat = new ChatRequest(messages, maxTokens, body["responseSchema"] is not null);
        return true;
    }

    private static string Text(JsonObject node, string name) =>
        node[name] is JsonValue value && value.TryGetValue<string>(out var text) ? text : "";

    /// <summary>O orçamento de uma execução. Reserva o pior caso antes da chamada e acerta com o uso real depois.</summary>
    private sealed class Grant(string executionId, int budget, string token)
    {
        private readonly object _gate = new();
        private int _reserved;
        private int _input;
        private int _output;
        private int _calls;

        public string ExecutionId { get; } = executionId;
        public string Token { get; } = token;

        public bool TryReserve(int maxTokens)
        {
            lock (_gate)
            {
                if (_input + _output + _reserved + maxTokens > budget) return false;
                _reserved += maxTokens;
                return true;
            }
        }

        public void Release(int maxTokens)
        {
            lock (_gate) _reserved -= maxTokens;
        }

        public void Settle(int maxTokens, int input, int output)
        {
            lock (_gate)
            {
                _reserved -= maxTokens;
                _input += input;
                _output += output;
                _calls++;
            }
        }

        public ModelUsage Usage()
        {
            lock (_gate) return new ModelUsage(_calls, _input, _output, 0);
        }
    }
}
