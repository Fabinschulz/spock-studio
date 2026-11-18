using System.Text.Json.Nodes;
using Mariliz.Application.Engineering;
using Mariliz.Application.Execution;
using Mariliz.Application.Integrations;

namespace Mariliz.Application.Protocol;

internal sealed class EngineeringRequestHandler(
    TechnologyProfileService profiles,
    ProjectKnowledgeService? knowledge,
    HarnessStatusService? harness,
    HarnessRevisionService? revisions,
    McpInventoryService? mcp) : IRequestHandler
{
    public IReadOnlyList<string> Methods => ProtocolMethods.Engineering;

    public async Task<JsonObject> HandleAsync(string method, RequestParams p, CancellationToken ct) => method switch
    {
        ProtocolMethods.EngineeringProfile => new JsonObject { ["profile"] = IntegrationJson.Node(profiles.Get(p.Str("projectId"))) },
        ProtocolMethods.EngineeringAnalyze => new JsonObject
        {
            ["profile"] = IntegrationJson.Node(await profiles.RefreshAsync(p.Str("projectId"), ct))
        },
        ProtocolMethods.EngineeringKnowledge => IntegrationJson.Node(await Knowledge().InspectAsync(p.Str("projectId"), ct))!.AsObject(),
        ProtocolMethods.EngineeringHarness => IntegrationJson.Node(Harness().Inspect(p.Str("projectId")))!.AsObject(),
        ProtocolMethods.EngineeringCatalog => IntegrationJson.Node(new HarnessCatalogService(harness).Read(p.OptionalStr("projectId")))!.AsObject(),
        ProtocolMethods.EngineeringPolicy => IntegrationJson.Node(AgentPolicyService.Read())!.AsObject(),
        ProtocolMethods.EngineeringMcp => IntegrationJson.Node(Mcp().Read(p.Str("projectId")))!.AsObject(),
        ProtocolMethods.EngineeringRevisions => IntegrationJson.Node(Revisions().Read(p.Str("projectId")))!.AsObject(),
        _ => throw new InvalidOperationException($"Método sem tratador: {method}.")
    };

    private ProjectKnowledgeService Knowledge() =>
        knowledge ?? throw new EngineException("KNOWLEDGE_UNAVAILABLE", "A visão de conhecimento não está disponível nesta sessão do motor.");

    private HarnessStatusService Harness() =>
        harness ?? throw new EngineException("HARNESS_STATUS_UNAVAILABLE", "O estado do Harness não está disponível nesta sessão do motor.");

    private McpInventoryService Mcp() =>
        mcp ?? throw new EngineException("MCP_INVENTORY_UNAVAILABLE", "O inventário de MCP não está disponível nesta sessão do motor.");

    private HarnessRevisionService Revisions() =>
        revisions ?? throw new EngineException("HARNESS_REVISIONS_UNAVAILABLE", "As revisões do Harness não estão disponíveis nesta sessão do motor.");
}
