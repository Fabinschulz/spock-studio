using System.Text.Json.Nodes;
using Mariliz.Application.Integrations;
using Mariliz.Application.Security;

namespace Mariliz.Application.Protocol;

internal sealed class SecurityRequestHandler(SecurityService service)
{
    public JsonObject Handle(string method, RequestParams p) => method switch
    {
        ProtocolMethods.SecurityScan => IntegrationJson.Node(service.Scan(p.Str("projectId")))!.AsObject(),
        ProtocolMethods.SecurityPlan => IntegrationJson.Node(service.Plan(p.Str("projectId")))!.AsObject(),
        ProtocolMethods.SecurityFix => new JsonObject { ["runId"] = service.StartFix(p.Str("projectId"), p.Str("planId")).RunId },
        _ => throw new InvalidOperationException($"Método sem tratador: {method}.")
    };
}
