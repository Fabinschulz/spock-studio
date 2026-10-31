using Mariliz.Core.Evals;
using Mariliz.Core.Plan;
using static Mariliz.Application.Validation.EvalResults;

namespace Mariliz.Application.Validation.Checks;

internal sealed class IdempotenceCheck : IEvalCheck
{
    public string Id => EvalIds.Idempotence;

    public async Task<EvalResult> RunAsync(EvalSpec spec, ValidationContext ctx, IReadOnlyList<EvalResult> completed, CancellationToken ct)
    {
        if (ctx.Lock is null) return Block(spec, "Sem mariliz.lock, não há uma configuração aplicada para repetir.");
        var plan = await ctx.Replan(ct);
        var managed = ctx.Lock.ManagedFiles.Where(f => f.Mode != "kept").Select(f => f.Path).ToHashSet(StringComparer.Ordinal);

        // Mudança em arquivo que a aplicação já gerencia = a configuração não se repete igual: falha.
        var drift = plan.Changes
            .Where(c => c.Action is ChangeAction.Create or ChangeAction.Update or ChangeAction.Remove && managed.Contains(c.Path))
            .Select(c => $"{c.Path}: {c.Action}").ToList();
        // Pendências de decisão (comando, arquivo ainda não aplicado, conflito): não é falha, é o que falta aprovar.
        var pending = plan.Changes
            .Where(c => c.Action == ChangeAction.Conflict || (c.Action is ChangeAction.Create or ChangeAction.Update or ChangeAction.Remove && !managed.Contains(c.Path)))
            .Select(c => $"{c.Path}: {c.Action}").ToList();
        pending.AddRange(plan.Commands.Select(c => $"comando {c.Id}"));

        if (drift.Count > 0) return Fail(spec, "Um novo plano ainda altera arquivos que a aplicação já gerencia: a configuração não é idempotente.", drift);
        if (pending.Count > 0) return Block(spec, $"Há {pending.Count} item(ns) pendente(s) de aprovação ({string.Join(", ", pending.Take(3))}); a idempotência só pode ser avaliada com tudo aplicado.");

        return Pass(spec, [$"Novo plano: {plan.Changes.Count(c => c.Action == ChangeAction.Preserve)} arquivo(s) preservado(s), nenhuma alteração, nenhum comando"]);
    }
}
