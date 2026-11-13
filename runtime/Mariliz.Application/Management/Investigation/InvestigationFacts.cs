using Mariliz.Application.Execution;
using Mariliz.Application.Management.Assistant;
using Mariliz.Application.Ports;
using Mariliz.Application.Timeline;

namespace Mariliz.Application.Management.Investigation;

/// <summary>
/// Os fatos que a investigação soma aos do assistente: o que aconteceu nas execuções do projeto e o que mudou nele, nos últimos
/// <see cref="WindowDays"/> dias. Só contagens e rótulos: nenhum título de item nem texto da pergunta entra aqui.
/// </summary>
internal sealed class InvestigationFacts(IRunStore? runs, ProjectTimelineService? timeline, IClock clock)
{
    public const int WindowDays = 28;

    public IEnumerable<EvidenceFact> For(string projectId) => RunFacts(projectId).Concat(ChangeFacts(projectId));

    private IEnumerable<EvidenceFact> RunFacts(string projectId)
    {
        if (runs is null) yield break;
        var since = clock.UtcNow.AddDays(-WindowDays);
        // Perguntas ao assistente e investigações não são entrega: ficam de fora dos fatos de execução.
        var recent = runs.ListJournals(projectId)
            .Where(journal => journal.StartedAt >= since && journal.Kind is not (RunKinds.Assistant or RunKinds.Investigation))
            .ToList();
        if (recent.Count == 0)
        {
            yield return new("runs.summary", "execuções", $"Nenhuma execução do projeto nos últimos {WindowDays} dias.");
            yield break;
        }

        var failed = recent.Where(journal => journal.State == RunStates.Failed).ToList();
        var limited = recent.Count(journal => journal.State == RunStates.CompletedWithLimitations);
        var reverted = recent.Count(journal => journal.RevertedByRunId is not null || journal.RolledBack);
        yield return new("runs.summary", "execuções",
            $"Execuções do projeto nos últimos {WindowDays} dias: {recent.Count} ({KindCounts(recent)}); {Count(failed.Count, "falhou", "falharam")}, {Count(limited, "terminou com limitações", "terminaram com limitações")}, {Count(reverted, "foi revertida ou desfeita", "foram revertidas ou desfeitas")}.");
        if (failed.Count > 0)
            yield return new("runs.failed", "execuções", $"Execuções que falharam nos últimos {WindowDays} dias, por tipo: {KindCounts(failed)}.");
    }

    private IEnumerable<EvidenceFact> ChangeFacts(string projectId)
    {
        if (timeline is null) yield break;
        var since = clock.UtcNow.AddDays(-WindowDays);
        // Execuções já têm o seu grupo; aqui só o que mudou no projeto em si.
        var kinds = TimelineKinds.All.Select(kind => kind.Id).Where(kind => kind != TimelineKinds.Run).ToList();
        var page = timeline.Read(projectId, kinds, ProjectTimelineService.MaxLimit, null);
        var inWindow = page.Events.Where(item => item.At >= since).ToList();
        var truncated = page.NextBefore is not null && page.Events.Count > 0 && page.Events[^1].At >= since;
        if (inWindow.Count == 0)
        {
            yield return new("changes.summary", "mudanças", $"Nenhuma mudança registrada no projeto nos últimos {WindowDays} dias.");
            yield break;
        }

        var counts = inWindow.GroupBy(item => item.Kind).OrderBy(group => TimelineKinds.All.Select(kind => kind.Id).ToList().IndexOf(group.Key))
            .Select(group => $"{group.Count()} {Label(group.Key, group.Count())}");
        yield return new("changes.summary", "mudanças",
            $"Mudanças no projeto nos últimos {WindowDays} dias{(truncated ? " (ao menos)" : "")}: {string.Join(", ", counts)}.");
        var problems = inWindow.Count(item => item.Level == "problem");
        if (problems > 0) yield return new("changes.problems", "mudanças", $"Dessas mudanças, {problems} aconteceram com problema.");
    }

    private static string Count(int value, string one, string many) => $"{value} {(value == 1 ? one : many)}";

    private static string KindCounts(IEnumerable<RunJournal> journals) =>
        string.Join(", ", journals.GroupBy(journal => journal.Kind).OrderByDescending(group => group.Count()).ThenBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => $"{group.Count()} {KindLabel(group.Key, group.Count())}"));

    private static string KindLabel(string kind, int count) => (kind, count == 1) switch
    {
        (RunKinds.Apply, true) => "aplicação do Harness",
        (RunKinds.Apply, false) => "aplicações do Harness",
        (RunKinds.Revert, true) => "reversão do Harness",
        (RunKinds.Revert, false) => "reversões do Harness",
        (RunKinds.Validate, true) => "validação",
        (RunKinds.Validate, false) => "validações",
        (RunKinds.Workflow, true) => "pedido de engenharia",
        (RunKinds.Workflow, false) => "pedidos de engenharia",
        (_, true) => "outra execução",
        _ => "outras execuções"
    };

    private static string Label(string kind, int count) => (kind, count == 1) switch
    {
        (TimelineKinds.Harness, true) => "revisão do Harness",
        (TimelineKinds.Harness, false) => "revisões do Harness",
        (TimelineKinds.Profile, true) => "mudança do perfil",
        (TimelineKinds.Profile, false) => "mudanças do perfil",
        (TimelineKinds.Decision, true) => "decisão gerencial",
        (TimelineKinds.Decision, false) => "decisões gerenciais",
        (TimelineKinds.Risk, true) => "mudança de risco",
        (TimelineKinds.Risk, false) => "mudanças de risco",
        (TimelineKinds.Sync, true) => "sincronização",
        (TimelineKinds.Sync, false) => "sincronizações",
        (_, true) => "outro acontecimento",
        _ => "outros acontecimentos"
    };
}
