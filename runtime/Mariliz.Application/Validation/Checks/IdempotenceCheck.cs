using Mariliz.Core.Evals;
using Mariliz.Core.Plan;
using static Mariliz.Application.Validation.EvalResults;

namespace Mariliz.Application.Validation.Checks;

internal sealed class IdempotenceCheck : IEvalCheck
{
    public string Id => EvalIds.Idempotence;

    public async Task<EvalResult> RunAsync(EvalSpec spec, ValidationContext ctx, IReadOnlyList<EvalResult> completed, CancellationToken ct)
    {
        if (ctx.Lock is null) return Block(spec, $"Sem {ctx.Naming.Lock}, não há uma configuração aplicada para repetir.");
        var plan = await ctx.Replan(ct);
        var managed = ctx.Lock.ManagedFiles.Where(f => f.Mode != "kept").Select(f => f.Path).ToHashSet(StringComparer.Ordinal);

        // Mudança em arquivo que a aplicação já gerencia = a configuração não se repete igual: falha.
        var drift = plan.Changes
            .Where(c => c.Action is ChangeAction.Create or ChangeAction.Update or ChangeAction.Remove && managed.Contains(c.Path))
            .Select(c => $"{c.Path} ({ActionText(c.Action)})").ToList();
        // Pendências de decisão (comando, arquivo ainda não aplicado, conflito): não é falha, é o que falta aprovar.
        var pending = plan.Changes
            .Where(c => c.Action == ChangeAction.Conflict || (c.Action is ChangeAction.Create or ChangeAction.Update or ChangeAction.Remove && !managed.Contains(c.Path)))
            .Select(c => $"{c.Path} ({ActionText(c.Action)})").ToList();
        pending.AddRange(plan.Commands.Select(c => $"o comando \"{c.Description}\""));

        if (drift.Count > 0) return Fail(spec, "Um novo plano ainda altera arquivos que a aplicação já gerencia: a configuração não é idempotente.", drift);
        if (pending.Count > 0)
        {
            var items = Plural(pending.Count, "item", "itens");
            var examples = string.Join(", ", pending.Take(3)) + (pending.Count > 3 ? ", entre outros" : "");
            return Block(spec, $"Falta aprovar {items}: {examples}. Só dá para conferir se a configuração se repete igual com tudo aplicado.");
        }

        var preserved = plan.Changes.Count(c => c.Action == ChangeAction.Preserve);
        return Pass(spec, [$"Novo plano: {Plural(preserved, "arquivo preservado", "arquivos preservados")}, nenhuma alteração, nenhum comando"]);
    }

    private static string ActionText(ChangeAction action) => action switch
    {
        ChangeAction.Create => "criar",
        ChangeAction.Update => "atualizar",
        ChangeAction.Remove => "remover",
        _ => "conflito"
    };
}
