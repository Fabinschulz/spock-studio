using System.Text.Json.Nodes;
using Mariliz.Application.Analysis;

namespace Mariliz.Application.Protocol;

internal sealed class ModernizationRequestHandler(ModernizationService service) : IRequestHandler
{
    public IReadOnlyList<string> Methods => ProtocolMethods.Modernization;

    Task<JsonObject> IRequestHandler.HandleAsync(string method, RequestParams p, CancellationToken ct) => Task.FromResult(Handle(method, p));

    public JsonObject Handle(string method, RequestParams p) => method switch
    {
        ProtocolMethods.ModernizationPlan => new JsonObject { ["modernization"] = ModernizationService.Json(service.Read(p.Str("projectId"))) },
        ProtocolMethods.ModernizationDecide => new JsonObject
        {
            ["modernization"] = ModernizationService.Json(service.Decide(p.Str("projectId"), p.Str("stepId"), Approve(p.Str("decision")), p.OptionalStr("rationale")))
        },
        _ => throw new InvalidOperationException($"Método sem tratador: {method}.")
    };

    private static bool Approve(string decision) => decision switch
    {
        ModernizationDecisions.Approved => true,
        ModernizationDecisions.Rejected => false,
        _ => throw new ParamException("'decision' é approved ou rejected.")
    };
}
