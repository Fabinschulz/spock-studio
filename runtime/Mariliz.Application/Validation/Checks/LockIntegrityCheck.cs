using Mariliz.Application.Harness;
using Mariliz.Core.Evals;
using Mariliz.Core.Policy;
using Mariliz.Core.Text;
using static Mariliz.Application.Validation.EvalResults;

namespace Mariliz.Application.Validation.Checks;

internal sealed class LockIntegrityCheck : IEvalCheck
{
    public string Id => EvalIds.Lock;

    public Task<EvalResult> RunAsync(EvalSpec spec, ValidationContext ctx, IReadOnlyList<EvalResult> completed, CancellationToken ct) =>
        Task.FromResult(Run(spec, ctx));

    private static EvalResult Run(EvalSpec spec, ValidationContext ctx)
    {
        if (ctx.Lock is null) return Block(spec, "Sem mariliz.lock, não há o que conferir.");
        var problems = new List<string>();
        var checkedFiles = 0;

        foreach (var entry in ctx.Lock.ManagedFiles)
        {
            if (!PathGuard.TryNormalize(entry.Path, out var path, out _) || !ManagedScope.IsWritable(path))
            { problems.Add($"{entry.Path}: caminho fora do escopo gerenciado."); continue; }

            var bytes = ctx.Files.ReadBytes(path);
            if (bytes is null) { problems.Add($"{path}: ausente."); continue; }
            checkedFiles++;

            if (entry.Mode == "file" && Hashing.Sha256(bytes) != entry.Sha256) problems.Add($"{path}: o conteúdo difere do hash registrado.");
            if (entry.Mode == "merge" && path == Catalog.PathSettings)
            {
                var text = System.Text.Encoding.UTF8.GetString(bytes);
                if (!text.Contains(Catalog.PathHookScript, StringComparison.Ordinal)) problems.Add($"{path}: o hook gerenciado não está mais registrado.");
            }
        }

        return problems.Count == 0
            ? Pass(spec, [$"{checkedFiles} arquivo(s) conferido(s) contra o lock"])
            : Fail(spec, $"{problems.Count} divergência(s) entre o lock e o disco.", problems);
    }
}
