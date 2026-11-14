using System.Text;
using System.Text.Json.Nodes;
using Mariliz.Application.Ports;
using Mariliz.Core.Text;

namespace Mariliz.Application.Execution;

public sealed record ApprovedWrite(string Path, string Op, string? BeforeHash, string AfterHash, string Content);

// Grava o que a pessoa aprovou: cópia antes, cada operação no diário antes e depois, conferência pelo hash e, se algo falhar,
// desfaz o que já tinha gravado. O escopo e a origem dizem também como a reversão vai restaurar.
internal sealed class ApprovedWriter(IRunStore store, IProjectFiles files, WriteScope scope, string source)
{
    // Devolve o motivo da falha, ou null quando tudo foi gravado e conferido.
    public string? Apply(RunHandle run, RunJournal journal, RunJournalRecorder recorder, IReadOnlyList<ApprovedWrite> writes)
    {
        var backup = recorder.BeginStep("backup", "Guardar uma cópia do que vai mudar");
        foreach (var write in writes)
            if (files.ReadBytes(write.Path) is { } bytes) store.SaveBackup(run.ProjectId, run.RunId, write.Path, bytes);
        recorder.CompleteStep(backup);

        var step = recorder.BeginStep("write", "Gravar o que foi aprovado");
        try
        {
            foreach (var write in writes) Write(run, journal, recorder, write);
            recorder.CompleteStep(step, StepStatus.Completed, $"{writes.Count} arquivo(s)");
            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or EngineException)
        {
            RollBack(run, journal);
            recorder.CompleteStep(step, StepStatus.Failed, "A gravação falhou e o que já tinha mudado foi desfeito.");
            return ex.Message;
        }
    }

    private void Write(RunHandle run, RunJournal journal, RunJournalRecorder recorder, ApprovedWrite write)
    {
        var op = new JournalOp
        {
            Path = write.Path,
            Op = write.Op,
            Source = source,
            BeforeHash = write.BeforeHash,
            AfterHash = write.AfterHash,
            BackedUp = write.BeforeHash is not null
        };
        journal.Ops.Add(op);
        recorder.Save();

        files.WriteAtomic(write.Path, Encoding.UTF8.GetBytes(write.Content), scope);
        if (Hashing.Sha256(files.ReadBytes(write.Path) ?? []) != write.AfterHash)
            throw new EngineException("WRITE_VERIFICATION_FAILED", $"O arquivo '{write.Path}' foi gravado, mas não confere com o que foi aprovado.");

        op.Status = OpStatus.Applied;
        journal.ChangesApplied = true;
        recorder.Save();
        run.Publish("file.applied", new JsonObject { ["path"] = write.Path, ["op"] = write.Op });
    }

    private void RollBack(RunHandle run, RunJournal journal)
    {
        var conflicts = new List<string>();
        foreach (var op in Enumerable.Reverse(journal.Ops).Where(op => op.Status == OpStatus.Applied))
            OperationUndoer.Undo(store, run.ProjectId, run.RunId, files, op, conflicts);
        journal.RolledBack = true;
        journal.Pending = conflicts;
    }
}
