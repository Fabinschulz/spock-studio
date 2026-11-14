using Mariliz.Application.Ports;
using Mariliz.Core.Policy;
using Mariliz.Core.Text;

namespace Mariliz.Application.Execution;

// O que a pessoa vê antes de aprovar: a diferença e os hashes que a gravação vai conferir.
public sealed record ReviewedChange(string Path, string Op, string Diff, bool Truncated, string? BeforeHash, string AfterHash);

internal static class ReviewedChanges
{
    public static (ReviewedChange Change, ApprovedWrite Write) Prepare(IProjectFiles files, string path, string after)
    {
        var before = files.ReadText(path);
        // A diferença vai para a tela: linha com formato de credencial sai dos dois lados, como no plano do Harness.
        var (diff, truncated) = UnifiedDiff.Create(path, before is null ? null : SecretScanner.RedactLines(before), SecretScanner.RedactLines(after));
        var beforeBytes = files.ReadBytes(path);
        var beforeHash = beforeBytes is null ? null : Hashing.Sha256(beforeBytes);
        var op = beforeBytes is null ? OpKinds.Create : OpKinds.Update;
        var afterHash = Hashing.Sha256(after);
        return (new ReviewedChange(path, op, diff, truncated, beforeHash, afterHash), new ApprovedWrite(path, op, beforeHash, afterHash, after));
    }

    // Depende só do estado dos arquivos: refeito na hora de gravar, tem de dar o mesmo id que a pessoa revisou.
    public static string PlanId(string prefix, IEnumerable<ReviewedChange> changes) =>
        prefix + Hashing.Sha256(string.Join("\n", changes.Select(change => $"{change.Path}|{change.BeforeHash}|{change.AfterHash}")))[..12];
}
