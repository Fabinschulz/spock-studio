using System.Text;
using System.Text.RegularExpressions;
using Mariliz.Application.Execution;
using Mariliz.Application.Ports;
using Mariliz.Core.Policy;

namespace Mariliz.Application.Quality.Git;

/// <summary>
/// Lê o que mudou num repositório autorizado, só com comandos de leitura do git: sem shell (executável e argumentos separados),
/// sem checkout, reset, stash, commit nem troca de branch, sem driver de diff nem conversão de texto do repositório, sem o
/// monitor de arquivos e sem travar o índice. O texto do diff nunca é guardado por aqui; quem usa decide o que fica no registro.
/// </summary>
public sealed partial class GitChangeReader(IProcessRunner runner, IProjectFilesFactory filesFactory)
{
    public const int MaxFiles = 500;
    public const int MaxUntrackedFiles = 200;
    public const int MaxUntrackedBytes = 256 * 1024;
    // O executor de processos corta a saída em 1.000.000 de caracteres; perto disso o patch pode estar incompleto.
    private const int PatchLimitChars = 990_000;
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);

    // O que o motor define para o filho: nada de pergunta no terminal, de paginador nem de bloqueio do índice.
    private static readonly IReadOnlyDictionary<string, string> Env = new Dictionary<string, string>
    {
        ["GIT_OPTIONAL_LOCKS"] = "0",
        ["GIT_TERMINAL_PROMPT"] = "0",
        ["GIT_PAGER"] = "cat"
    };

    private static readonly string[] Prefix = ["--no-pager", "-c", "core.fsmonitor=false", "-c", "core.quotepath=false"];

    // Só alterações de conteúdo; o conflito é lido à parte, e o modo/tipo de arquivo não é revisão de código.
    private const string DiffFilter = "--diff-filter=ACDMRT";

    public async Task<ChangeSet> ReadWorkingTreeAsync(string root, CancellationToken ct)
    {
        var info = await ReadRepositoryAsync(root, ct);
        var limitations = new List<string>();

        // Em repositório sem commit não há HEAD: o que está no índice é "tudo novo".
        var baseArgs = info.HeadCommit is null ? new[] { "--cached" } : ["HEAD"];
        if (info.HeadCommit is null) limitations.Add("O repositório ainda não tem nenhum commit: só o que está no índice (git add) e os arquivos novos foram lidos.");

        var conflicts = await ConflictsAsync(root, ct);
        var patch = await RunAsync(root, ct, ["diff", .. baseArgs, "-U3", "--no-color", "--no-ext-diff", "--no-textconv", "-M", "--relative", DiffFilter, "--", "."]);
        RequireSuccess(patch);
        var files = Limit(UnifiedDiffParser.Parse(patch.StandardOutput), limitations, out var truncated);
        if (patch.StandardOutput.Length >= PatchLimitChars)
        {
            truncated = true;
            limitations.Add("A alteração é grande demais para ser lida inteira: o fim do diff ficou de fora.");
        }

        var untracked = await ReadUntrackedAsync(root, ct, limitations);
        var outside = await OutsideProjectAsync(root, info, ct);
        if (outside > 0)
            limitations.Add($"O repositório tem {outside} arquivo(s) alterado(s) fora da pasta do projeto; eles não foram revisados.");
        if (conflicts.Count > 0)
            limitations.Add($"{conflicts.Count} arquivo(s) com conflito de merge não resolvido não foram lidos linha a linha.");

        var all = files.Concat(untracked).Where(file => !conflicts.Contains(file.Path)).Select(Classify).ToList();
        return new ChangeSet(ReviewModes.WorkingTree, info.Branch, info.HeadCommit, null, null, all, conflicts, outside, truncated, limitations, info.Version);
    }

    /// <summary>A base é sempre explícita: o motor não adivinha contra qual branch comparar.</summary>
    public async Task<ChangeSet> ReadBranchAsync(string root, string baseRef, string? headRef, CancellationToken ct)
    {
        if (!IsSafeRef(baseRef)) throw new EngineException("REVIEW_INVALID_REF", "Informe a referência base da comparação (uma branch, tag ou commit).");
        var head = string.IsNullOrWhiteSpace(headRef) ? "HEAD" : headRef.Trim();
        if (!IsSafeRef(head)) throw new EngineException("REVIEW_INVALID_REF", "A referência a revisar não é válida.");

        var info = await ReadRepositoryAsync(root, ct);
        var baseCommit = await ResolveAsync(root, baseRef, ct) ?? throw new EngineException("REVIEW_REF_NOT_FOUND", "A referência base não existe neste repositório.");
        var headCommit = await ResolveAsync(root, head, ct) ?? throw new EngineException("REVIEW_REF_NOT_FOUND", "A referência a revisar não existe neste repositório.");
        var mergeBase = await RunAsync(root, ct, ["merge-base", baseCommit, headCommit]);
        if (mergeBase.ExitCode != 0 || string.IsNullOrWhiteSpace(mergeBase.StandardOutput))
            throw new EngineException("REVIEW_NO_MERGE_BASE", "As duas referências não têm um ancestral em comum: não há o que comparar.");

        var limitations = new List<string>();
        var patch = await RunAsync(root, ct, ["diff", $"{baseCommit}...{headCommit}", "-U3", "--no-color", "--no-ext-diff", "--no-textconv", "-M", "--relative", DiffFilter, "--", "."]);
        RequireSuccess(patch);
        var files = Limit(UnifiedDiffParser.Parse(patch.StandardOutput), limitations, out var truncated);
        if (patch.StandardOutput.Length >= PatchLimitChars)
        {
            truncated = true;
            limitations.Add("A alteração é grande demais para ser lida inteira: o fim do diff ficou de fora.");
        }
        limitations.Add("Revisão de branch: o que está só na pasta de trabalho (sem commit) não entra; use a revisão das alterações locais para isso.");
        return new ChangeSet(ReviewModes.Branch, info.Branch, headCommit, baseRef.Trim(), head, [.. files.Select(Classify)], [], 0, truncated, limitations, info.Version);
    }

    private sealed record RepositoryInfo(string? Branch, string? HeadCommit, string Version);

    private async Task<RepositoryInfo> ReadRepositoryAsync(string root, CancellationToken ct)
    {
        var version = await RunAsync(root, ct, ["--version"]);
        if (version.NotFound) throw new EngineException("REVIEW_GIT_UNAVAILABLE", "O git não está instalado neste computador.");
        var inside = await RunAsync(root, ct, ["rev-parse", "--is-inside-work-tree"]);
        if (inside.ExitCode != 0 || inside.StandardOutput.Trim() != "true")
            throw new EngineException("REVIEW_NOT_A_REPOSITORY", "A pasta do projeto não é um repositório git (ou o git não a considera segura).");

        var branch = await RunAsync(root, ct, ["symbolic-ref", "--short", "-q", "HEAD"]);
        var head = await RunAsync(root, ct, ["rev-parse", "--verify", "-q", "HEAD"]);
        return new RepositoryInfo(
            branch.ExitCode == 0 && branch.StandardOutput.Trim() is { Length: > 0 } name ? name : null,
            head.ExitCode == 0 && head.StandardOutput.Trim() is { Length: > 0 } commit ? commit : null,
            version.StandardOutput.Trim());
    }

    private async Task<string?> ResolveAsync(string root, string reference, CancellationToken ct)
    {
        var result = await RunAsync(root, ct, ["rev-parse", "--verify", "--quiet", "--end-of-options", reference.Trim() + "^{commit}"]);
        return result.ExitCode == 0 && result.StandardOutput.Trim() is { Length: > 0 } commit ? commit : null;
    }

    private async Task<IReadOnlyList<string>> ConflictsAsync(string root, CancellationToken ct)
    {
        var result = await RunAsync(root, ct, ["diff", "--name-only", "--diff-filter=U", "-z", "--relative", "--", "."]);
        return result.ExitCode != 0 ? [] : [.. Paths(result.StandardOutput)];
    }

    // Só quando a pasta do projeto é uma subpasta do repositório existe "fora": a diferença entre o que o repositório inteiro
    // mudou e o que mudou dentro da pasta, com o mesmo filtro nos dois lados.
    private async Task<int> OutsideProjectAsync(string root, RepositoryInfo info, CancellationToken ct)
    {
        var baseArgs = info.HeadCommit is null ? new[] { "--cached" } : ["HEAD"];
        var everything = await RunAsync(root, ct, ["diff", .. baseArgs, "--name-only", "-z", "--no-ext-diff", DiffFilter]);
        var inside = await RunAsync(root, ct, ["diff", .. baseArgs, "--name-only", "-z", "--no-ext-diff", "--relative", DiffFilter, "--", "."]);
        if (everything.ExitCode != 0 || inside.ExitCode != 0) return 0;
        return Math.Max(0, Paths(everything.StandardOutput).Count() - Paths(inside.StandardOutput).Count());
    }

    private async Task<List<ChangedFile>> ReadUntrackedAsync(string root, CancellationToken ct, List<string> limitations)
    {
        var listed = await RunAsync(root, ct, ["ls-files", "--others", "--exclude-standard", "-z", "--", "."]);
        var result = new List<ChangedFile>();
        if (listed.ExitCode != 0) return result;
        var paths = Paths(listed.StandardOutput).ToList();
        if (paths.Count > MaxUntrackedFiles)
        {
            limitations.Add($"Há {paths.Count} arquivos novos ainda não rastreados; só os primeiros {MaxUntrackedFiles} foram lidos.");
            paths = paths.Take(MaxUntrackedFiles).ToList();
        }

        var files = filesFactory.Open(root);
        foreach (var path in paths)
        {
            if (!PathGuard.TryNormalize(path, out var normalized, out _)) continue;
            var bytes = files.ReadBytes(normalized);
            if (bytes is null) { result.Add(new ChangedFile(normalized, null, FileStatuses.Untracked, 0, 0, [], SkipReasons.Unreadable)); continue; }
            if (bytes.Length > MaxUntrackedBytes) { result.Add(new ChangedFile(normalized, null, FileStatuses.Untracked, 0, 0, [], SkipReasons.TooLarge)); continue; }
            if (Array.IndexOf(bytes, (byte)0, 0, Math.Min(bytes.Length, 8000)) >= 0) { result.Add(new ChangedFile(normalized, null, FileStatuses.Untracked, 0, 0, [], SkipReasons.Binary)); continue; }

            var text = Encoding.UTF8.GetString(bytes);
            var lines = text.Split('\n');
            if (lines.Length > 0 && lines[^1].Length == 0) lines = lines[..^1];
            var diffLines = lines.Select((line, index) => new DiffLine('+', null, index + 1, line.TrimEnd('\r'))).ToList();
            result.Add(new ChangedFile(normalized, null, FileStatuses.Untracked, diffLines.Count, 0, [new DiffHunk(1, null, diffLines)], null));
        }
        return result;
    }

    private static List<ChangedFile> Limit(IReadOnlyList<ChangedFile> files, List<string> limitations, out bool truncated)
    {
        truncated = files.Count > MaxFiles;
        if (truncated) limitations.Add($"A alteração tem {files.Count} arquivos; só os primeiros {MaxFiles} foram revisados.");
        return [.. files.Take(MaxFiles)];
    }

    private static ChangedFile Classify(ChangedFile file)
    {
        if (file.SkippedReason is not null) return file;
        var reason = FileClassifier.SkipReason(file);
        return reason is null ? file : file with { SkippedReason = reason, Hunks = file.Status == FileStatuses.Deleted ? file.Hunks : [] };
    }

    // O executor de processos devolve a saída linha a linha com o fim de linha do sistema (CRLF no Windows), inclusive depois
    // de listas separadas por NUL: aqui tudo volta a ser LF, que é o que o git escreve.
    private async Task<ProcessResult> RunAsync(string root, CancellationToken ct, IReadOnlyList<string> arguments)
    {
        var result = await runner.RunAsync(new ProcessSpec("git", [.. Prefix, .. arguments], root, Timeout, Environment: Env), ct);
        return result with { StandardOutput = result.StandardOutput.Replace("\r\n", "\n") };
    }

    private static void RequireSuccess(ProcessResult result)
    {
        if (result.TimedOut) throw new EngineException("REVIEW_GIT_TIMEOUT", "O git demorou demais para ler as alterações.");
        if (result.ExitCode != 0) throw new EngineException("REVIEW_GIT_FAILED", "O git não conseguiu ler as alterações.");
    }

    private static IEnumerable<string> Paths(string nulSeparated) =>
        nulSeparated.TrimEnd('\n').Split('\0', StringSplitOptions.RemoveEmptyEntries).Where(path => PathGuard.TryNormalize(path, out _, out _));

    // Uma referência que começa com "-" viraria opção do git; o resto é o conjunto de caracteres que nomes de ref usam.
    public static bool IsSafeRef(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= 200 && !value.TrimStart().StartsWith('-') && RefPattern().IsMatch(value.Trim());

    [GeneratedRegex(@"^[A-Za-z0-9._/@{}~^+\-]+$")]
    private static partial Regex RefPattern();
}
