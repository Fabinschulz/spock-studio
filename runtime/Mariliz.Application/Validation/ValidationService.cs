using Mariliz.Application.Execution;
using Mariliz.Application.Harness;
using Mariliz.Application.Ports;
using Mariliz.Core.Evals;

namespace Mariliz.Application.Validation;

/// <summary>Validação usada ao fim da aplicação e pela validação avulsa.</summary>
public static class ValidationService
{
    public static async Task<ValidationReport> RunAsync(
        IProcessRunner runner, IRunStore store, IProjectFiles files, string root, string projectId,
        Func<CancellationToken, Task<Replanned>> replan, CancellationToken ct)
    {
        var fresh = await replan(ct);
        var (declared, declaredError) = DeclaredConfig.TryRead(files);

        var previous = store.ListJournals(projectId)
            .Where(j => j.Evals.Count > 0 && j.Kind is RunKinds.Apply or RunKinds.Validate)
            .OrderByDescending(j => j.StartedAt)
            .FirstOrDefault()?.Evals ?? [];

        var context = new ValidationContext(files, root, fresh.Discovery, ManagedLockFile.Read(files), declared, declaredError, runner,
            async c => (await replan(c)).Plan, previous);

        return await EvalRunner.RunAsync(context, ct);
    }
}
