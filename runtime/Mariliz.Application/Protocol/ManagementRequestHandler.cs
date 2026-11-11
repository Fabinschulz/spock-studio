using System.Text.Json.Nodes;
using Mariliz.Application.Integrations;
using Mariliz.Application.Management;

namespace Mariliz.Application.Protocol;

/// <summary>Traduz <c>management.*</c> para o <see cref="ManagementService"/>.</summary>
internal sealed class ManagementRequestHandler(ManagementService service)
{
    public JsonObject Handle(string method, RequestParams p) => method switch
    {
        ProtocolMethods.ManagementPortfolio => IntegrationJson.Node(service.Portfolio())!.AsObject(),
        ProtocolMethods.ManagementMetrics => IntegrationJson.Node(service.Metrics(p.Str("projectId"), p.OptionalInt("weeks")))!.AsObject(),
        ProtocolMethods.ManagementForecast => IntegrationJson.Node(service.Forecast(p.Str("projectId")))!.AsObject(),
        ProtocolMethods.ManagementRisks => IntegrationJson.Node(service.Risks(p.Str("projectId")))!.AsObject(),
        _ => throw new InvalidOperationException($"Método sem tratador: {method}.")
    };
}
