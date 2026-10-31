using System.Text.RegularExpressions;
using Mariliz.Core.Evals;
using Mariliz.Core.Policy;
using Mariliz.Core.Text;
using static Mariliz.Application.Validation.EvalResults;

namespace Mariliz.Application.Validation.Checks;

internal sealed class ReferencesCheck : IEvalCheck
{
    public string Id => EvalIds.Reference;

    public Task<EvalResult> RunAsync(EvalSpec spec, ValidationContext ctx, IReadOnlyList<EvalResult> completed, CancellationToken ct) =>
        Task.FromResult(Run(spec, ctx));

    private static EvalResult Run(EvalSpec spec, ValidationContext ctx)
    {
        if (ctx.Lock is null) return Block(spec, "Sem mariliz.lock, não há arquivos gerenciados a conferir.");
        var markdown = ctx.Lock.ManagedFiles.Where(f => f.Path.EndsWith(".md", StringComparison.Ordinal) && f.Mode == "file").ToList();
        var problems = new List<string>();
        var links = 0;

        foreach (var file in markdown)
        {
            var text = ctx.Files.ReadText(file.Path);
            if (text is null) continue;
            var dir = ProjectPath.Parent(file.Path);
            foreach (Match m in Regex.Matches(text, @"\]\((?<t>[^)\s]+)\)"))
            {
                var target = m.Groups["t"].Value;
                if (Regex.IsMatch(target, @"^[a-z][a-z0-9+.-]*:", RegexOptions.IgnoreCase) || target.StartsWith('#')) continue;
                links++;
                var clean = target.Split('#')[0];
                var joined = clean.StartsWith('/') ? clean.TrimStart('/') : (dir.Length == 0 ? clean : dir + "/" + clean);
                if (!PathGuard.TryNormalize(Resolve(joined), out var normalized, out _) || !(ctx.Files.FileExists(normalized) || ctx.Files.DirectoryExists(normalized)))
                    problems.Add($"{file.Path}: o link '{target}' não resolve.");
            }
        }
        return problems.Count == 0
            ? Pass(spec, [$"{markdown.Count} arquivo(s) Markdown, {links} link(s) relativo(s) resolvidos"])
            : Fail(spec, $"{problems.Count} link(s) quebrado(s).", problems);
    }

    private static string Resolve(string path)
    {
        var stack = new List<string>();
        foreach (var part in path.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (part == ".") continue;
            if (part == ".." && stack.Count > 0) stack.RemoveAt(stack.Count - 1);
            else if (part == "..") return "../escape";
            else stack.Add(part);
        }
        return string.Join('/', stack);
    }
}
