using System.Text.Json.Nodes;
using Mariliz.Application.Engineering;
using Mariliz.Application.Execution;
using Mariliz.Application.Integrations;

namespace Mariliz.Application.Protocol;

/// <summary>Traduz <c>engineering.*</c> para o perfil tecnológico e o conhecimento do projeto.</summary>
internal sealed class EngineeringRequestHandler(TechnologyProfileService profiles, ProjectKnowledgeService? knowledge)
{
    public async Task<JsonObject> HandleAsync(string method, RequestParams p, CancellationToken ct) => method switch
    {
        ProtocolMethods.EngineeringProfile => new JsonObject { ["profile"] = IntegrationJson.Node(profiles.Get(p.Str("projectId"))) },
        ProtocolMethods.EngineeringAnalyze => new JsonObject
        {
            ["profile"] = IntegrationJson.Node(await profiles.RefreshAsync(p.Str("projectId"), ct))
        },
        ProtocolMethods.EngineeringKnowledge => IntegrationJson.Node(await Knowledge().InspectAsync(p.Str("projectId"), ct))!.AsObject(),
        _ => throw new InvalidOperationException($"Método sem tratador: {method}.")
    };

    /// <summary>Sem runtime de IA na sessão (CLI, testes de outras áreas), a visão de conhecimento não existe.</summary>
    private ProjectKnowledgeService Knowledge() =>
        knowledge ?? throw new EngineException("KNOWLEDGE_UNAVAILABLE", "A visão de conhecimento não está disponível nesta sessão do motor.");
}
