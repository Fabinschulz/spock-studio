using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Mariliz.Application.Discovery;
using Mariliz.Application.Harness;
using Mariliz.Application.Ports;
using Mariliz.Application.Validation;
using Mariliz.Core.Discovery;
using Mariliz.Core.Evals;
using Mariliz.Core.Plan;
using Mariliz.Core.Policy;
using Mariliz.Core.Text;

namespace Mariliz.Application.Execution;

public sealed class EngineException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

/// <summary>Plano calculado e guardado na memória do motor até ser aplicado ou descartado.</summary>
public sealed record PlanSession(string ProjectId, string Root, PlanOptions Options, ConfigurationPlan Plan, ComposeResult Composed);

public sealed record ApplyApprovals(
    IReadOnlyList<string> ChangeIds,
    IReadOnlyList<string> CommandIds,
    IReadOnlyDictionary<string, ConflictResolution> Resolutions);

public sealed record Replanned(ConfigurationPlan Plan, ComposeResult Composed, DiscoveryReport Discovery);

/// <summary>
/// Aplica somente o que foi aprovado, na ordem: backup, comando de instalação, escritas, remoções, lock, validação.
/// Cada operação é gravada no diário antes e depois. Se algo falha ou a pessoa cancela no meio, tudo o que já foi
/// aplicado é desfeito. Nada é escrito fora do escopo gerenciado.
/// </summary>
public sealed class ApplyService(IProcessRunner runner, IRunStore store, IClock clock)
{
    private static readonly TimeSpan InstallTimeout = TimeSpan.FromMinutes(5);

    /// <summary>Final da saída do programa, para a pessoa saber o que houve. Credenciais são omitidas antes de qualquer registro.</summary>
    internal static string OutputTail(ProcessResult result)
    {
        var text = SecretScanner.RedactLines(string.Join("\n", new[] { result.StandardError, result.StandardOutput }.Where(t => !string.IsNullOrWhiteSpace(t))));
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).TakeLast(15).ToList();
        if (lines.Count == 0) return " O programa não escreveu nenhuma saída.";
        var tail = string.Join(" | ", lines);
        return " Saída (final): " + (tail.Length > 1200 ? tail[^1200..] : tail);
    }

    private static readonly string[] BlockingLimitations =
    [
        LimitationCodes.HookBlocked, LimitationCodes.TlcBlocked, LimitationCodes.TlcNeedsForce,
        LimitationCodes.EnvironmentNotSupported, LimitationCodes.EnvironmentNotSelected
    ];

    public async Task<JsonObject> ExecuteAsync(
        RunHandle run, string root, IProjectFiles files, PlanSession session, ApplyApprovals approvals,
        Func<CancellationToken, Task<Replanned>> replan, CancellationToken ct,
        Func<CancellationToken, Task<Replanned>>? replanAfterApply = null)
    {
        var journal = new RunJournal { RunId = run.RunId, ProjectId = run.ProjectId, Kind = RunKinds.Apply, PlanId = session.Plan.PlanId, StartedAt = clock.UtcNow };
        void Save() => store.SaveJournal(journal);
        JournalStep Step(string id, string title) { var s = new JournalStep { Id = id, Title = title, Status = "running" }; journal.Steps.Add(s); Save(); run.Publish("step.started", new JsonObject { ["id"] = id, ["title"] = title }); return s; }
        void Done(JournalStep s, string status = "completed", string? detail = null) { s.Status = status; s.Detail = detail; Save(); run.Publish("step.completed", new JsonObject { ["id"] = s.Id, ["status"] = status, ["detail"] = detail }); }

        var applied = new List<JournalOp>();
        try
        {
            run.SetState(RunStates.Running);

            // ---- 1. Pré-condições
            var pre = Step("precheck", "Verificar pré-condições");
            var fresh = await replan(ct);
            if (fresh.Plan.PlanId != session.Plan.PlanId)
                throw new EngineException("STALE_PLAN", "O projeto mudou desde que o plano foi gerado. Gere o plano de novo e revise as diferenças.");

            var plan = session.Plan;
            ValidateApprovals(plan, approvals);
            var contentByPath = session.Composed.Files.ToDictionary(f => f.Path, f => PlanBuilder.Normalize(f.Content), StringComparer.Ordinal);
            var desiredByPath = session.Composed.Files.ToDictionary(f => f.Path, StringComparer.Ordinal);
            var commands = plan.Commands.Where(c => approvals.CommandIds.Contains(c.Id)).ToList();
            Done(pre);

            // ---- 2. Operações de arquivo decididas
            var outcomes = new Dictionary<string, Outcome>(StringComparer.Ordinal);
            var writes = new List<(FileChange Change, string Op)>();
            foreach (var change in plan.Changes)
            {
                var approved = approvals.ChangeIds.Contains(change.Id);
                switch (change.Action)
                {
                    case ChangeAction.Preserve:
                        outcomes[change.Path] = change.Origin == DecisionOrigins.Existing
                            ? new Outcome(OutcomeKind.Kept, change.BeforeHash)
                            : new Outcome(OutcomeKind.Preserved, change.AfterHash ?? change.BeforeHash);
                        break;
                    case ChangeAction.Create when change.Path != ManagedLockFile.FileName:
                    case ChangeAction.Update when change.Path != ManagedLockFile.FileName:
                        if (!approved) { journal.NotApplied.Add(change.Path); outcomes[change.Path] = new Outcome(OutcomeKind.NotApplied, change.BeforeHash); }
                        else writes.Add((change, change.Action == ChangeAction.Create ? "create" : "update"));
                        break;
                    case ChangeAction.Remove:
                        if (!approved) journal.NotApplied.Add(change.Path);
                        else writes.Add((change, "delete"));
                        break;
                    case ChangeAction.Conflict:
                        var resolution = approvals.Resolutions.GetValueOrDefault(change.Id, ConflictResolution.Keep);
                        if (approved && resolution == ConflictResolution.Overwrite)
                            writes.Add((change, change.AfterHash is null ? "delete" : "update"));
                        else outcomes[change.Path] = new Outcome(OutcomeKind.Kept, change.BeforeHash);
                        break;
                }
            }

            // ---- 3. Backups antes de qualquer mudança
            var backup = Step("backup", "Guardar cópia do que será alterado");
            var preCommandSnapshot = commands.Count > 0 ? Snapshot(files) : new Dictionary<string, string>();
            foreach (var (change, op) in writes.Where(w => w.Op != "create"))
            {
                ct.ThrowIfCancellationRequested();
                var bytes = files.ReadBytes(change.Path);
                if (bytes is null) continue;
                store.SaveBackup(run.ProjectId, run.RunId, change.Path, bytes);
            }
            foreach (var path in preCommandSnapshot.Keys)
                if (files.ReadBytes(path) is { } b) store.SaveBackup(run.ProjectId, run.RunId, path, b);
            Done(backup);

            // ---- 4. Comando de instalação (TLC)
            var tlc = TlcInspector.Inspect(files);
            foreach (var command in commands)
            {
                var step = Step("command:" + command.Id, command.Description);
                ProcessResult result;
                try
                {
                    result = await runner.RunAsync(new ProcessSpec(command.Executable, command.Arguments, root, InstallTimeout), ct);
                }
                finally
                {
                    // Mesmo cancelado ou falho, o que o instalador já gravou entra no diário para poder ser desfeito.
                    RecordCommandEffects(files, journal, preCommandSnapshot, applied);
                    Save();
                }

                if (result.NotFound) throw new EngineException("COMMAND_NOT_FOUND", $"O programa '{command.Executable}' não foi encontrado.");
                if (result.TimedOut) throw new EngineException("COMMAND_TIMEOUT", "O instalador excedeu o tempo limite." + OutputTail(result));
                if (result.ExitCode != 0)
                    throw new EngineException("COMMAND_FAILED", $"O instalador terminou com código {result.ExitCode}.{OutputTail(result)}");

                tlc = TlcInspector.Inspect(files);
                if (tlc.State != TlcState.Installed)
                    throw new EngineException("TLC_VALIDATION_FAILED", "O instalador terminou, mas a instalação do TLC não passou na verificação: " + string.Join(" ", tlc.Problems));
                Done(step, "completed", $"TLC {tlc.Version} instalado e verificado");
            }

            // ---- 5. Escritas e remoções
            var write = Step("write", "Aplicar as alterações aprovadas");
            foreach (var (change, op) in writes)
            {
                ct.ThrowIfCancellationRequested();
                var journalOp = new JournalOp { Path = change.Path, Op = op, BeforeHash = change.BeforeHash, AfterHash = op == "delete" ? null : change.AfterHash, BackedUp = op != "create" };
                journal.Ops.Add(journalOp);
                Save();

                if (!PathGuard.TryNormalize(change.Path, out var normalized, out _) || !ManagedScope.IsWritable(normalized))
                    throw new EngineException("PATH_OUT_OF_SCOPE", $"O caminho '{change.Path}' está fora do que a aplicação pode alterar.");

                if (op == "delete")
                {
                    files.Delete(normalized);
                    files.PruneEmptyDirectories(normalized.Contains('/') ? normalized[..normalized.LastIndexOf('/')] : "");
                    outcomes[change.Path] = new Outcome(OutcomeKind.Removed, null);
                }
                else
                {
                    var content = contentByPath[change.Path];
                    files.WriteAtomic(normalized, Encoding.UTF8.GetBytes(content));
                    // Confere o que foi escrito: a gravação só vale se o hash bate com o plano aprovado.
                    if (Hashing.Sha256(files.ReadBytes(normalized) ?? []) != change.AfterHash)
                        throw new EngineException("WRITE_VERIFICATION_FAILED", $"O arquivo '{change.Path}' foi gravado, mas o conteúdo não confere com o plano.");
                    outcomes[change.Path] = new Outcome(OutcomeKind.Written, change.AfterHash);
                }
                journalOp.Status = OpStatus.Applied;
                applied.Add(journalOp);
                journal.ChangesApplied = true;
                Save();
                run.Publish("file.applied", new JsonObject { ["path"] = change.Path, ["op"] = op });
            }
            Done(write, "completed", $"{writes.Count} operação(ões)");

            // ---- 6. Lock
            var lockStep = Step("lock", "Registrar os arquivos gerenciados");
            ct.ThrowIfCancellationRequested();
            var existingLock = ManagedLockFile.Read(files);
            var lockContent = LockBuilder.BuildFromOutcomes(session.Composed, outcomes, existingLock, tlc);
            var lockPath = ManagedLockFile.FileName;
            var currentLock = files.ReadBytes(lockPath);
            var lockHash = Hashing.Sha256(Encoding.UTF8.GetBytes(lockContent));
            if (currentLock is null || Hashing.Sha256(currentLock) != lockHash)
            {
                if (currentLock is not null) store.SaveBackup(run.ProjectId, run.RunId, lockPath, currentLock);
                var lockOp = new JournalOp { Path = lockPath, Op = currentLock is null ? "create" : "update", BeforeHash = currentLock is null ? null : Hashing.Sha256(currentLock), AfterHash = lockHash, BackedUp = currentLock is not null };
                journal.Ops.Add(lockOp);
                Save();
                files.WriteAtomic(lockPath, Encoding.UTF8.GetBytes(lockContent));
                lockOp.Status = OpStatus.Applied;
                applied.Add(lockOp);
                journal.ChangesApplied = true;
                Save();
            }
            Done(lockStep);

            // ---- 7. Validação
            run.SetState(RunStates.Validating);
            var validation = Step("validate", "Validar a configuração aplicada");
            var report = await ValidationService.RunAsync(runner, store, files, root, run.ProjectId, replanAfterApply ?? replan, ct);
            journal.Evals = report.Results.Select(r => new EvalSummary { Id = r.Id, Status = r.Status.ToString(), Required = r.Required }).ToList();
            Done(validation, "completed", report.Outcome.ToString());

            // ---- 8. Resultado
            var limitations = session.Plan.Limitations.Where(l => BlockingLimitations.Contains(l.Code)).ToList();
            journal.Pending = journal.NotApplied.Select(p => $"Não aplicado (não aprovado): {p}")
                .Concat(limitations.Select(l => l.Message))
                .Concat(plan.Changes.Where(c => c.Action == ChangeAction.Conflict && !writes.Any(w => w.Change.Id == c.Id)).Select(c => $"Mantido o seu arquivo: {c.Path}"))
                .ToList();

            var state = report.Outcome switch
            {
                ValidationOutcome.Failed => RunStates.Failed,
                ValidationOutcome.PassedWithLimitations => RunStates.CompletedWithLimitations,
                _ => journal.Pending.Count > 0 ? RunStates.CompletedWithLimitations : RunStates.Completed
            };
            journal.State = state;
            journal.FinishedAt = clock.UtcNow;
            Save();
            return ResultJson.Build(journal, report, session.Plan, outcomes);
        }
        catch (OperationCanceledException)
        {
            return await RollbackAsync(run, files, journal, applied, RunStates.Cancelled, "Execução cancelada; as alterações já feitas foram desfeitas.", null);
        }
        catch (EngineException ex)
        {
            return await RollbackAsync(run, files, journal, applied, RunStates.Failed, ex.Message, ex.Code);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return await RollbackAsync(run, files, journal, applied, RunStates.Failed, $"Falha ao aplicar ({ex.GetType().Name}); as alterações foram desfeitas.", "APPLY_FAILED");
        }
    }

    // ------------------------------------------------------------------ Aprovação

    private static void ValidateApprovals(ConfigurationPlan plan, ApplyApprovals approvals)
    {
        var changeIds = plan.Changes.Select(c => c.Id).ToHashSet();
        foreach (var id in approvals.ChangeIds)
            if (!changeIds.Contains(id)) throw new EngineException("UNKNOWN_CHANGE", "A aprovação cita uma mudança que não faz parte do plano.");
        foreach (var id in approvals.CommandIds)
            if (plan.Commands.All(c => c.Id != id)) throw new EngineException("UNKNOWN_COMMAND", "A aprovação cita um comando que não faz parte do plano.");
        foreach (var (id, _) in approvals.Resolutions)
            if (plan.Changes.FirstOrDefault(c => c.Id == id) is not { Action: ChangeAction.Conflict })
                throw new EngineException("INVALID_RESOLUTION", "Só conflitos aceitam uma decisão de manter ou substituir.");

    }

    // ------------------------------------------------------------------ Comando: efeitos nos arquivos

    private static Dictionary<string, string> Snapshot(IProjectFiles files)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var root in new[] { ManagedScope.TlcSkillDirectory, ManagedScope.SkillsLockDirectory })
            foreach (var path in files.EnumerateFiles(root))
                if (files.ReadBytes(path) is { } bytes) map[path] = Hashing.Sha256(bytes);
        return map;
    }

    private static void RecordCommandEffects(IProjectFiles files, RunJournal journal, Dictionary<string, string> before, List<JournalOp> applied)
    {
        var after = Snapshot(files);
        foreach (var (path, hash) in after)
        {
            if (before.TryGetValue(path, out var old))
            {
                if (old == hash) continue;
                var op = new JournalOp { Path = path, Op = "update", Source = "command", BeforeHash = old, AfterHash = hash, BackedUp = true, Status = OpStatus.Applied };
                journal.Ops.Add(op); applied.Add(op);
            }
            else
            {
                var op = new JournalOp { Path = path, Op = "create", Source = "command", AfterHash = hash, Status = OpStatus.Applied };
                journal.Ops.Add(op); applied.Add(op);
            }
        }
        foreach (var (path, old) in before.Where(b => !after.ContainsKey(b.Key)))
        {
            var op = new JournalOp { Path = path, Op = "delete", Source = "command", BeforeHash = old, BackedUp = true, Status = OpStatus.Applied };
            journal.Ops.Add(op); applied.Add(op);
        }
        if (journal.Ops.Count > 0) journal.ChangesApplied = true;
    }

    // ------------------------------------------------------------------ Falha parcial e cancelamento

    private async Task<JsonObject> RollbackAsync(RunHandle run, IProjectFiles files, RunJournal journal, List<JournalOp> applied, string state, string message, string? code)
    {
        var conflicts = new List<string>();
        foreach (var op in applied.AsEnumerable().Reverse())
        {
            try { UndoOp(run.ProjectId, run.RunId, files, op, conflicts); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                conflicts.Add($"{op.Path}: não foi possível desfazer ({ex.GetType().Name}).");
            }
        }

        journal.RolledBack = applied.Count > 0 && conflicts.Count == 0;
        journal.ChangesApplied = applied.Count > 0 && conflicts.Count > 0;
        journal.State = conflicts.Count > 0 ? RunStates.RequiresIntervention : state;
        journal.Error = code is null ? message : $"{code}: {message}";
        journal.FinishedAt = clock.UtcNow;
        journal.Pending = conflicts;
        store.SaveJournal(journal);
        await Task.CompletedTask;
        return ResultJson.BuildFailure(journal, message, code, conflicts);
    }

    internal void UndoOp(string projectId, string runId, IProjectFiles files, JournalOp op, List<string> conflicts) =>
        RevertHelper.Undo(store, projectId, runId, files, op, conflicts);

    // ------------------------------------------------------------------ Tipos auxiliares

    internal enum OutcomeKind { Written, Removed, Preserved, Kept, NotApplied }
    internal sealed record Outcome(OutcomeKind Kind, string? Hash);
}

/// <summary>Desfaz uma operação só se o arquivo ainda está como a aplicação o deixou.</summary>
public static class RevertHelper
{
    public static void Undo(IRunStore store, string projectId, string backupRunId, IProjectFiles files, JournalOp op, List<string> conflicts)
    {
        var scope = op.Source == "command" ? WriteScope.TlcRestore : WriteScope.Managed;
        var currentBytes = files.ReadBytes(op.Path);
        var currentHash = currentBytes is null ? null : Hashing.Sha256(currentBytes);

        switch (op.Op)
        {
            case "create":
                if (currentHash is null) { op.Status = OpStatus.Reverted; return; }
                if (currentHash != op.AfterHash) { conflicts.Add($"{op.Path}: foi alterado depois da aplicação; mantido."); op.Status = OpStatus.Conflict; return; }
                files.Delete(op.Path, scope);
                files.PruneEmptyDirectories(op.Path.Contains('/') ? op.Path[..op.Path.LastIndexOf('/')] : "");
                break;

            case "update":
                if (currentHash != op.AfterHash) { conflicts.Add($"{op.Path}: foi alterado depois da aplicação; mantido."); op.Status = OpStatus.Conflict; return; }
                RestoreBackup(store, projectId, backupRunId, files, op, scope, conflicts);
                break;

            case "delete":
                if (currentHash is not null) { conflicts.Add($"{op.Path}: existe de novo depois da aplicação; mantido."); op.Status = OpStatus.Conflict; return; }
                RestoreBackup(store, projectId, backupRunId, files, op, scope, conflicts);
                break;
        }
        if (op.Status != OpStatus.Conflict) op.Status = OpStatus.Reverted;
    }

    private static void RestoreBackup(IRunStore store, string projectId, string runId, IProjectFiles files, JournalOp op, WriteScope scope, List<string> conflicts)
    {
        var backup = store.LoadBackup(projectId, runId, op.Path);
        if (backup is null) { conflicts.Add($"{op.Path}: o backup não está disponível."); op.Status = OpStatus.Conflict; return; }
        files.WriteAtomic(op.Path, backup, scope);
        if (Hashing.Sha256(files.ReadBytes(op.Path) ?? []) != op.BeforeHash)
        { conflicts.Add($"{op.Path}: a restauração não confere com o estado anterior."); op.Status = OpStatus.Conflict; }
    }
}

/// <summary>Validação isolada, usada pelo apply (ao final) e pelo método de validação avulsa.</summary>
public static class ValidationService
{
    public static async Task<ValidationReport> RunAsync(
        IProcessRunner runner, IRunStore store, IProjectFiles files, string root, string projectId,
        Func<CancellationToken, Task<Replanned>> replan, CancellationToken ct)
    {
        var fresh = await replan(ct);
        var (declared, declaredError) = DeclaredConfig.TryRead(files);
        var previous = store.ListJournals(projectId)
            .Where(j => j.Evals.Count > 0 && j.Kind is RunKinds.Apply or RunKinds.Validate)
            .OrderByDescending(j => j.StartedAt).FirstOrDefault()?.Evals ?? [];

        var ctx = new ValidationContext(files, root, fresh.Discovery, ManagedLockFile.Read(files), declared, declaredError, runner,
            async c => (await replan(c)).Plan, previous);
        return await EvalRunner.RunAsync(ctx, ct);
    }
}
