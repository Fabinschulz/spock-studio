using Mariliz.Application.Cloud;
using Mariliz.Application.Execution;
using Mariliz.Application.Ports;
using Mariliz.Application.Workspace;

namespace Mariliz.Application.Iac;

/// <param name="Quantity">Quanto do item o mês usa; null nos itens por uso.</param>
/// <param name="UnitPrice">Dólares por unidade; null quando a lista da AWS não tem um preço único.</param>
/// <param name="PriceDescription">A descrição do preço como a AWS publica.</param>
public sealed record IacCostLine(string Resource, string Basis, decimal? Quantity, string Unit, decimal? UnitPrice, decimal? Monthly, string? PriceDescription);

/// <param name="MonthlyFixed">Soma dos itens fixos com preço.</param>
/// <param name="Complete">Todos os itens fixos tiveram preço.</param>
public sealed record IacCostEstimate(
    string Currency,
    string Region,
    string Source,
    DateTimeOffset PricedAt,
    decimal MonthlyFixed,
    bool Complete,
    IReadOnlyList<IacCostLine> Fixed,
    IReadOnlyList<IacCostLine> UsageBased,
    IReadOnlyList<string> Assumptions);

public sealed record IacCostRequest(string ProjectId, string ConnectionId, IacBlueprint Blueprint);

// O preço vem da lista pública do provedor, pela conexão vinculada ao projeto e com a capacidade de preços liberada. Nada
// da conta é lido e nada é gravado; a consulta fica na trilha como leitura cloud.
public sealed class IacCostService(WorkspaceAuthorizer workspace, CloudHub? cloud, IClock clock, IacOperationService? operations = null)
{
    // O custo do que um plano já gerado muda: os recursos cobrados vêm do próprio plano (só atributos de preço), e o preço,
    // da lista pública pela conexão do projeto. Nada é aplicado nem lido da conta.
    public async Task<IacPlanCostEstimate> EstimatePlanAsync(IacPlanCostRequest request, CloudCaller caller, CancellationToken ct)
    {
        if (!workspace.TryGetRoot(request.ProjectId, out _)) throw EngineErrors.ProjectNotAuthorized();
        if (operations is null) throw IacErrors.CostSourceUnavailable();
        var plan = operations.Detail(request.ProjectId, request.OperationId);
        if (plan.Kind != IacOperationKind.Plan || plan.Status != IacOperationStatus.Succeeded || plan.PlanId is null) throw IacErrors.NotAPlan();
        if (plan.Billables is null) throw IacErrors.PlanWithoutCostData();
        if (cloud is null) throw IacErrors.CostSourceUnavailable();
        var connection = cloud.Connections.Require(request.ConnectionId);
        if (connection.Provider != CloudProviders.Aws) throw IacErrors.CostSourceUnavailable();
        var region = request.Region?.Trim() is { Length: > 0 } chosen ? chosen : connection.Settings.Region ?? throw IacErrors.CostSourceUnavailable();

        var sides = plan.Billables.Select(b => (
            Billable: b,
            Before: b.Before is null ? ((IReadOnlyList<IacPlanCost.Part>)[], (string?)null, false) : Side(b.Type, b.Before, region),
            After: b.After is null ? ((IReadOnlyList<IacPlanCost.Part>)[], (string?)null, false) : Side(b.Type, b.After, region))).ToList();
        var queries = sides.SelectMany(s => s.Before.Item1.Concat(s.After.Item1)).Select(p => p.Query)
            .DistinctBy(Key).ToList();
        var prices = queries.Count == 0 ? []
            : (await cloud.Reads.PricesAsync(request.ProjectId, request.ConnectionId, queries, caller, ct)).ToDictionary(q => Key(q.Query), q => q.UsdPerUnit);

        decimal? Monthly((IReadOnlyList<IacPlanCost.Part> Parts, string? Problem, bool Present) side) =>
            !side.Present || side.Problem is not null || side.Parts.Any(p => prices.GetValueOrDefault(Key(p.Query)) is null)
                ? null
                : decimal.Round(side.Parts.Sum(p => p.Quantity * prices[Key(p.Query)]!.Value), 2);
        var lines = sides.Select(s =>
        {
            var problem = s.After.Item2 ?? s.Before.Item2
                          ?? (s.Before.Item3 && Monthly(s.Before) is null || s.After.Item3 && Monthly(s.After) is null ? "sem preço único na lista da AWS" : null);
            return new IacPlanCostLine(s.Billable.Address, s.Billable.Type, s.Billable.Action, IacPlanCost.Detail(s.Billable), Monthly(s.Before),
                Monthly(s.After), problem);
        }).ToList();

        var before = lines.Sum(l => l.Before ?? 0m);
        var after = lines.Sum(l => l.After ?? 0m);
        var uncovered = (plan.Summary?.Changes ?? []).Where(c => c.Action != "read" && !IacPlanBillables.Priced(c.Type)).Select(c => c.Type)
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        var assumptions = new List<string>
        {
            $"Estimativa com os preços públicos sob demanda da AWS em {region}, em dólares, consultados na {IacCostEstimator.Source} em {clock.UtcNow.UtcDateTime:yyyy-MM-dd}.",
            "Mês de 730 horas; só o custo fixo de cada recurso (tráfego, requisições, armazenamento usado e transferência ficam fora).",
            "Sem impostos, câmbio, free tier, Savings Plans, instâncias reservadas nem descontos da conta.",
            "EC2 cotado como Linux compartilhado; outro sistema operacional ou tenancy muda o preço."
        };
        if (plan.Billables.Any(b => IacPlanCost.AssumesSingleAz(b.Type, b.After)))
            assumptions.Add("Banco RDS sem multi_az declarado foi cotado como Single-AZ, o padrão da AWS.");
        if (lines.Any(l => l.Problem is not null))
            assumptions.Add("Recursos sem valor em algum lado ficam fora do total: " + string.Join(", ", lines.Where(l => l.Problem is not null).Select(l => l.Address)) + ".");
        return new IacPlanCostEstimate(plan.Id, plan.PlanId, IacCostEstimator.Currency, region, IacCostEstimator.Source, clock.UtcNow, before, after,
            after - before, lines.All(l => l.Problem is null), lines, uncovered, assumptions);
    }

    private static (IReadOnlyList<IacPlanCost.Part>, string?, bool) Side(string type, IReadOnlyDictionary<string, string> values, string region)
    {
        var (parts, problem) = IacPlanCost.Parts(type, values, region);
        return (parts, problem, true);
    }

    private static string Key(CloudPriceQuery query) =>
        string.Join('|', [query.Service, query.Region, query.UsageType, query.Unit, .. query.Attributes.OrderBy(a => a.Key, StringComparer.Ordinal).Select(a => a.Key + "=" + a.Value)]);

    public async Task<IacCostEstimate> EstimateAsync(IacCostRequest request, CloudCaller caller, CancellationToken ct)
    {
        if (!workspace.TryGetRoot(request.ProjectId, out _)) throw EngineErrors.ProjectNotAuthorized();
        var failed = IacBlueprintChecks.Check(request.Blueprint).Where(c => !c.Passed).ToList();
        if (failed.Count > 0) throw IacErrors.BlueprintInvalid(failed);
        if (cloud is null) throw IacErrors.CostSourceUnavailable();
        if (cloud.Connections.Require(request.ConnectionId).Provider != request.Blueprint.Provider) throw IacErrors.CostSourceUnavailable();

        var items = IacCostEstimator.Items(request.Blueprint);
        var quotes = await cloud.Reads.PricesAsync(request.ProjectId, request.ConnectionId, items.Select(i => i.Query).ToList(), caller, ct);
        return IacCostEstimator.Compose(request.Blueprint, items, quotes, clock.UtcNow);
    }
}
