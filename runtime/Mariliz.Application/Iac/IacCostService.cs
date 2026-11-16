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
public sealed class IacCostService(WorkspaceAuthorizer workspace, CloudHub? cloud, IClock clock)
{
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
