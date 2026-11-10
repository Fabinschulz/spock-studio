using Mariliz.Application.Ports;
using Mariliz.Core.Text;

namespace Mariliz.Application.Execution;

/// <summary>Desfaz uma operação só se o arquivo ainda está como a aplicação o deixou.</summary>
public static class OperationUndoer
{
    public static void Undo(IRunStore store, string projectId, string backupRunId, IProjectFiles files, JournalOp op, List<string> conflicts)
    {
        var scope = op.Source == OpSources.Command ? WriteScope.SpecificationRestore : WriteScope.Managed;
        var currentBytes = files.ReadBytes(op.Path);
        var currentHash = currentBytes is null ? null : Hashing.Sha256(currentBytes);

        // Cada operação diz se o arquivo pode voltar (null) ou por que ele fica como está (o motivo do conflito).
        var conflict = op.Op switch
        {
            OpKinds.Create => UndoCreate(files, op, scope, currentHash),
            OpKinds.Update when currentHash != op.AfterHash => Altered,
            OpKinds.Update => RestoreBackup(store, projectId, backupRunId, files, op, scope),
            OpKinds.Delete when currentHash is not null => "existe de novo depois da aplicação; mantido.",
            OpKinds.Delete => RestoreBackup(store, projectId, backupRunId, files, op, scope),
            _ => null
        };

        if (conflict is null) op.Status = OpStatus.Reverted;
        else Keep(op, conflicts, conflict);
    }

    private const string Altered = "foi alterado depois da aplicação; mantido.";

    private static string? UndoCreate(IProjectFiles files, JournalOp op, WriteScope scope, string? currentHash)
    {
        if (currentHash is null) return null;
        if (currentHash != op.AfterHash) return Altered;

        files.Delete(op.Path, scope);
        files.PruneEmptyDirectories(ProjectPath.Parent(op.Path));
        return null;
    }

    private static void Keep(JournalOp op, List<string> conflicts, string reason)
    {
        conflicts.Add($"{op.Path}: {reason}");
        op.Status = OpStatus.Conflict;
    }

    private static string? RestoreBackup(IRunStore store, string projectId, string runId, IProjectFiles files, JournalOp op, WriteScope scope)
    {
        var backup = store.LoadBackup(projectId, runId, op.Path);
        if (backup is null) return "o backup não está disponível.";

        files.WriteAtomic(op.Path, backup, scope);
        return Hashing.Sha256(files.ReadBytes(op.Path) ?? []) != op.BeforeHash
            ? "a restauração não confere com o estado anterior."
            : null;
    }
}
