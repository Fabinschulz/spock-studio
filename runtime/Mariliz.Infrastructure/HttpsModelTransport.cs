using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Mariliz.Application.Execution;
using Mariliz.Application.Models;

namespace Mariliz.Infrastructure;

/// <summary>
/// Chama só os endereços da lista de provedores. A chave vai no cabeçalho e não entra na mensagem de erro. Cada provedor fala o
/// seu formato (OpenAI: chat completions; Anthropic: messages), e o formato é escolhido pelo endereço, nunca pelo chamador.
/// </summary>
public sealed partial class HttpsModelTransport : IModelTransport, IModelChatTransport, IDisposable
{
    /// <summary>A versão da API da Anthropic que este transporte sabe falar.</summary>
    public const string AnthropicVersion = "2023-06-01";

    private const string JsonInstruction = "Responda somente com um objeto JSON válido, sem nenhum texto antes ou depois.";

    private readonly HttpClient _http;

    public HttpsModelTransport(HttpMessageHandler? handler = null)
    {
        _http = handler is null ? new HttpClient() : new HttpClient(handler, disposeHandler: false);
        _http.Timeout = TimeSpan.FromSeconds(15);
    }

    public async Task<ModelTransportResult> SendAsync(Uri endpoint, string key, string prompt, int maxTokens, CancellationToken ct)
    {
        var provider = Allowed(endpoint);
        var reply = await ExchangeAsync(provider, key, provider.DefaultModel, [new ChatMessage("user", prompt)], maxTokens, jsonObject: false, ct);
        return new ModelTransportResult(reply.Text, reply.TotalTokens);
    }

    public async Task<ChatTransportResult> SendChatAsync(Uri endpoint, string key, string model, ChatRequest chat, CancellationToken ct)
    {
        var provider = Allowed(endpoint);
        var reply = await ExchangeAsync(provider, key, model, chat.Messages, chat.MaxTokens, chat.JsonObject, ct);
        return new ChatTransportResult(reply.Text, reply.Version.Length > 0 ? reply.Version : model, reply.InputTokens, reply.OutputTokens);
    }

    private sealed record Reply(string Text, string Version, int InputTokens, int OutputTokens, int TotalTokens);

    private async Task<Reply> ExchangeAsync(
        ModelProvider provider, string key, string model, IReadOnlyList<ChatMessage> messages, int maxTokens, bool jsonObject, CancellationToken ct)
    {
        var anthropic = provider.Id == ModelProviders.Anthropic.Id;
        using var request = new HttpRequestMessage(HttpMethod.Post, provider.Endpoint);
        if (anthropic)
        {
            request.Headers.Add("x-api-key", key);
            request.Headers.Add("anthropic-version", AnthropicVersion);
        }
        else
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        }

        var body = anthropic ? AnthropicBody(model, messages, maxTokens, jsonObject) : OpenAiBody(model, messages, maxTokens, jsonObject);
        request.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");

        try
        {
            using var response = await _http.SendAsync(request, ct);
            var text = await response.Content.ReadAsStringAsync(ct);
            ThrowIfRefused(response.StatusCode);
            if (!response.IsSuccessStatusCode || JsonNode.Parse(text) is not JsonObject document)
                throw new EngineException("MODEL_INVALID_RESPONSE", "O modelo respondeu fora do contrato.");

            var reply = anthropic ? ReadAnthropic(document) : ReadOpenAi(document);
            return jsonObject ? reply with { Text = StripFence(reply.Text) } : reply;
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or InvalidOperationException or FormatException)
        {
            throw new EngineException("MODEL_UNAVAILABLE", "O modelo não pôde ser contactado.");
        }
    }

    private static JsonObject OpenAiBody(string model, IReadOnlyList<ChatMessage> messages, int maxTokens, bool jsonObject)
    {
        var body = new JsonObject
        {
            ["model"] = model,
            ["max_tokens"] = maxTokens,
            ["messages"] = new JsonArray(messages.Select(m => (JsonNode)new JsonObject { ["role"] = m.Role, ["content"] = m.Content }).ToArray())
        };
        if (jsonObject) body["response_format"] = new JsonObject { ["type"] = "json_object" };
        return body;
    }

    /// <summary>
    /// A Anthropic leva as instruções de sistema à parte e só conhece as funções "user" e "assistant" nas mensagens. Ela não tem um
    /// modo de resposta em JSON: o pedido vai na instrução, e uma cerca de código em volta da resposta é tirada na volta.
    /// </summary>
    private static JsonObject AnthropicBody(string model, IReadOnlyList<ChatMessage> messages, int maxTokens, bool jsonObject)
    {
        var system = messages.Where(m => m.Role == "system").Select(m => m.Content).ToList();
        if (jsonObject) system.Add(JsonInstruction);

        var conversation = messages.Where(m => m.Role != "system").ToList();
        var onlyInstructions = conversation.Count == 0;
        // Uma conversa só de instruções ainda precisa de uma mensagem do usuário: a instrução vira o pedido.
        if (onlyInstructions) conversation.Add(new ChatMessage("user", system.Count > 0 ? string.Join("\n\n", system) : "Continue."));

        var body = new JsonObject
        {
            ["model"] = model,
            ["max_tokens"] = maxTokens,
            ["messages"] = new JsonArray(conversation.Select(m => (JsonNode)new JsonObject
            {
                ["role"] = m.Role == "assistant" ? "assistant" : "user",
                ["content"] = m.Content
            }).ToArray())
        };
        if (system.Count > 0 && !onlyInstructions) body["system"] = string.Join("\n\n", system);
        return body;
    }

    private static Reply ReadOpenAi(JsonObject document)
    {
        var content = document["choices"]?[0]?["message"]?["content"]?.GetValue<string>() ?? "";
        var version = document["model"]?.GetValue<string>() ?? "";
        var input = document["usage"]?["prompt_tokens"]?.GetValue<int>() ?? 0;
        var output = document["usage"]?["completion_tokens"]?.GetValue<int>() ?? 0;
        var total = document["usage"]?["total_tokens"]?.GetValue<int>() ?? input + output;
        return new Reply(content, version, input, output, total);
    }

    private static Reply ReadAnthropic(JsonObject document)
    {
        var content = document["content"] is JsonArray blocks
            ? string.Concat(blocks.OfType<JsonObject>().Where(b => b["type"]?.GetValue<string>() == "text").Select(b => b["text"]?.GetValue<string>() ?? ""))
            : "";
        var version = document["model"]?.GetValue<string>() ?? "";
        var input = document["usage"]?["input_tokens"]?.GetValue<int>() ?? 0;
        var output = document["usage"]?["output_tokens"]?.GetValue<int>() ?? 0;
        return new Reply(content, version, input, output, input + output);
    }

    /// <summary>Tira a cerca de código (<c>```json ... ```</c>) que um modelo às vezes põe em volta de um JSON.</summary>
    private static string StripFence(string text)
    {
        var match = Fence().Match(text.Trim());
        return match.Success ? match.Groups["body"].Value.Trim() : text;
    }

    [GeneratedRegex(@"^```[a-zA-Z]*\s*(?<body>.*?)\s*```$", RegexOptions.Singleline)]
    private static partial Regex Fence();

    /// <summary>
    /// O que a pessoa precisa saber de uma recusa do provedor: a chave não vale, ou vale mas o uso está limitado agora. O corpo da
    /// resposta não é repetido: ele pode citar a chave.
    /// </summary>
    private static void ThrowIfRefused(HttpStatusCode status)
    {
        if (status is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            throw new EngineException("MODEL_AUTH_FAILED", "O provedor recusou a chave.");
        if (status == HttpStatusCode.TooManyRequests)
            throw new EngineException("MODEL_RATE_LIMITED", "O provedor limitou o uso desta chave agora. Tente de novo em instantes.");
    }

    /// <summary>A lista de provedores é a única fonte de destinos permitidos; qualquer outro endereço é recusado antes de a chave sair.</summary>
    private static ModelProvider Allowed(Uri endpoint) =>
        ModelProviders.ByEndpoint(endpoint) ?? throw new EngineException("MODEL_ENDPOINT_DENIED", "O destino do modelo não é permitido.");

    public void Dispose() => _http.Dispose();
}
