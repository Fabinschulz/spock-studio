using System.Text.Json.Nodes;
using Mariliz.Application.Integrations;
using Mariliz.Application.Management;

namespace Mariliz.Application.Protocol;

/// <summary>Traduz <c>management.portfolio</c> e <c>management.metrics</c> para o <see cref="ManagementService"/>.</summary>
internal sealed class ManagementRequestHandler(ManagementService service)
{
    public JsonObject Handle(string method, RequestParams p) => method switch
    {
        ProtocolMethods.ManagementPortfolio => IntegrationJson.Node(service.Portfolio())!.AsObject(),
        ProtocolMethods.ManagementMetrics => IntegrationJson.Node(service.Metrics(p.Str("projectId"), p.OptionalInt("weeks")))!.AsObject(),
        _ => throw new InvalidOperationException($"Método sem tratador: {method}.")
    };
}
