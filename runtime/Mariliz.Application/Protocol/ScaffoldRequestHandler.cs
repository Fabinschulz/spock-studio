using System.Text.Json.Nodes;
using Mariliz.Application.Scaffold;

namespace Mariliz.Application.Protocol;

internal sealed class ScaffoldRequestHandler(ScaffoldService service) : IRequestHandler
{
    public IReadOnlyList<string> Methods => ProtocolMethods.Scaffold;

    Task<JsonObject> IRequestHandler.HandleAsync(string method, RequestParams p, CancellationToken ct) => Task.FromResult(Handle(method, p));

    public JsonObject Handle(string method, RequestParams p) => method switch
    {
        ProtocolMethods.ProjectScaffold => new JsonObject { ["scaffold"] = ScaffoldService.Json(service.Read(p.Str("projectId"))) },
        _ => throw new InvalidOperationException($"Método sem tratador: {method}.")
    };
}
