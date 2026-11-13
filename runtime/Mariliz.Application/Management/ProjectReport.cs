using Mariliz.Application.Execution;
using Mariliz.Application.Ports;
using Mariliz.Application.Protocol;
using Mariliz.Application.Workspace;

namespace Mariliz.Application.Management;

public sealed record ReportDecision(string Id, DateTimeOffset RecordedAt, string Title, string Decision, int Runs, int Facts);

public sealed record ReportRiskChange(string RiskId, string Title, RiskChange Change);

public sealed record ReportRuns(string Kind, string State, int Count);

// Só fatos do motor sobre o que foi sincronizado e registrado: nenhum texto foi escrito por modelo. As mudanças de risco só existem desde o primeiro registro do histórico.
public sealed record ProjectReport(
    string ProjectId,
    string ProjectName,
    DateTimeOffset GeneratedAt,
    int Weeks,
    DateTimeOffset PeriodFrom,
    DateTimeOffset PeriodTo,
    ProjectMetrics Metrics,
    ProjectForecast Forecast,
    ProjectRisks Risks,
    IReadOnlyList<ReportRiskChange> RiskChanges,
    ProjectMilestones Milestones,
    IReadOnlyList<ReportDecision> Decisions,
    IReadOnlyList<ReportRuns> Runs,
    string Basis);

// Não calcula nada novo e não grava: cada parte é a resposta dos métodos de Gestão, decisões e execuções, recortada pelo período.
public sealed class ReportService(
    WorkspaceAuthorizer workspace, ManagementService management, DecisionService decisions, IRunStore runs, IClock clock)
{
    public const int DefaultWeeks = 4;
    public const int MaxRiskChanges = 50;

    public const string Basis =
        "Todos os números e textos deste relatório vêm do motor, calculados sobre o que foi sincronizado e registrado; nenhum "
        + "texto foi escrito por modelo de linguagem. O histórico de riscos só existe desde o primeiro registro, e as execuções "
        + "são as que começaram no período.";

    public ProjectReport Build(string projectId, int? weeks)
    {
        var period = weeks ?? DefaultWeeks;
        if (period is < ManagementService.MinWeeks or > ManagementService.MaxWeeks)
            throw new ParamException($"'weeks' precisa estar entre {ManagementService.MinWeeks} e {ManagementService.MaxWeeks}.");
        var project = workspace.Projects().FirstOrDefault(item => item.ProjectId == projectId) ?? throw EngineErrors.ProjectNotAuthorized();

        var now = clock.UtcNow;
        var from = now.AddDays(-7 * period);
        return new ProjectReport(
            projectId, project.DisplayName, now, period, from, now,
            management.Metrics(projectId, period), management.Forecast(projectId), management.Risks(projectId),
            RiskChanges(projectId, from, now), management.Milestones(projectId),
            decisions.Recorded(projectId, from, now).Select(item => new ReportDecision(item.Id, item.RecordedAt, item.Title, item.Decision, item.Runs.Count, item.Facts.Count)).ToList(),
            RunCounts(projectId, from, now), Basis);
    }

    private List<ReportRiskChange> RiskChanges(string projectId, DateTimeOffset from, DateTimeOffset to) =>
        management.RiskHistory(projectId).Trails
            .SelectMany(trail => trail.Changes.Where(change => change.At >= from && change.At <= to).Select(change => new ReportRiskChange(trail.Id, trail.Title, change)))
            .OrderByDescending(item => item.Change.At)
            .ThenBy(item => item.RiskId, StringComparer.Ordinal)
            .Take(MaxRiskChanges)
            .ToList();

    private List<ReportRuns> RunCounts(string projectId, DateTimeOffset from, DateTimeOffset to) =>
        runs.ListJournals(projectId)
            .Where(journal => journal.StartedAt >= from && journal.StartedAt <= to)
            .GroupBy(journal => (journal.Kind, journal.State))
            .Select(group => new ReportRuns(group.Key.Kind, group.Key.State, group.Count()))
            .OrderBy(item => item.Kind, StringComparer.Ordinal)
            .ThenBy(item => item.State, StringComparer.Ordinal)
            .ToList();
}
