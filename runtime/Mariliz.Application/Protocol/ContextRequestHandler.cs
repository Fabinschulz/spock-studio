using System.Text.Json.Nodes;
using Mariliz.Application.Context;
using Mariliz.Application.Integrations;

namespace Mariliz.Application.Protocol;

internal sealed class ContextRequestHandler(ContextReviewService service)
{
    public async Task<JsonObject> HandleAsync(string method, RequestParams p, CancellationToken ct) => method switch
    {
        ProtocolMethods.ContextReview => IntegrationJson.Node(await service.ReviewAsync(p.Str("projectId"), p.Str("goal"), ct))!.AsObject(),
        _ => throw new InvalidOperationException($"Método sem tratador: {method}.")
    };
}
