using Mariliz.Application.Execution;
using Mariliz.Application.Integrations;
using Mariliz.Application.Ports;
using Mariliz.Application.Protocol;
using Mariliz.Application.Workspace;

namespace Mariliz.Application.Management;

// Só lê o que já foi sincronizado: não chama provedor, não grava nada e não usa modelo de linguagem.
public sealed class ManagementService(WorkspaceAuthorizer workspace, IntegrationHub hub, IClock clock, IRiskHistoryStore? riskHistory = null)
{
    public const int DefaultWeeks = 8;
    public const int MinWeeks = 2;
    public const int MaxWeeks = 26;

    // Ficam na resposta para a interface dizer que faltam.
    public static readonly IReadOnlyList<NotCalculated> NotCalculatedYet =
    [
        new("scopeChange", "Mudança de escopo", "Precisa comparar o escopo do marco no começo e agora, e o histórico do escopo ainda não é guardado."),
        new("predictability", "Previsibilidade", "Precisa do que foi planejado por período, que ainda não é sincronizado.")
    ];

    public Portfolio Portfolio()
    {
        var now = clock.UtcNow;
        var projects = workspace.Projects().Select(project => ProjectRow(project, now)).ToList();
        // Quem pede atenção vem primeiro. Sem ferramenta não é atenção, é falta de dado: fica depois dos que têm dado.
        var ordered = projects
            .OrderByDescending(p => p.Signals.Count(signal => signal.Id != PortfolioSignals.NoSources))
            .ThenBy(p => p.Sources == 0)
            .ThenBy(p => p.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
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

    public ProjectForecast Forecast(string projectId)
    {
        var now = clock.UtcNow;
        var sources = Sources(projectId);
        var complete = Complete(sources);
        var kinds = ItemsByKind(sources).Select(group => ForecastCalculator.Compute(group.Key, group.Value, now, complete)).ToList();
        return new ProjectForecast(projectId, now, ForecastCalculator.Method, complete, ForecastCalculator.Assumptions,
            ForecastCalculator.Limitations, kinds);
    }

    public ProjectMilestones Milestones(string projectId)
    {
        var now = clock.UtcNow;
        var sources = Sources(projectId);
        var complete = Complete(sources);
        var byKind = ItemsByKind(sources)
            .Select(group => (Kind: group.Key, Result: MilestoneCalculator.Compute(group.Key, group.Value, now, complete)))
            .ToList();

        var ordered = byKind.SelectMany(entry => entry.Result.Milestones)
            .OrderBy(milestone => milestone.Open == 0)
            .ThenBy(milestone => milestone.DueDate is null)
            .ThenBy(milestone => milestone.DueDate)
            .ThenBy(milestone => milestone.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(milestone => milestone.Kind, StringComparer.Ordinal)
            .ToList();
        return new ProjectMilestones(projectId, now, complete, ordered.Take(MilestoneCalculator.MaxMilestones).ToList(),
            Math.Max(0, ordered.Count - MilestoneCalculator.MaxMilestones), byKind.Select(entry => entry.Result.Unassigned).ToList(),
            MilestoneCalculator.Assumptions, MilestoneCalculator.Limitations);
    }

    public ProjectRisks Risks(string projectId)
    {
        var now = clock.UtcNow;
        var sources = Sources(projectId);
        var byKind = ItemsByKind(sources);
        return new ProjectRisks(projectId, now, Complete(sources), byKind.Count > 0, RiskRules.Evaluate(byKind, now), RiskRules.All);
    }

    // Só registra com todas as fontes completas: uma leitura parcial faria um risco parecer resolvido quando só faltou chegar.
    public bool CaptureRisks(string projectId)
    {
        if (riskHistory is null) return false;
        var sources = Sources(projectId);
        if (!Complete(sources)) return false;
        var byKind = ItemsByKind(sources);
        if (byKind.Count == 0) return false;

        var now = clock.UtcNow;
        var current = RiskHistoryBuilder.Entries(RiskRules.Evaluate(byKind, now));
        lock (riskHistory)
        {
            var existing = riskHistory.Load(projectId);
            var changed = existing.Count == 0 || !RiskHistoryBuilder.Same(existing[^1].Risks, current);
            riskHistory.Save(projectId, RiskHistoryBuilder.Append(existing, current, now));
            return changed;
        }
    }

    public ProjectRiskHistory RiskHistory(string projectId)
    {
        if (!workspace.IsAuthorized(projectId)) throw EngineErrors.ProjectNotAuthorized();
        return RiskHistoryBuilder.Build(projectId, clock.UtcNow, riskHistory?.Load(projectId) ?? []);
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
