using Mariliz.Application.Execution;
using Mariliz.Application.Harness;
using Mariliz.Application.Ports;
using Mariliz.Core.Discovery;
using Mariliz.Core.Plan;

namespace Mariliz.Application.Validation;

public sealed record ValidationContext(
    IProjectFiles Files,
    string RootPath,
    DiscoveryReport Discovery,
    ManagedLockFile? Lock,
    DeclaredConfig? Declared,
    string? DeclaredError,
    IProcessRunner Runner,
    Func<CancellationToken, Task<ConfigurationPlan>> Replan,
    IReadOnlyList<EvalSummary> PreviousEvals)
{
    /// <summary>
    /// Arquivos que a aplicação gerou. Os mesclados e "kept" ficam no lock para a decisão não se perder,
    /// mas não são avaliados: as regras de conformidade valem para o que geramos.
    /// </summary>
    public IEnumerable<ManagedLockFile.ManagedFileEntry> OwnedFiles() => Lock?.ManagedFiles.Where(f => f.Mode != "kept") ?? [];
}
