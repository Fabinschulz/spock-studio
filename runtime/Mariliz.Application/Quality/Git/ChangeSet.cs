namespace Mariliz.Application.Quality.Git;

/// <param name="Kind">'+' linha adicionada, '-' removida, ' ' contexto.</param>
public sealed record DiffLine(char Kind, int? OldNumber, int? NewNumber, string Text);

/// <param name="Symbol">O que o git mostra depois do "@@": a declaração mais próxima acima do trecho, quando ele a reconhece.</param>
public sealed record DiffHunk(int NewStart, string? Symbol, IReadOnlyList<DiffLine> Lines);

public sealed record ChangedFile(
    string Path,
    string? OldPath,
    string Status,
    int Added,
    int Removed,
    IReadOnlyList<DiffHunk> Hunks,
    string? SkippedReason)
{
    public bool Analyzable => SkippedReason is null && Status != FileStatuses.Deleted;

    public IEnumerable<DiffLine> AddedLines => Hunks.SelectMany(hunk => hunk.Lines).Where(line => line.Kind == '+');

    public IEnumerable<DiffLine> RemovedLines => Hunks.SelectMany(hunk => hunk.Lines).Where(line => line.Kind == '-');
}

/// <summary>O que mudou, lido do git sem alterar nada. As limitações dizem o que ficou de fora e por quê.</summary>
public sealed record ChangeSet(
    string Mode,
    string? Branch,
    string? HeadCommit,
    string? Base,
    string? Head,
    IReadOnlyList<ChangedFile> Files,
    IReadOnlyList<string> Conflicts,
    int OutsideProjectFiles,
    bool Truncated,
    IReadOnlyList<string> Limitations,
    string GitVersion);
