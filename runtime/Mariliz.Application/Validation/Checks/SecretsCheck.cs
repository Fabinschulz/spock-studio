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
        var paths = new List<string> { ctx.Naming.Declaration, ctx.Naming.Lock, ".mcp.json", ctx.Naming.Proposal };
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
            ? Pass(spec, [$"{Plural(scanned, "arquivo verificado", "arquivos verificados")}; nenhum formato conhecido de credencial"])
            : Fail(spec, $"{Plural(findings.Count, "possível credencial encontrada", "possíveis credenciais encontradas")}; os valores não são exibidos.", findings);
    }
}
