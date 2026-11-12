using System.Text.Json.Nodes;
using Mariliz.Application.Integrations;
using Mariliz.Application.Execution;
using Mariliz.Application.Management;
using Mariliz.Application.Management.Assistant;

namespace Mariliz.Application.Protocol;

/// <summary>Traduz <c>management.*</c> para o <see cref="ManagementService"/>.</summary>
internal sealed class ManagementRequestHandler(ManagementService service, ManagementAssistant? assistant)
{
    public async Task<JsonObject> HandleAsync(string method, RequestParams p, CancellationToken ct) => method switch
    {
        ProtocolMethods.ManagementEvidence => IntegrationJson.Node(Assistant().Evidence(p.Str("projectId")))!.AsObject(),
        ProtocolMethods.ManagementAsk => IntegrationJson.Node(await Assistant().AskAsync(p.Str("projectId"), p.Str("question"), ct))!.AsObject(),
        ProtocolMethods.ManagementPortfolio => IntegrationJson.Node(service.Portfolio())!.AsObject(),
        ProtocolMethods.ManagementMetrics => IntegrationJson.Node(service.Metrics(p.Str("projectId"), p.OptionalInt("weeks")))!.AsObject(),
        ProtocolMethods.ManagementForecast => IntegrationJson.Node(service.Forecast(p.Str("projectId")))!.AsObject(),
        ProtocolMethods.ManagementRiskHistory => IntegrationJson.Node(service.RiskHistory(p.Str("projectId")))!.AsObject(),
        ProtocolMethods.ManagementMilestones => IntegrationJson.Node(service.Milestones(p.Str("projectId")))!.AsObject(),
        ProtocolMethods.ManagementRisks => IntegrationJson.Node(service.Risks(p.Str("projectId")))!.AsObject(),
        _ => throw new InvalidOperationException($"Método sem tratador: {method}.")
    };

    /// <summary>Sem proxy de modelo (CLI, testes de outras áreas), o assistente não existe; os demais métodos seguem.</summary>
    private ManagementAssistant Assistant() =>
        assistant ?? throw new EngineException("MODEL_NOT_CONFIGURED", "O assistente não está disponível nesta sessão do motor.");
}
