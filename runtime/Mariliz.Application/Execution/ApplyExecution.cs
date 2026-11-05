using Mariliz.Application.Harness;
using Mariliz.Application.Models;
using Mariliz.Application.Ports;
using Mariliz.Application.Specification;
using Mariliz.Application.Validation;
using Mariliz.Core.Discovery;
using Mariliz.Core.Evals;
using Mariliz.Core.Plan;
using Mariliz.Core.Policy;
using Mariliz.Core.Text;
using System.Text.Json.Nodes;
using System.Text;

namespace Mariliz.Application.Execution;

/// <summary>
/// Uma aplicação em andamento, na ordem: pré-condições, backup, instalador, escritas, lock e validação.
/// <see cref="Applied"/> acumula o que já foi gravado, para o chamador poder desfazer se algo falhar.
/// </summary>
internal sealed class ApplyExecution(
    IProcessRunner runner,
    IRunStore store,
    IClock clock,
    RunHandle run,
    RunJournal journal,
    string root,
    IProjectFiles files,
    PlanSession session,
    ApplyApprovals approvals,
    Func<CancellationToken, Task<Replanned>> replan,
    Func<CancellationToken, Task<Replanned>> replanAfterApply,
    CancellationToken ct,
    IModelSettings? models = null)
{
    private static readonly TimeSpan InstallTimeout = TimeSpan.FromMinutes(5);

    private static readonly string[] BlockingLimitations =
    [
        LimitationCodes.HookBlocked, LimitationCodes.SpecificationBlocked, LimitationCodes.SpecificationNeedsReinstall,
        LimitationCodes.EnvironmentNotSupported, LimitationCodes.EnvironmentNotSelected
    ];

    private readonly RunJournalRecorder _recorder = new(run, journal, store);

    private ConfigurationPlan Plan => session.Plan;

    public List<JournalOp> Applied { get; } = [];

    public async Task<JsonObject> RunAsync()
    {
        run.SetState(RunStates.Running);
        // Antes da primeira etapa: quem acompanha já sabe o que vem. Só entram comandos que são do plano; a checagem
        // de que as aprovações pertencem a ele acontece na primeira etapa.
        run.Publish("run.plan", ApplySteps.Plan(Plan.Commands.Where(c => approvals.CommandIds.Contains(c.Id))));

        var commands = await CheckPreconditionsAsync();
        var decision = WritePlanner.Decide(Plan, approvals);
        journal.NotApplied.AddRange(decision.NotApplied);

        var snapshot = BackUp(decision.Writes, commands);
        var specification = await InstallAsync(commands, snapshot);
        ApplyWrites(decision);
        WriteLock(decision.Outcomes, specification);
        var report = await ValidateAsync();

        return Conclude(report, decision);
    }

    private async Task<IReadOnlyList<PlannedCommand>> CheckPreconditionsAsync()
    {
        var step = _recorder.BeginStep(ApplySteps.Precheck);

        var fresh = await replan(ct);
        if (fresh.Plan.PlanId != Plan.PlanId)
            throw new EngineException("STALE_PLAN", "O projeto mudou desde que o plano foi gerado. Gere o plano de novo e revise as diferenças.");

        approvals.EnsureBelongTo(Plan);
        var commands = Plan.Commands.Where(c => approvals.CommandIds.Contains(c.Id)).ToList();

        _recorder.CompleteStep(step);
        return commands;
    }

    private Dictionary<string, string> BackUp(IReadOnlyList<PlannedWrite> writes, IReadOnlyList<PlannedCommand> commands)
    {
        var step = _recorder.BeginStep(ApplySteps.Backup);

        var snapshot = commands.Count > 0 ? CommandEffects.Snapshot(files) : new Dictionary<string, string>();
        var pathsToBackUp = writes.Where(w => w.Op != OpKinds.Create).Select(w => w.Change.Path).Concat(snapshot.Keys);

        foreach (var path in pathsToBackUp)
        {
            ct.ThrowIfCancellationRequested();
            if (files.ReadBytes(path) is { } bytes) store.SaveBackup(run.ProjectId, run.RunId, path, bytes);
        }

        _recorder.CompleteStep(step);
        return snapshot;
    }

    private async Task<SpecificationStatus> InstallAsync(IReadOnlyList<PlannedCommand> commands, IReadOnlyDictionary<string, string> before)
    {
        var specification = SpecificationInspector.Inspect(files);

        foreach (var command in commands)
        {
            var step = _recorder.BeginStep(ApplySteps.Command(command));

            var result = await RunCommandAsync(command, before);
            EnsureSucceeded(command, result);

            specification = SpecificationInspector.Inspect(files);
            if (specification.State != SpecificationState.Installed)
                throw new EngineException("SPECIFICATION_VALIDATION_FAILED", "O instalador terminou, mas a estrutura de especificação não passou na verificação: " + string.Join(" ", specification.Problems));

            _recorder.CompleteStep(step, StepStatus.Completed, $"Estrutura de especificação {specification.Version} instalada e verificada");
        }

        return specification;
    }

    private async Task<ProcessResult> RunCommandAsync(PlannedCommand command, IReadOnlyDictionary<string, string> before)
    {
        try
        {
            return await runner.RunAsync(new ProcessSpec(command.Executable, command.Arguments, root, InstallTimeout), ct);
        }
        finally
        {
            // Mesmo cancelado ou falho, o que o instalador já gravou entra no diário para poder ser desfeito.
            CommandEffects.Record(files, journal, before, Applied);
            _recorder.Save();
        }
    }

    private static void EnsureSucceeded(PlannedCommand command, ProcessResult result)
    {
        if (result.NotFound)
            throw new EngineException("COMMAND_NOT_FOUND", $"O programa '{command.Executable}' não foi encontrado.");
        if (result.TimedOut)
            throw new EngineException("COMMAND_TIMEOUT", "O instalador excedeu o tempo limite." + ProcessOutput.Tail(result));
        if (result.ExitCode != 0)
            throw new EngineException("COMMAND_FAILED", $"O instalador terminou com código {result.ExitCode}.{ProcessOutput.Tail(result)}");
    }

    private void ApplyWrites(WriteDecision decision)
    {
        var step = _recorder.BeginStep(ApplySteps.Write);
        var contents = session.Composed.Files.ToDictionary(f => f.Path, f => PlanBuilder.Normalize(f.Content), StringComparer.Ordinal);

        foreach (var (change, kind) in decision.Writes)
        {
            ct.ThrowIfCancellationRequested();

            var op = new JournalOp
            {
                Path = change.Path,
                Op = kind,
                BeforeHash = change.BeforeHash,
                AfterHash = kind == OpKinds.Delete ? null : change.AfterHash,
                BackedUp = kind != OpKinds.Create
            };
            journal.Ops.Add(op);
            _recorder.Save();

            var path = EnsureWritable(change.Path);
            decision.Outcomes[change.Path] = kind == OpKinds.Delete ? Remove(path) : Write(path, change, contents[change.Path]);

            MarkApplied(op);
            run.Publish("file.applied", new JsonObject { ["path"] = change.Path, ["op"] = kind });
        }

        _recorder.CompleteStep(step, StepStatus.Completed, $"{decision.Writes.Count} operação(ões)");
    }

    private static string EnsureWritable(string path) =>
        PathGuard.TryNormalize(path, out var normalized, out _) && ManagedScope.IsWritable(normalized)
            ? normalized
            : throw new EngineException("PATH_OUT_OF_SCOPE", $"O caminho '{path}' está fora do que a aplicação pode alterar.");

    private FileOutcome Remove(string path)
    {
        files.Delete(path);
        files.PruneEmptyDirectories(ProjectPath.Parent(path));
        return new FileOutcome(OutcomeKind.Removed, null);
    }

    private FileOutcome Write(string path, FileChange change, string content)
    {
        files.WriteAtomic(path, Encoding.UTF8.GetBytes(content));

        if (Hashing.Sha256(files.ReadBytes(path) ?? []) != change.AfterHash)
            throw new EngineException("WRITE_VERIFICATION_FAILED", $"O arquivo '{change.Path}' foi gravado, mas o conteúdo não confere com o plano.");

        return new FileOutcome(OutcomeKind.Written, change.AfterHash);
    }

    private void WriteLock(IReadOnlyDictionary<string, FileOutcome> outcomes, SpecificationStatus specification)
    {
        var step = _recorder.BeginStep(ApplySteps.Lock);
        ct.ThrowIfCancellationRequested();

        var content = LockBuilder.BuildFromOutcomes(session.Composed, outcomes, ManagedLockFile.Read(files), specification);
        var newHash = Hashing.Sha256(Encoding.UTF8.GetBytes(content));
        var current = files.ReadBytes(ManagedLockFile.FileName);

        if (current is null || Hashing.Sha256(current) != newHash)
        {
            if (current is not null) store.SaveBackup(run.ProjectId, run.RunId, ManagedLockFile.FileName, current);

            var op = new JournalOp
            {
                Path = ManagedLockFile.FileName,
                Op = current is null ? OpKinds.Create : OpKinds.Update,
                BeforeHash = current is null ? null : Hashing.Sha256(current),
                AfterHash = newHash,
                BackedUp = current is not null
            };
            journal.Ops.Add(op);
            _recorder.Save();

            files.WriteAtomic(ManagedLockFile.FileName, Encoding.UTF8.GetBytes(content));
            MarkApplied(op);
        }

        _recorder.CompleteStep(step);
    }

    private void MarkApplied(JournalOp op)
    {
        op.Status = OpStatus.Applied;
        Applied.Add(op);
        journal.ChangesApplied = true;
        _recorder.Save();
    }

    private async Task<ValidationReport> ValidateAsync()
    {
        run.SetState(RunStates.Validating);
        var step = _recorder.BeginStep(ApplySteps.Validate);

        var report = await ValidationService.RunAsync(runner, store, files, root, run.ProjectId, replanAfterApply, ct, models, new RunEvalProgress(run));
        journal.Evals = EvalSummary.From(report);

        _recorder.CompleteStep(step, StepStatus.Completed, report.Outcome.ToString());
        return report;
    }

    private JsonObject Conclude(ValidationReport report, WriteDecision decision)
    {
        var blocking = Plan.Limitations.Where(l => BlockingLimitations.Contains(l.Code)).Select(l => l.Message);
        var keptConflicts = Plan.Changes
            .Where(c => c.Action == ChangeAction.Conflict && decision.Writes.All(w => w.Change.Id != c.Id))
            .Select(c => $"Mantido o seu arquivo: {c.Path}");

        journal.Pending = journal.NotApplied.Select(p => $"Não aplicado (não aprovado): {p}")
            .Concat(blocking)
            .Concat(keptConflicts)
            .ToList();

        journal.State = report.Outcome switch
        {
            ValidationOutcome.Failed => RunStates.Failed,
            ValidationOutcome.PassedWithLimitations => RunStates.CompletedWithLimitations,
            _ => journal.Pending.Count > 0 ? RunStates.CompletedWithLimitations : RunStates.Completed
        };
        journal.FinishedAt = clock.UtcNow;
        _recorder.Save();

        return ResultJson.Build(journal, report, Plan, decision.Outcomes);
    }
}
