using System.Text.Json.Nodes;
using Mariliz.Application.Engineering;
using Mariliz.Application.Execution;
using Mariliz.Application.Models;
using Mariliz.Application.Ports;
using Mariliz.Application.Protocol;
using Mariliz.Application.Workflows;
using Mariliz.Application.Workspace;

namespace Mariliz.Application.Context;

// Mostra o pedido de engenharia como ele iria ao modelo: mesmas fontes, mesma busca e mesmo texto, contados antes de gastar.
public sealed class ContextReviewService(
    WorkspaceAuthorizer workspace,
    IWorkflowContext context,
    IContextPreviewRuntime runtime,
    ITokenizers tokenizers,
    ModelGateway? models = null,
    ProjectKnowledgeService? knowledge = null)
{
    public const int MaxGoal = 4000;
    private const string PreviewSchema = "ai.context.preview.v1";
    private const string PreviewAck = "ai.context.preview.ack.v1";

    // O formato de conversa soma tokens por mensagem e para abrir a resposta, como a OpenAI documenta.
    private const int PerMessage = 4;
    private const int PerReply = 3;

    private static readonly Dictionary<string, string> CallLabels = new(StringComparer.Ordinal)
    {
        ["planning"] = "Planejamento",
        ["architecture"] = "Arquitetura"
    };

    public async Task<ContextReview> ReviewAsync(string projectId, string goal, CancellationToken ct)
    {
        if (!workspace.IsAuthorized(projectId)) throw EngineErrors.ProjectNotAuthorized();
        var text = goal.Trim();
        if (text.Length is 0 or > MaxGoal) throw new ParamException($"O pedido precisa ter de 1 a {MaxGoal} caracteres.");

        var built = await context.BuildAsync(projectId, ct);
        if (built["knowledgeRoot"] is null)
            throw new EngineException("CONTEXT_UNAVAILABLE", "O índice de conhecimento não está disponível nesta sessão do motor.");

        var reply = await runtime.PreviewContextAsync(Request(projectId, text, built), ct);
        if (reply.Text("schema") != PreviewAck || reply["calls"] is not JsonArray calls || reply["evidence"] is not JsonArray evidence)
            throw EngineErrors.AiRuntimeInvalidResponse();

        var model = Model();
        var counter = tokenizers.For(model.Provider, model.Model);
        var pieces = evidence.OfType<JsonObject>().Select(item => Evidence(item, counter)).ToList();
        var evidenceTokens = pieces.Where(piece => piece.Included).Sum(piece => piece.Tokens);
        var budget = models?.Budget(ModelPurposes.Workflow) ?? ModelBudgets.Default(ModelPurposes.Workflow);
        var reviewed = Calls(calls, counter, evidenceTokens, budget);
        var exclusions = knowledge is null ? [] : (await knowledge.InspectAsync(projectId, ct)).Excluded;

        return new ContextReview(
            projectId,
            model,
            new ContextCounting(counter.Exact, counter.Method),
            budget,
            reviewed.Sum(call => call.InputTokens + call.OutputTokens),
            reviewed,
            pieces,
            new ContextLeftOut(pieces.Count(piece => !piece.Included), pieces.Count(piece => piece.Truncated), Pending(reply), exclusions));
    }

    private static JsonObject Request(string projectId, string goal, JsonObject built) => new()
    {
        ["schema"] = PreviewSchema,
        ["projectId"] = projectId,
        ["goal"] = goal,
        ["context"] = new JsonObject
        {
            ["stacks"] = built["stacks"]?.DeepClone() ?? new JsonArray(),
            ["knowledgeRoot"] = built["knowledgeRoot"]!.DeepClone(),
            ["sources"] = built["sources"]?.DeepClone() ?? new JsonArray()
        }
    };

    private ContextModel Model()
    {
        var connection = models?.Connection();
        var purpose = connection?.Purposes.FirstOrDefault(item => item.Id == ModelPurposes.Workflow);
        return connection is { Configured: true } && purpose is not null
            ? new ContextModel(true, purpose.Provider, purpose.Model)
            : new ContextModel(false, null, null);
    }

    private static ContextEvidence Evidence(JsonObject item, TokenCounter counter) => new(
        item.Text("filePath"),
        counter.Count(item.Text("text")),
        item["included"]?.GetValue<bool>() is true,
        item["truncated"]?.GetValue<bool>() is true);

    // Cada chamada reserva a resposta máxima: é o pior caso que o orçamento precisa comportar.
    private static List<ContextCall> Calls(JsonArray calls, TokenCounter counter, int evidenceTokens, int budget)
    {
        var result = new List<ContextCall>();
        var spent = 0;
        foreach (var call in calls.OfType<JsonObject>())
        {
            var messages = (call["messages"] as JsonArray ?? []).OfType<JsonObject>().ToList();
            var system = messages.Where(m => m.Text("role") == "system").Sum(m => counter.Count(m.Text("content")) + PerMessage);
            var user = messages.Where(m => m.Text("role") != "system").Sum(m => counter.Count(m.Text("content")) + PerMessage);
            var block = Math.Min(evidenceTokens, user);
            var input = system + user + PerReply;
            var output = call["maxOutputTokens"] is JsonValue value && value.TryGetValue<int>(out var max) ? Math.Max(0, max) : 0;
            var within = spent + input + output <= budget;
            if (within) spent += input + output;

            var agentId = call.Text("agentId");
            result.Add(new ContextCall(agentId, CallLabels.GetValueOrDefault(agentId, agentId), input, output, within,
            [
                new ContextSection("instructions", "Instruções do agente", system),
                new ContextSection("request", "Pedido e stacks", user - block + PerReply),
                new ContextSection("evidence", "Trechos do projeto", block)
            ]));
        }
        return result;
    }

    private static int Pending(JsonObject reply) =>
        reply["index"] is JsonObject index && index["pending"] is JsonValue value && value.TryGetValue<int>(out var pending) ? pending : 0;
}
