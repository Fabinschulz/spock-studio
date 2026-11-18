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
        if (ctx.Lock is null) return Block(spec, $"Sem {ctx.Naming.Lock}, não há o que conferir.");
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
            if (ManagedRegion.IsRegionMode(entry.Mode) && RegionProblem(entry, System.Text.Encoding.UTF8.GetString(bytes)) is { } problem)
                problems.Add($"{path}: {problem}");
            if (entry.Mode == "merge" && path == Catalog.PathSettings)
            {
                var text = System.Text.Encoding.UTF8.GetString(bytes);
                if (!text.Contains(ctx.Naming.HookScript, StringComparison.Ordinal)) problems.Add($"{path}: o hook gerenciado não está mais registrado.");
            }
            if (entry.Mode == "merge" && path == Catalog.PathCodexHooks)
            {
                var text = System.Text.Encoding.UTF8.GetString(bytes);
                if (!SettingsMerger.HasSession(text) && !SettingsMerger.HasPrompt(text)) problems.Add($"{path}: os hooks do Spock não estão mais registrados.");
            }
            if (entry.Mode == "merge" && path == Catalog.PathCursorHooks && !CursorHooksMerger.HasSession(System.Text.Encoding.UTF8.GetString(bytes)))
                problems.Add($"{path}: o hook do Spock não está mais registrado.");
        }

        return problems.Count == 0
            ? Pass(spec, [$"{checkedFiles} arquivo(s) conferido(s) contra o lock"])
            : Fail(spec, $"{problems.Count} divergência(s) entre o lock e o disco.", problems);
    }

    // Num trecho, só ele é conferido: o resto do arquivo é da pessoa.
    private static string? RegionProblem(ManagedLockFile.ManagedFileEntry entry, string text)
    {
        try
        {
            return ManagedRegion.Extract(entry.Mode, entry.Region ?? "", text) is not { } region
                ? $"o trecho {entry.Region} do Spock sumiu."
                : ManagedRegion.Hash(region) != entry.Sha256 ? $"o trecho {entry.Region} difere do hash registrado." : null;
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException)
        {
            return $"o trecho {entry.Region} não pôde ser lido: {ex.Message}";
        }
    }
}
