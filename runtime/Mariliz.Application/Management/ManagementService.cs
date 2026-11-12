using Mariliz.Application.Integrations;
using Mariliz.Application.Ports;
using Mariliz.Application.Protocol;
using Mariliz.Application.Workspace;

namespace Mariliz.Application.Management;

/// <summary>
/// A visão gerencial: portfólio e indicadores de um projeto. Só lê o que já foi sincronizado; não chama provedor, não
/// grava nada e não usa modelo de linguagem. O cálculo é do <see cref="FlowCalculator"/>.
/// </summary>
public sealed class ManagementService(WorkspaceAuthorizer workspace, IntegrationHub hub, IClock clock)
{
    public const int DefaultWeeks = 8;
    public const int MinWeeks = 2;
    public const int MaxWeeks = 26;

    /// <summary>Pedidos no pedido de gestão que esta versão não calcula. Ficam na resposta para a interface dizer que faltam.</summary>
    public static readonly IReadOnlyList<NotCalculated> NotCalculatedYet =
    [
        new("blockers", "Bloqueios", "O bloqueio no Jira é um campo personalizado, diferente em cada site; ainda não é lido."),
        new("scopeChange", "Mudança de escopo", "Precisa de um escopo de referência (sprint ou marco), que ainda não é sincronizado."),
        new("predictability", "Previsibilidade", "Precisa do que foi planejado por período, que ainda não é sincronizado.")
    ];

    public Portfolio Portfolio()
    {
        var now = clock.UtcNow;
        var projects = workspace.Projects().Select(project => ProjectRow(project, now)).ToList();
        // Quem tem mais pontos de atenção aparece primeiro; empate fica em ordem alfabética.
        var ordered = projects.OrderByDescending(p => p.Signals.Count).ThenBy(p => p.DisplayName, StringComparer.OrdinalIgnoreCase).ToList();
        return new Portfolio(now, DefaultWeeks, ordered);
    }

    public ProjectMetrics Metrics(string projectId, int? weeks)
    {
        var period = weeks ?? DefaultWeeks;
        if (period is < MinWeeks or > MaxWeeks)
            throw new ParamException($"'weeks' precisa estar entre {MinWeeks} e {MaxWeeks}.");

        var now = clock.UtcNow;
        var sources = Sources(projectId);
        var metricSources = sources.Select(source => new MetricSource(
            source.Integration.Id,
            source.Integration.Provider,
            source.Integration.Scope.ResourceName,
            PortfolioSignals.LastFinished(source.Data)?.Status,
            source.Data.Items.Count == 0 ? null : source.Data.Items.Values.Max(item => item.SyncedAt),
            PortfolioSignals.LastFinished(source.Data)?.Status == SyncStatus.Succeeded)).ToList();

        var kinds = ItemsByKind(sources)
            .Select(group => FlowCalculator.Compute(group.Key, group.Value, now, period))
            .ToList();

        return new ProjectMetrics(projectId, now, period, now.AddDays(-7 * period), now,
            Complete(sources), metricSources, kinds, NotCalculatedYet);
    }

    /// <summary>Previsão de conclusão do trabalho aberto, por tipo de dado.</summary>
    public ProjectForecast Forecast(string projectId)
    {
        var now = clock.UtcNow;
        var sources = Sources(projectId);
        var complete = Complete(sources);
        var kinds = ItemsByKind(sources).Select(group => ForecastCalculator.Compute(group.Key, group.Value, now, complete)).ToList();
        return new ProjectForecast(projectId, now, ForecastCalculator.Method, complete, ForecastCalculator.Assumptions,
            ForecastCalculator.Limitations, kinds);
    }

    /// <summary>Riscos detectados por regra fixa sobre os dados do projeto.</summary>
    public ProjectRisks Risks(string projectId)
    {
        var now = clock.UtcNow;
        var sources = Sources(projectId);
        var byKind = ItemsByKind(sources);
        return new ProjectRisks(projectId, now, Complete(sources), byKind.Count > 0, RiskRules.Evaluate(byKind, now), RiskRules.All);
    }

    private static bool Complete(IReadOnlyCollection<(ProjectIntegration Integration, SyncedData Data, ConnectionStatus Connection)> sources) =>
        sources.Count > 0 && sources.All(source => PortfolioSignals.LastFinished(source.Data)?.Status == SyncStatus.Succeeded);

    private PortfolioProject ProjectRow(AuthorizedProject project, DateTimeOffset now)
    {
        var sources = Sources(project.ProjectId);
        var from = now.AddDays(-7 * DefaultWeeks);
        var kinds = ItemsByKind(sources)
            .Select(group => new KindSnapshot(
                group.Key,
                group.Value.Count,
                group.Value.Count(DeliveryRules.IsOpen),
                group.Value.Count(DeliveryRules.IsInProgress),
                group.Value.Count(item => DeliveryRules.IsDelivered(item) && item.ClosedAt >= from && item.ClosedAt < now),
                group.Value.Any(item => item.DueDate is not null) ? group.Value.Count(item => DeliveryRules.IsOverdue(item, now)) : null))
            .ToList();

        var readAt = sources
            .Where(source => source.Data.Items.Count > 0)
            .Select(source => source.Data.Items.Values.Max(item => item.SyncedAt))
            .Order()
            .Select(at => (DateTimeOffset?)at)
            .FirstOrDefault();

        return new PortfolioProject(project.ProjectId, project.DisplayName, project.DisplayPath, sources.Count, kinds, readAt,
            PortfolioSignals.For(sources, kinds, readAt, now));
    }

    private List<(ProjectIntegration Integration, SyncedData Data, ConnectionStatus Connection)> Sources(string projectId)
    {
        var connections = hub.Connections.List().ToDictionary(c => c.Id, c => c.Status, StringComparer.Ordinal);
        return hub.Projects.List(projectId)
            .Select(integration => (integration, hub.Projects.Data(integration),
                connections.GetValueOrDefault(integration.ConnectionId, ConnectionStatus.Disconnected)))
            .ToList();
    }

    /// <summary>Itens de todas as fontes do projeto, separados por tipo, na ordem fixa dos tipos conhecidos.</summary>
    private static List<KeyValuePair<string, List<WorkItem>>> ItemsByKind(
        IEnumerable<(ProjectIntegration Integration, SyncedData Data, ConnectionStatus Connection)> sources) =>
        sources
            .SelectMany(source => source.Data.Items.Values.Select(stored => stored.Item))
            .GroupBy(item => item.Kind, StringComparer.Ordinal)
            .OrderBy(group => group.Key == DataKinds.Issues ? 0 : group.Key == DataKinds.PullRequests ? 1 : 2)
            .ThenBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => new KeyValuePair<string, List<WorkItem>>(group.Key, group.ToList()))
            .ToList();
}
