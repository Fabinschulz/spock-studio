using System.Text.Json.Nodes;
using Mariliz.Application.Ideas;
using Mariliz.Application.Integrations;

namespace Mariliz.Application.Protocol;

internal sealed class IdeaRequestHandler(IdeaArchitectService service) : IRequestHandler
{
    public IReadOnlyList<string> Methods => ProtocolMethods.Idea;

    public async Task<JsonObject> HandleAsync(string method, RequestParams p, CancellationToken ct) => method switch
    {
        ProtocolMethods.IdeaDraft => Json(await service.DraftAsync(p.Str("projectId"), p.Str("stage"), Artifacts(p), ct)),
        ProtocolMethods.IdeaPlan => Json(service.Plan(p.Str("projectId"), Artifacts(p))),
        ProtocolMethods.IdeaSave => new JsonObject { ["runId"] = service.StartSave(p.Str("projectId"), Artifacts(p), p.Str("planId")).RunId },
        ProtocolMethods.ProjectDesign => new JsonObject { ["design"] = IntegrationJson.Node(service.Design(p.Str("projectId"))) },
        _ => throw new InvalidOperationException($"Método sem tratador: {method}.")
    };

    // Cópias: o que a interface manda não pode ser alterado por fora enquanto o motor confere e grava.
    private static IdeaArtifacts Artifacts(RequestParams p) => new(
        p.Str("idea"),
        p.Object("specification")?.DeepClone().AsObject(),
        p.Object("architecture")?.DeepClone().AsObject(),
        p.Object("backlog")?.DeepClone().AsObject());

    private static JsonObject Json(object value) => IntegrationJson.Node(value)!.AsObject();
}
