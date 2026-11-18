using System.Text.Json.Nodes;
using Mariliz.Application.AiTools;

namespace Mariliz.Application.Protocol;

internal sealed class AgentRequestHandler(AgentRunService service) : IRequestHandler
{
    public IReadOnlyList<string> Methods => ProtocolMethods.Agent;

    Task<JsonObject> IRequestHandler.HandleAsync(string method, RequestParams p, CancellationToken ct) => Task.FromResult(Handle(method, p));

    public JsonObject Handle(string method, RequestParams p) => method switch
    {
        ProtocolMethods.AgentRun => new JsonObject
        {
            ["runId"] = service.Start(p.Str("projectId"), p.Str("tool"), p.Str("prompt")).RunId
        },
        _ => throw new InvalidOperationException($"Método sem tratador: {method}.")
    };
}
