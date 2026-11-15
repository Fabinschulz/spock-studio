using System.Text.Json.Nodes;
using Mariliz.Application.Iac;
using Mariliz.Application.Integrations;

namespace Mariliz.Application.Protocol;

// Só a interface chega aqui (a allowlist do host não deixa agentes chamarem iac.*): instalar, adotar, fixar, remover,
// validar e planejar são sempre um clique da pessoa.
internal sealed class IacRequestHandler(IacRuntimeService terraform, IacOperationService operations, IacStateService state, IacGenerationService generation)
{
    public async Task<JsonObject> HandleAsync(string method, RequestParams p, CancellationToken ct) => method switch
    {
        ProtocolMethods.IacState => new JsonObject { ["state"] = IntegrationJson.Node(await state.ReportAsync(p.Str("projectId"), ct)) },
        ProtocolMethods.IacStateResources => new JsonObject
        {
            ["state"] = IntegrationJson.Node(await operations.ReadStateAsync(p.Str("projectId"), p.OptionalStr("folder"), p.OptionalStr("workspace"),
                p.OptionalStr("connectionId"), IacOperationService.Person, ct))
        },
        ProtocolMethods.IacStateUnlock => await Unlocked(p, ct),
        ProtocolMethods.IacBootstrapPlan => new JsonObject { ["plan"] = IntegrationJson.Node(state.BootstrapPlan(Bootstrap(p))) },
        ProtocolMethods.IacBootstrapSave => new JsonObject { ["runId"] = state.StartBootstrap(Bootstrap(p), p.Str("planId")).RunId },
        ProtocolMethods.IacArchitectDraft => new JsonObject
        {
            ["draft"] = IntegrationJson.Node(await generation.DraftAsync(new IacDraftRequest(p.Str("projectId"), p.Str("need"),
                IacBlueprintJson.Blueprint(p.ObjectOrEmpty("facts"))), ct))
        },
        ProtocolMethods.IacGeneratePlan => new JsonObject { ["plan"] = IntegrationJson.Node(generation.Plan(Generate(p))) },
        ProtocolMethods.IacGenerateSave => new JsonObject { ["runId"] = generation.StartSave(Generate(p), p.Str("planId")).RunId },
        ProtocolMethods.IacLayout => new JsonObject { ["layout"] = IntegrationJson.Node(operations.Layout(p.Str("projectId"))) },
        ProtocolMethods.IacValidate => Operation(await operations.ValidateAsync(p.Str("projectId"), IacOperationService.Person, ct)),
        ProtocolMethods.IacPlan => Operation(await operations.PlanAsync(
            new IacPlanRequest(p.Str("projectId"), p.OptionalStr("folder"), p.OptionalStr("workspace"), p.OptionalStr("connectionId")),
            IacOperationService.Person, ct)),
        ProtocolMethods.IacOperations => new JsonObject
        {
            ["operations"] = new JsonArray(operations.History(p.Str("projectId")).Select(o => IntegrationJson.Node(o)).ToArray())
        },
        ProtocolMethods.IacOperation => Operation(operations.Detail(p.Str("projectId"), p.Str("operationId"))),
        // A aprovação vem junto do pedido de aplicar: o planId que a pessoa viu e as confirmações que ela marcou.
        ProtocolMethods.IacApply => Operation(await operations.ApplyAsync(new IacApplyRequest(p.Str("projectId"), p.Str("operationId"), p.Str("planId"),
            p.Flag("destroyConfirmed"), p.Flag("risksAccepted"), p.OptionalStr("workspaceConfirmation")), ct)),
        ProtocolMethods.IacRuntimeStatus => Status(await terraform.StatusAsync(p.OptionalStr("projectId"), ct)),
        ProtocolMethods.IacRuntimeOffer => new JsonObject
        {
            ["offer"] = IntegrationJson.Node(await terraform.OfferAsync(p.OptionalStr("version"), p.OptionalStr("projectId"), OptionalEngine(p), ct))
        },
        ProtocolMethods.IacRuntimeInstall => Status(await terraform.InstallAsync(p.Str("offerId"), p.Str("version"), Engine(p), ct)),
        ProtocolMethods.IacRuntimeDecline => Status(await terraform.DeclineAsync(ct)),
        ProtocolMethods.IacRuntimeRemove => Status(await terraform.RemoveAsync(p.Str("version"), Engine(p), p.Flag("confirmed"), ct)),
        ProtocolMethods.IacRuntimeAdopt => Status(await terraform.AdoptAsync(p.Str("version"), Engine(p), ct)),
        ProtocolMethods.IacRuntimePin => Status(await terraform.PinAsync(p.Str("projectId"), p.OptionalStr("version"), Source(p.OptionalStr("source")), OptionalEngine(p), ct)),
        _ => throw new InvalidOperationException($"Método sem tratador: {method}.")
    };

    // Como nos projetos, a interface só vê o caminho abreviado: a pasta pessoal vira "~".
    private async Task<JsonObject> Unlocked(RequestParams p, CancellationToken ct)
    {
        await operations.UnlockAsync(p.Str("projectId"), p.OptionalStr("folder"), p.OptionalStr("workspace"), p.OptionalStr("connectionId"),
            p.Str("lockId"), p.Flag("confirmed"), ct);
        return new JsonObject { ["unlocked"] = true };
    }

    private static IacGenerateRequest Generate(RequestParams p) =>
        new(p.Str("projectId"), IacBlueprintJson.Blueprint(p.Object("blueprint") ?? throw new ParamException("'blueprint' é obrigatório.")), p.OptionalStr("folder"));

    private static IacBootstrapRequest Bootstrap(RequestParams p) =>
        new(p.Str("projectId"), p.Str("provider"), p.OptionalStr("connectionId"), p.Has("values") ? p.StringMap("values") : new Dictionary<string, string>());

    private static JsonObject Operation(IacOperation operation) => new() { ["operation"] = IntegrationJson.Node(operation) };

    private static JsonObject Status(IacRuntimeStatus status)
    {
        var json = IntegrationJson.Node(status with
        {
            Installations = status.Installations.Select(Display).ToList(),
            Selected = status.Selected is null ? null : Display(status.Selected),
            Project = status.Project is null ? null : status.Project with { Selected = status.Project.Selected is null ? null : Display(status.Project.Selected) }
        });
        return new JsonObject { ["runtime"] = json };
    }

    private static IacInstallation Display(IacInstallation installation)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        return home.Length > 0 && installation.Path.StartsWith(home, comparison)
            ? installation with { Path = "~" + installation.Path[home.Length..] }
            : installation;
    }

    // Sem "engine", a oferta e a fixação usam o OpenTofu; instalar, adotar e remover sempre dizem de qual ferramenta se trata.
    private static IacEngine Engine(RequestParams p) => OptionalEngine(p) ?? throw new ParamException("'engine' é obrigatório.");

    private static IacEngine? OptionalEngine(RequestParams p) => p.OptionalStr("engine") switch
    {
        null => null,
        "openTofu" => IacEngine.OpenTofu,
        "terraform" => IacEngine.Terraform,
        _ => throw new ParamException("'engine' deve ser openTofu ou terraform.")
    };

    private static IacRuntimeSource? Source(string? value) => value switch
    {
        null => null,
        "managed" => IacRuntimeSource.Managed,
        "external" => IacRuntimeSource.External,
        _ => throw new ParamException("'source' deve ser managed ou external.")
    };
}
