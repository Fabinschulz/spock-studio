using System.Globalization;
using Mariliz.Application.Engineering;
using Mariliz.Application.Execution;
using Mariliz.Application.Integrations;
using Mariliz.Application.Management;
using Mariliz.Application.Ports;
using Mariliz.Application.Workspace;

namespace Mariliz.Application.Timeline;

public static class TimelineKinds
{
    public const string Harness = "harness";
    public const string Profile = "profile";
    public const string Run = "run";
    public const string Decision = "decision";
    public const string Risk = "risk";
    public const string Sync = "sync";

    public static readonly IReadOnlyList<(string Id, string Label)> All =
    [
        (Harness, "Revisões do Harness"),
        (Profile, "Mudanças do perfil"),
        (Run, "Execuções"),
        (Decision, "Decisões"),
        (Risk, "Riscos"),
        (Sync, "Sincronizações")
    ];
}

// Level: ok, attention, problem ou info. RefType: run ou decision; sem destino, RefType e RefId são nulos. O texto é do motor, sem título de item nem conteúdo de provedor.
public sealed record TimelineEvent(string Id, DateTimeOffset At, string Kind, string Level, string Title, string Detail, string? RefType, string? RefId);

// Available é falso quando a fonte falhou: as outras seguem.
public sealed record TimelineSource(string Kind, string Label, int Count, bool Available);

/// <param name="NextBefore">Passe este instante como <c>before</c> para a página seguinte. Nulo no fim.</param>
public sealed record ProjectTimeline(
    string ProjectId, DateTimeOffset GeneratedAt, IReadOnlyList<TimelineEvent> Events, DateTimeOffset? NextBefore, IReadOnlyList<TimelineSource> Sources);

// É uma projeção: lê o que cada área já guarda e não grava nada. Uma fonte que falha aparece como indisponível sem derrubar as outras.
public sealed class ProjectTimelineService(
    WorkspaceAuthorizer workspace,
    IClock clock,
    IRunStore store,
    HarnessRevisionService? revisions = null,
    TechnologyProfileService? profiles = null,
    DecisionService? decisions = null,
    ManagementService? management = null,
    IntegrationHub? integrations = null)
{
    public const int DefaultLimit = 50;
    public const int MaxLimit = 200;

    public ProjectTimeline Read(string projectId, IReadOnlyList<string> kinds, int? limit, DateTimeOffset? before)
    {
        if (!workspace.IsAuthorized(projectId)) throw EngineErrors.ProjectNotAuthorized();
        var known = TimelineKinds.All.Select(kind => kind.Id).ToHashSet(StringComparer.Ordinal);
        if (kinds.Any(kind => !known.Contains(kind))) throw new EngineException("TIMELINE_KIND_UNKNOWN", "Esse tipo de acontecimento não existe.");
        var page = Math.Clamp(limit ?? DefaultLimit, 1, MaxLimit);

        var all = new List<TimelineEvent>();
        var sources = new List<TimelineSource>();
        var harnessRuns = new HashSet<string>(StringComparer.Ordinal);

        Collect(TimelineKinds.Harness, revisions is null ? null : () => HarnessEvents(projectId, harnessRuns), all, sources);
        Collect(TimelineKinds.Profile, profiles is null ? null : () => ProfileEvents(projectId), all, sources);
        Collect(TimelineKinds.Run, () => RunEvents(projectId, harnessRuns), all, sources);
        Collect(TimelineKinds.Decision, decisions is null ? null : () => DecisionEvents(projectId), all, sources);
        Collect(TimelineKinds.Risk, management is null ? null : () => RiskEvents(projectId), all, sources);
        Collect(TimelineKinds.Sync, integrations is null ? null : () => SyncEvents(projectId), all, sources);

        var visible = all
            .Where(item => kinds.Count == 0 || kinds.Contains(item.Kind))
            .Where(item => before is null || item.At < before)
            .OrderByDescending(item => item.At)
            .ThenBy(item => item.Kind, StringComparer.Ordinal)
            .ThenBy(item => item.Id, StringComparer.Ordinal)
            .ToList();

        var events = visible.Take(page).ToList();
        // A página seguinte pede o que é anterior ao último instante mostrado: quem tem exatamente esse instante entra nesta página,
        // para um empate na fronteira não fazer um acontecimento sumir entre as duas.
        if (visible.Count > page) events.AddRange(visible.Skip(page).TakeWhile(item => item.At == events[^1].At));
        var next = visible.Count > events.Count ? events[^1].At : (DateTimeOffset?)null;
        return new ProjectTimeline(projectId, clock.UtcNow, events, next, sources);
    }

    // O motivo detalhado da falha não sai do motor.
    private static void Collect(string kind, Func<IReadOnlyList<TimelineEvent>>? read, List<TimelineEvent> all, List<TimelineSource> sources)
    {
        if (read is null) return;
        var label = TimelineKinds.All.Single(item => item.Id == kind).Label;
        try
        {
            var events = read();
            all.AddRange(events);
            sources.Add(new TimelineSource(kind, label, events.Count, true));
        }
        catch (Exception ex) when (ex is EngineException or IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            sources.Add(new TimelineSource(kind, label, 0, false));
        }
    }

    private IReadOnlyList<TimelineEvent> HarnessEvents(string projectId, HashSet<string> covered)
    {
        var result = new List<TimelineEvent>();
        foreach (var revision in revisions!.Read(projectId).Revisions)
        {
            covered.Add(revision.RunId);
            var created = revision.Components.Sum(component => component.Created.Count);
            var updated = revision.Components.Sum(component => component.Updated.Count);
            var removed = revision.Components.Sum(component => component.Removed.Count);
            var labels = revision.Components.Select(component => component.Label).Take(3).ToList();
            var more = revision.Components.Count - labels.Count;

            var title = revision.Kind == RunKinds.Revert ? $"Revisão {revision.Number} do Harness: reversão" : $"Revisão {revision.Number} do Harness";
            if (revision.RolledBack) title += " (a aplicação falhou e foi desfeita na hora)";
            var detail = Counts(created, updated, removed)
                + (labels.Count > 0 ? $" Componentes: {string.Join(", ", labels)}{(more > 0 ? $" e mais {more}" : "")}." : "");
            var level = revision.RolledBack ? "attention" : LevelOf(revision.State);
            result.Add(new TimelineEvent($"harness:{revision.RunId}", revision.At, TimelineKinds.Harness, level, title, detail.Trim(), "run", revision.RunId));
        }
        return result;
    }

    private static string Counts(int created, int updated, int removed)
    {
        var parts = new List<string>();
        if (created > 0) parts.Add(created == 1 ? "1 arquivo criado" : $"{created} arquivos criados");
        if (updated > 0) parts.Add(updated == 1 ? "1 atualizado" : $"{updated} atualizados");
        if (removed > 0) parts.Add(removed == 1 ? "1 removido" : $"{removed} removidos");
        return parts.Count == 0 ? "Nenhum arquivo mudou." : string.Join(", ", parts) + ".";
    }

    private IReadOnlyList<TimelineEvent> ProfileEvents(string projectId) =>
        (profiles!.Get(projectId)?.History ?? []).Select(change => new TimelineEvent(
            $"profile:{change.At.UtcTicks}:{change.Kind}:{change.Subject}",
            change.At, TimelineKinds.Profile, "info", ProfileTitle(change), "", null, null)).ToList();

    private static string ProfileTitle(ProfileChange change) => change.Kind switch
    {
        "created" => $"Perfil tecnológico criado{(string.IsNullOrWhiteSpace(change.To) ? "" : $": {change.To}")}",
        "added" => $"{change.Subject} entrou no perfil{(string.IsNullOrWhiteSpace(change.To) ? "" : $" ({change.To})")}",
        "removed" => $"{change.Subject} saiu do perfil",
        "changed" => $"{change.Subject} mudou de {change.From ?? "sem versão"} para {change.To ?? "sem versão"}",
        _ => $"Perfil: {change.Subject}"
    };

    private IReadOnlyList<TimelineEvent> RunEvents(string projectId, HashSet<string> coveredByHarness) =>
        store.ListJournals(projectId)
            .Where(journal => !coveredByHarness.Contains(journal.RunId))
            .Select(journal =>
            {
                var evals = journal.Evals.Count;
                var passed = journal.Evals.Count(item => string.Equals(item.Status, "Passed", StringComparison.OrdinalIgnoreCase));
                var detail = StateLabel(journal.State) + (evals > 0 ? $"; {passed} de {evals} verificações passaram" : "") + ".";
                return new TimelineEvent(
                    $"run:{journal.RunId}", journal.FinishedAt ?? journal.StartedAt, TimelineKinds.Run, LevelOf(journal.State),
                    RunTitle(journal.Kind), Capitalize(detail), "run", journal.RunId);
            }).ToList();

    private static string RunTitle(string kind) => kind switch
    {
        RunKinds.Validate => "Validação do Harness",
        RunKinds.Workflow => "Pedido de engenharia",
        RunKinds.Assistant => "Pergunta ao assistente gerencial",
        RunKinds.Investigation => "Investigação de gestão",
        RunKinds.Apply => "Aplicação do Harness",
        RunKinds.Revert => "Reversão do Harness",
        _ => "Execução"
    };

    private static string StateLabel(string state) => state switch
    {
        RunStates.Completed => "concluída",
        RunStates.CompletedWithLimitations => "concluída com limitações",
        RunStates.Failed => "falhou",
        RunStates.RequiresIntervention => "exige a sua intervenção",
        RunStates.Cancelled => "cancelada",
        RunStates.AwaitingApproval => "esperando aprovação",
        RunStates.Running => "em andamento",
        RunStates.Validating => "validando",
        _ => state
    };

    private static string LevelOf(string state) => state switch
    {
        RunStates.Completed => "ok",
        RunStates.CompletedWithLimitations => "attention",
        RunStates.Failed or RunStates.RequiresIntervention => "problem",
        _ => "info"
    };

    private IReadOnlyList<TimelineEvent> DecisionEvents(string projectId) =>
        decisions!.Recorded(projectId, DateTimeOffset.MinValue, DateTimeOffset.MaxValue).Select(decision => new TimelineEvent(
            $"decision:{decision.Id}", decision.RecordedAt, TimelineKinds.Decision, "info", $"Decisão: {decision.Title}",
            decision.Facts.Count == 0 ? "Registrada sem fatos do momento." : $"Registrada com {Plural(decision.Facts.Count, "fato", "fatos")} do momento.",
            "decision", decision.Id)).ToList();

    private IReadOnlyList<TimelineEvent> RiskEvents(string projectId)
    {
        var result = new List<TimelineEvent>();
        foreach (var trail in management!.RiskHistory(projectId).Trails)
            foreach (var change in trail.Changes)
            {
                var (level, text) = RiskText(change);
                result.Add(new TimelineEvent(
                    $"risk:{trail.Id}:{change.At.UtcTicks}:{change.Kind}", change.At, TimelineKinds.Risk, level, $"Risco \"{trail.Title}\": {text}",
                    ItemsText(change), null, null));
            }
        return result;
    }

    private static (string Level, string Text) RiskText(RiskChange change) => change.Kind switch
    {
        RiskChangeKinds.Appeared => ("attention", $"apareceu, com gravidade {Severity(change.To)}"),
        RiskChangeKinds.Observed => ("info", $"já estava ativo no primeiro registro, com gravidade {Severity(change.To)}"),
        RiskChangeKinds.Raised => ("attention", $"subiu de gravidade {Severity(change.From)} para {Severity(change.To)}"),
        RiskChangeKinds.Lowered => ("ok", $"baixou de gravidade {Severity(change.From)} para {Severity(change.To)}"),
        RiskChangeKinds.ItemsChanged => ("info", "mudou os itens citados"),
        RiskChangeKinds.Resolved => ("ok", "deixou de existir"),
        _ => ("info", change.Kind)
    };

    private static string Severity(string? value) => value switch
    {
        "high" => "alta",
        "medium" => "média",
        "low" => "baixa",
        _ => value ?? "desconhecida"
    };

    // O histórico de risco guarda identificadores de itens, e a linha do tempo não precisa deles.
    private static string ItemsText(RiskChange change)
    {
        var parts = new List<string>();
        if (change.ItemsAdded.Count > 0) parts.Add(change.ItemsAdded.Count == 1 ? "1 item entrou" : $"{change.ItemsAdded.Count} itens entraram");
        if (change.ItemsRemoved.Count > 0) parts.Add(change.ItemsRemoved.Count == 1 ? "1 item saiu" : $"{change.ItemsRemoved.Count} itens saíram");
        return parts.Count == 0 ? "" : Capitalize(string.Join(", ", parts)) + ".";
    }

    private IReadOnlyList<TimelineEvent> SyncEvents(string projectId)
    {
        var result = new List<TimelineEvent>();
        var providers = integrations!.Connectors.Catalog().ToDictionary(descriptor => descriptor.Provider, descriptor => descriptor.Name, StringComparer.Ordinal);
        foreach (var integration in integrations.Projects.List(projectId))
            foreach (var sync in integrations.Sync.History(projectId, integration.Id).Where(item => item.FinishedAt is not null))
            {
                var provider = providers.GetValueOrDefault(integration.Provider, integration.Provider);
                var counts = $"{Plural(sync.Created, "novo", "novos")}, {Plural(sync.Updated, "atualizado", "atualizados")}, {sync.Unchanged} sem mudança";
                var (level, status) = sync.Status switch
                {
                    SyncStatus.Succeeded => ("ok", "concluída"),
                    SyncStatus.Partial => ("attention", "parcial"),
                    SyncStatus.Failed => ("problem", "falhou"),
                    _ => ("info", "em andamento")
                };
                result.Add(new TimelineEvent(
                    $"sync:{sync.Id}", sync.FinishedAt!.Value, TimelineKinds.Sync, level,
                    $"Sincronização de {provider} {integration.Scope.ResourceName}",
                    $"{Capitalize(status)}; {counts}.", null, null));
            }
        return result;
    }

    private static string Plural(int count, string one, string many) => $"{count} {(count == 1 ? one : many)}";

    private static string Capitalize(string text) =>
        text.Length == 0 ? text : char.ToUpper(text[0], CultureInfo.GetCultureInfo("pt-BR")) + text[1..];
}
