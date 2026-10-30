using Mariliz.Application.Ports;
using Mariliz.Core.Text;

namespace Mariliz.Application.Execution;

/// <summary>Desfaz uma operação só se o arquivo ainda está como a aplicação o deixou.</summary>
public static class OperationUndoer
{
    public static void Undo(IRunStore store, string projectId, string backupRunId, IProjectFiles files, JournalOp op, List<string> conflicts)
    {
        var scope = op.Source == OpSources.Command ? WriteScope.TlcRestore : WriteScope.Managed;
        var currentBytes = files.ReadBytes(op.Path);
        var currentHash = currentBytes is null ? null : Hashing.Sha256(currentBytes);

        switch (op.Op)
        {
            case OpKinds.Create:
                if (currentHash is null)
                {
                    op.Status = OpStatus.Reverted;
                    return;
                }
                if (currentHash != op.AfterHash)
                {
                    Keep(op, conflicts, "foi alterado depois da aplicação; mantido.");
                    return;
                }
                files.Delete(op.Path, scope);
                files.PruneEmptyDirectories(ProjectPath.Parent(op.Path));
                break;

            case OpKinds.Update:
                if (currentHash != op.AfterHash)
                {
                    Keep(op, conflicts, "foi alterado depois da aplicação; mantido.");
                    return;
                }
                RestoreBackup(store, projectId, backupRunId, files, op, scope, conflicts);
                break;

            case OpKinds.Delete:
                if (currentHash is not null)
                {
                    Keep(op, conflicts, "existe de novo depois da aplicação; mantido.");
                    return;
                }
                RestoreBackup(store, projectId, backupRunId, files, op, scope, conflicts);
                break;
        }

        if (op.Status != OpStatus.Conflict) op.Status = OpStatus.Reverted;
    }

    private static void Keep(JournalOp op, List<string> conflicts, string reason)
    {
        conflicts.Add($"{op.Path}: {reason}");
        op.Status = OpStatus.Conflict;
    }

    private static void RestoreBackup(IRunStore store, string projectId, string runId, IProjectFiles files, JournalOp op, WriteScope scope, List<string> conflicts)
    {
        var backup = store.LoadBackup(projectId, runId, op.Path);
        if (backup is null)
        {
            Keep(op, conflicts, "o backup não está disponível.");
            return;
        }

        files.WriteAtomic(op.Path, backup, scope);
        if (Hashing.Sha256(files.ReadBytes(op.Path) ?? []) != op.BeforeHash)
            Keep(op, conflicts, "a restauração não confere com o estado anterior.");
    }
}
