using System.Text.Json.Nodes;
using Mariliz.Application.Integrations;
using Mariliz.Application.Management;

namespace Mariliz.Application.Protocol;

internal sealed class DecisionRequestHandler(DecisionService service)
{
    public JsonObject Handle(string method, RequestParams p) => method switch
    {
        ProtocolMethods.DecisionRecord => Record(p),
        ProtocolMethods.DecisionList => new JsonObject { ["decisions"] = service.List(p.Str("projectId")) },
        ProtocolMethods.DecisionGet => new JsonObject { ["decision"] = IntegrationJson.Node(service.Get(p.Str("projectId"), p.Str("decisionId"))) },
        _ => throw new InvalidOperationException($"Método sem tratador: {method}.")
    };

    private JsonObject Record(RequestParams p)
    {
        var (decision, redacted) = service.Record(
            p.Str("projectId"), p.Str("title"), p.Str("decision"), p.OptionalStr("rationale"), p.StringList("runIds"));
        return new JsonObject { ["decision"] = IntegrationJson.Node(decision), ["redacted"] = redacted };
    }
}
