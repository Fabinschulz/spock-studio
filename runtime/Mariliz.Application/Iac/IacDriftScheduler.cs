using System.Text.Json.Nodes;
using Mariliz.Application.Execution;
using Mariliz.Application.Integrations;
using Mariliz.Application.Ports;
using Mariliz.Application.Workspace;

namespace Mariliz.Application.Iac;

/// <param name="LastRunAt">Quando a última verificação agendada começou, com sucesso ou não.</param>
public sealed record IacDriftSchedule(
    string ProjectId,
    bool Enabled,
    int IntervalHours,
    string? Folder,
    string? Workspace,
    string? ConnectionId,
    DateTimeOffset ChangedAt,
    DateTimeOffset? LastRunAt = null,
    string? LastOperationId = null,
    string? LastError = null);

public sealed record IacDriftScheduleView(IacDriftSchedule? Schedule, DateTimeOffset? NextRunAt, IReadOnlyList<int> Intervals);

public sealed record IacDriftScheduleRequest(string ProjectId, bool Enabled, int IntervalHours, string? Folder, string? Workspace, string? ConnectionId);

public interface IIacDriftScheduleStore
{
    IReadOnlyList<IacDriftSchedule> All();

    void Save(IacDriftSchedule schedule);
}

// Drift verificado sozinho, de tempos em tempos, enquanto o app está aberto. Só a pessoa liga, escolhe pasta, ambiente,
// conexão e intervalo; a verificação é a mesma do botão (lê a nuvem, nunca muda nada) e entra no histórico como pedida
// pelo agendamento. Quem roda é um laço do motor; não há serviço do sistema operacional nem nada fora do app.
public sealed class IacDriftScheduler(IacOperationService operations, IIacDriftScheduleStore store, WorkspaceAuthorizer workspace, IClock clock,
    IAuditLog audit)
{
    public const string Requester = "schedule";
    public static readonly IReadOnlyList<int> Intervals = [6, 12, 24, 168];

    public IacDriftScheduleView Get(string projectId)
    {
        RequireRoot(projectId);
        var schedule = store.All().FirstOrDefault(s => s.ProjectId == projectId);
        return new IacDriftScheduleView(schedule, NextRun(schedule), Intervals);
    }

    public IacDriftScheduleView Set(IacDriftScheduleRequest request)
    {
        RequireRoot(request.ProjectId);
        if (!Intervals.Contains(request.IntervalHours)) throw IacErrors.ScheduleIntervalInvalid(Intervals);
        // As mesmas conferências do botão (pasta, ambiente, conexão vinculada), na hora de ligar e não só na hora de rodar.
        if (request.Enabled) operations.CheckTarget(request.ProjectId, request.Folder, request.Workspace, request.ConnectionId);
        var previous = store.All().FirstOrDefault(s => s.ProjectId == request.ProjectId);
        var schedule = new IacDriftSchedule(request.ProjectId, request.Enabled, request.IntervalHours, Clean(request.Folder), Clean(request.Workspace),
            Clean(request.ConnectionId), clock.UtcNow, previous?.LastRunAt, previous?.LastOperationId, previous?.LastError);
        store.Save(schedule);
        audit.Record("iac.drift_schedule_changed", new JsonObject
        {
            ["projectId"] = request.ProjectId,
            ["enabled"] = request.Enabled,
            ["intervalHours"] = request.IntervalHours,
            ["folder"] = schedule.Folder,
            ["workspace"] = schedule.Workspace ?? "default",
            ["connectionId"] = schedule.ConnectionId
        });
        return new IacDriftScheduleView(schedule, NextRun(schedule), Intervals);
    }

    // Roda as verificações vencidas, uma por vez. Projeto ocupado com outra operação fica para a próxima volta; qualquer outra
    // falha conta como rodada (para não repetir a cada minuto) e fica no agendamento e na trilha.
    public async Task<int> RunDueAsync(CancellationToken ct)
    {
        var ran = 0;
        foreach (var schedule in store.All().Where(s => s.Enabled && NextRun(s) <= clock.UtcNow))
        {
            ct.ThrowIfCancellationRequested();
            if (!workspace.TryGetRoot(schedule.ProjectId, out _)) continue;
            var started = clock.UtcNow;
            try
            {
                var operation = await operations.DriftAsync(new IacPlanRequest(schedule.ProjectId, schedule.Folder, schedule.Workspace, schedule.ConnectionId),
                    Requester, ct);
                store.Save(schedule with { LastRunAt = started, LastOperationId = operation.Id, LastError = operation.Error is null ? null : "A verificação falhou." });
                ran++;
            }
            catch (EngineException ex) when (ex.Code == IacErrors.OperationRunningCode)
            {
            }
            catch (EngineException ex)
            {
                store.Save(schedule with { LastRunAt = started, LastError = ex.Message });
                audit.Record("iac.drift_schedule_failed", new JsonObject { ["projectId"] = schedule.ProjectId, ["code"] = ex.Code });
            }
        }
        return ran;
    }

    private DateTimeOffset? NextRun(IacDriftSchedule? schedule) =>
        schedule is not { Enabled: true } ? null : (schedule.LastRunAt ?? schedule.ChangedAt).AddHours(schedule.LastRunAt is null ? 0 : schedule.IntervalHours);

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private void RequireRoot(string projectId)
    {
        if (!workspace.TryGetRoot(projectId, out _)) throw EngineErrors.ProjectNotAuthorized();
    }
}
