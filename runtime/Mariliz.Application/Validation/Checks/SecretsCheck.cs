using Mariliz.Application.Harness;
using Mariliz.Core.Evals;
using Mariliz.Core.Policy;
using static Mariliz.Application.Validation.EvalResults;

namespace Mariliz.Application.Validation.Checks;

internal sealed class SecretsCheck : IEvalCheck
{
    public string Id => EvalIds.Secrets;

    public Task<EvalResult> RunAsync(EvalSpec spec, ValidationContext ctx, IReadOnlyList<EvalResult> completed, CancellationToken ct) =>
        Task.FromResult(Run(spec, ctx));

    private static EvalResult Run(EvalSpec spec, ValidationContext ctx)
    {
        var paths = new List<string> { DeclaredConfig.FileName, ManagedLockFile.FileName, ".mcp.json", Catalog.PathProposal };
        paths.AddRange(ctx.Lock?.ManagedFiles.Select(f => f.Path) ?? []);
        var findings = new List<string>();
        var scanned = 0;

        foreach (var path in paths.Distinct(StringComparer.Ordinal))
        {
            var text = ctx.Files.ReadText(path);
            if (text is null) continue;
            scanned++;
            foreach (var f in SecretScanner.Scan(text)) findings.Add($"{path}:{f.Line} ({f.Rule})");
        }
        return findings.Count == 0
            ? Pass(spec, [$"{scanned} arquivo(s) verificado(s); nenhum formato conhecido de credencial"])
            : Fail(spec, $"{findings.Count} possível(is) credencial(is) encontrada(s); os valores não são exibidos.", findings);
    }
}
