using System.Text.Json.Nodes;
using Mariliz.Application.Engineering;
using Mariliz.Application.Integrations;

namespace Mariliz.Application.Protocol;

/// <summary>Traduz <c>engineering.*</c> para o <see cref="TechnologyProfileService"/>.</summary>
internal sealed class EngineeringRequestHandler(TechnologyProfileService profiles)
{
    public async Task<JsonObject> HandleAsync(string method, RequestParams p, CancellationToken ct) => method switch
    {
        ProtocolMethods.EngineeringProfile => new JsonObject { ["profile"] = IntegrationJson.Node(profiles.Get(p.Str("projectId"))) },
        ProtocolMethods.EngineeringAnalyze => new JsonObject
        {
            ["profile"] = IntegrationJson.Node(await profiles.RefreshAsync(p.Str("projectId"), ct))
        },
        _ => throw new InvalidOperationException($"Método sem tratador: {method}.")
    };
}
