using System.Text.Json.Nodes;
using Mariliz.Application.Boot;
using Mariliz.Application.Integrations;

namespace Mariliz.Application.Protocol;

internal sealed class BootRequestHandler(BootService service) : IRequestHandler
{
    public IReadOnlyList<string> Methods => ProtocolMethods.Boot;

    public async Task<JsonObject> HandleAsync(string method, RequestParams p, CancellationToken ct) => method switch
    {
        ProtocolMethods.BootStart => Wrap(await service.StartAsync(
            new BootStartRequest(p.OptionalStr("projectId"), p.OptionalStr("text"), p.OptionalStr("hint"), p.OptionalStr("capabilityId"), Autonomy(p), "ui", Source(p)), ct)),
        ProtocolMethods.BootProject => Wrap(await service.StartProjectAsync(p.Str("projectId"), Autonomy(p), "ui", Source(p), ct)),
        ProtocolMethods.BootResume => Wrap(await service.ResumeAsync(p.OptionalStr("projectId"), p.Str("bootId"), ct)),
        ProtocolMethods.BootDetail => Wrap(service.Detail(p.OptionalStr("projectId"), p.Str("bootId"))),
        ProtocolMethods.BootList => new JsonObject { ["boots"] = new JsonArray([.. service.List(p.OptionalStr("projectId"), p.OptionalInt("limit") ?? 20).Select(Summary)]) },
        ProtocolMethods.BootCapabilities => Capabilities(p),
        _ => throw new InvalidOperationException($"Método sem tratador: {method}.")
    };

    // O padrão é o teto da fundação (nível 1, preparação); a tela nunca precisa pedir mais, e pedir mais é recusado pelo serviço.
    private static AutonomyLevel Autonomy(RequestParams p) => p.OptionalInt("autonomy") switch
    {
        null => PermissionEvaluator.Ceiling,
        >= 0 and <= 4 and var level => (AutonomyLevel)level,
        _ => throw new ParamException("'autonomy' precisa ficar entre 0 e 4.")
    };

    private static string? Source(RequestParams p) =>
        p.OptionalStr("source") is { Length: <= 40 } source ? source : null;

    private static JsonObject Wrap(BootRecord record) => new() { ["boot"] = IntegrationJson.Node(record) };

    private static JsonNode? Summary(BootRecord record) => IntegrationJson.Node(new
    {
        record.Id,
        record.Mode,
        record.State,
        record.ProjectId,
        record.CreatedAt,
        record.FinishedAt,
        record.RecoversBootId,
        record.RecoveredByBootId,
        Outcome = record.Result?.Outcome,
        Headline = record.Result?.Headline,
        // Um Boot interrompido não tem manchete: o objetivo (ou o pedido) é o que o distingue na lista.
        Objective = Brief(record.Plan?.Objective ?? record.Request),
        // Sem plano não há o que retomar: o motor recusa, então a tela nem oferece.
        Resumable = BootService.CanResume(record),
        Capabilities = record.Plan?.Capabilities ?? []
    });

    private static string? Brief(string? text) => text is null ? null : text.Length <= 120 ? text : text[..119] + "…";

    private JsonObject Capabilities(RequestParams p)
    {
        var autonomy = Autonomy(p);
        var views = service.Capabilities(p.OptionalStr("projectId"), autonomy).Select(entry => new
        {
            entry.Status.Capability.Id,
            entry.Status.Capability.Name,
            entry.Status.Capability.Description,
            entry.Status.Capability.Domain,
            entry.Status.Capability.Goals,
            entry.Status.Capability.Operation,
            entry.Status.Capability.Mode,
            entry.Status.Capability.Route,
            entry.Status.Capability.Agents,
            entry.Status.Capability.Skills,
            entry.Status.Capability.Workflows,
            entry.Status.Capability.Composes,
            entry.Status.Capability.ExpectedResult,
            entry.Status.Capability.Preconditions,
            Parameters = CapabilityParameters.For(entry.Status.Capability.Id)
                .Select(spec => new { spec.Name, spec.Label, spec.Min, spec.Max }).ToList(),
            Needs = Enum.GetValues<CapabilityNeeds>().Where(flag => flag != CapabilityNeeds.None && entry.Status.Capability.Needs.HasFlag(flag)).Select(flag => flag.ToString()).ToList(),
            entry.Status.Availability,
            entry.Status.Reason,
            Verdict = entry.Decision.Verdict,
            entry.Decision.NeedsApproval,
            RequiredLevel = (int)entry.Decision.RequiredLevel
        }).ToList();
        return new JsonObject { ["autonomy"] = (int)(autonomy > PermissionEvaluator.Ceiling ? PermissionEvaluator.Ceiling : autonomy), ["capabilities"] = IntegrationJson.Node(views) };
    }
}
