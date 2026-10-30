using Mariliz.Application.Ports;
using Mariliz.Core.Policy;
using Mariliz.Core.Text;

namespace Mariliz.Application.Execution;

/// <summary>Registra no diário o que o instalador do TLC gravou, para poder desfazer.</summary>
internal static class CommandEffects
{
    private static readonly string[] WatchedDirectories = [ManagedScope.TlcSkillDirectory, ManagedScope.SkillsLockDirectory];

    public static Dictionary<string, string> Snapshot(IProjectFiles files)
    {
        var hashes = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var path in WatchedDirectories.SelectMany(directory => files.EnumerateFiles(directory)))
            if (files.ReadBytes(path) is { } bytes) hashes[path] = Hashing.Sha256(bytes);
        return hashes;
    }

    public static void Record(IProjectFiles files, RunJournal journal, IReadOnlyDictionary<string, string> before, List<JournalOp> applied)
    {
        var after = Snapshot(files);

        foreach (var (path, hash) in after)
        {
            if (!before.TryGetValue(path, out var previous))
                Add(new JournalOp { Path = path, Op = OpKinds.Create, AfterHash = hash });
            else if (previous != hash)
                Add(new JournalOp { Path = path, Op = OpKinds.Update, BeforeHash = previous, AfterHash = hash, BackedUp = true });
        }

        foreach (var (path, previous) in before.Where(b => !after.ContainsKey(b.Key)))
            Add(new JournalOp { Path = path, Op = OpKinds.Delete, BeforeHash = previous, BackedUp = true });

        if (journal.Ops.Count > 0) journal.ChangesApplied = true;

        void Add(JournalOp op)
        {
            op.Source = OpSources.Command;
            op.Status = OpStatus.Applied;
            journal.Ops.Add(op);
            applied.Add(op);
        }
    }
}
