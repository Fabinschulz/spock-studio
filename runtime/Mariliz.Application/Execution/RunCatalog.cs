using System.Globalization;
using System.Text.Json.Nodes;
using Mariliz.Application.Protocol;
using Mariliz.Application.Workspace;

namespace Mariliz.Application.Execution;

/// <summary>
/// A central de execuções: lista e abre qualquer execução passada ou em andamento, de qualquer jornada, a partir dos
/// diários que já existem em disco. Não guarda nada novo e só lê: abrir o detalhe nunca dispara, reverte nem valida.
/// Só projetos autorizados aparecem, e um projeto esquecido some da lista mesmo com diário em disco.
/// </summary>
public sealed class RunCatalog(IRunStore store, WorkspaceAuthorizer workspace, RunTracker tracker, RunLinkService? links = null)
{
    public const int DefaultLimit = 25;
    public const int MaxLimit = 100;
    private const int MaxErrorText = 240;
    private const int MaxDetailEvents = 200;

    public static readonly IReadOnlyList<string> Kinds = [RunKinds.Apply, RunKinds.Revert, RunKinds.Validate, RunKinds.Workflow, RunKinds.Assistant];

    public static readonly IReadOnlyList<string> States =
    [
        RunStates.AwaitingApproval, RunStates.Running, RunStates.Validating, RunStates.Completed,
        RunStates.CompletedWithLimitations, RunStates.Failed, RunStates.RequiresIntervention, RunStates.Cancelled
    ];

    /// <summary>
    /// As execuções do projeto (ou de todos os autorizados), da mais recente para a mais antiga. A página é pedida com o
    /// <paramref name="cursor"/> que a anterior devolveu. <c>summary</c> conta o que está andando e o que espera
    /// aprovação em todo o recorte de projeto, sem olhar os filtros, para o indicador do menu.
    /// </summary>
    public JsonObject List(string? projectId, IReadOnlyList<string> kinds, IReadOnlyList<string> states, int? limit, string? cursor)
    {
        var page = Math.Clamp(limit ?? DefaultLimit, 1, MaxLimit);
        var after = ParseCursor(cursor);

        var projects = projectId is null
            ? workspace.Projects()
            : [Require(projectId)];

        var all = new List<(RunJournal Journal, string ProjectName)>();
        foreach (var project in projects)
            all.AddRange(store.ListJournals(project.ProjectId).Select(journal => (journal, project.DisplayName)));

        var ordered = all
            .OrderByDescending(entry => entry.Journal.StartedAt)
            .ThenByDescending(entry => entry.Journal.RunId, StringComparer.Ordinal)
            .ToList();

        var filtered = ordered.Where(entry =>
            (kinds.Count == 0 || kinds.Contains(entry.Journal.Kind)) && (states.Count == 0 || states.Contains(entry.Journal.State)));
        if (after is { } position)
            filtered = filtered.Where(entry => Precedes(entry.Journal, position));

        var window = filtered.Take(page + 1).ToList();
        var items = window.Take(page).Select(entry => Item(entry.Journal, entry.ProjectName)).ToArray();
        var next = window.Count > page ? CursorOf(window[page - 1].Journal) : null;

        return new JsonObject
        {
            ["runs"] = new JsonArray(items),
            ["nextCursor"] = next,
            ["summary"] = new JsonObject
            {
                ["total"] = all.Count,
                ["running"] = all.Count(entry => entry.Journal.State is RunStates.Running or RunStates.Validating),
                ["awaitingApproval"] = all.Count(entry => entry.Journal.State == RunStates.AwaitingApproval)
            }
        };
    }

    /// <summary>
    /// Tudo o que o diário diz de uma execução, sem o conteúdo que seria gravado (esse fica no diário e na aprovação).
    /// Os eventos são só os últimos, com tipo e número: o conteúdo de cada um é do <c>run.snapshot</c>.
    /// </summary>
    public JsonObject Detail(string projectId, string runId)
    {
        var project = Require(projectId);
        RunJournal? journal;
        try
        {
            journal = store.LoadJournal(project.ProjectId, runId);
        }
        catch (ArgumentException)
        {
            // O id vem do cliente e vira nome de pasta: um formato inválido é "não existe", não falha interna.
            journal = null;
        }

        if (journal is null) throw EngineErrors.RunNotFound();

        var events = store.LoadEvents(project.ProjectId, runId);
        var detail = Item(journal, project.DisplayName);
        detail["operations"] = new JsonArray(journal.Ops.Select(op => (JsonNode)new JsonObject
        {
            ["path"] = op.Path,
            ["op"] = op.Op,
            ["source"] = op.Source,
            ["status"] = op.Status,
            ["note"] = op.Note
        }).ToArray());
        detail["steps"] = new JsonArray(journal.Steps.Select(step => (JsonNode)new JsonObject
        {
            ["id"] = step.Id,
            ["title"] = step.Title,
            ["status"] = step.Status,
            ["detail"] = step.Detail
        }).ToArray());
        detail["notApplied"] = new JsonArray(journal.NotApplied.Select(text => (JsonNode)JsonValue.Create(text)!).ToArray());
        detail["pending"] = new JsonArray(journal.Pending.Select(text => (JsonNode)JsonValue.Create(text)!).ToArray());
        detail["evals"] = new JsonArray(journal.Evals.Select(eval => (JsonNode)new JsonObject
        {
            ["id"] = eval.Id,
            ["status"] = eval.Status,
            ["required"] = eval.Required
        }).ToArray());
        detail["actions"] = new JsonArray(journal.Actions.Select(action => (JsonNode)new JsonObject
        {
            ["id"] = action.Id,
            ["kind"] = action.Kind,
            ["summary"] = action.Summary,
            ["target"] = action.Target,
            ["status"] = action.Status
        }).ToArray());
        detail["model"] = journal.ModelUsage is { } usage
            ? new JsonObject { ["calls"] = usage.Calls, ["inputTokens"] = usage.InputTokens, ["outputTokens"] = usage.OutputTokens }
            : null;
        detail["summary"] = Summary(journal);
        // Sem o serviço de ligações (sessões sem central de integrações) a execução simplesmente não tem itens ligados.
        detail["links"] = links?.For(project.ProjectId, runId) ?? new JsonArray();
        detail["eventCount"] = events.Count;
        detail["events"] = new JsonArray(events.TakeLast(MaxDetailEvents).Select(evt => (JsonNode)new JsonObject
        {
            ["seq"] = evt["seq"]?.DeepClone(),
            ["type"] = evt["type"]?.DeepClone()
        }).ToArray());
        return detail;
    }

    /// <summary>
    /// O resumo de entrega no topo da execução: o que foi feito, o que foi validado e o que ficou de fora. O texto do pedido
    /// não entra: o diário nunca o guarda (ele fica só no checkpoint do runtime), e a interface diz o que o tipo de execução faz. São
    /// contagens do diário, sem interpretação: a interface as transforma em frases.
    /// </summary>
    private static JsonObject Summary(RunJournal journal)
    {
        int Applied(string op) => journal.Ops.Count(item => item.Op == op && item.Status == OpStatus.Applied);
        int Evals(params string[] statuses) =>
            journal.Evals.Count(item => statuses.Contains(item.Status, StringComparer.OrdinalIgnoreCase));

        return new JsonObject
        {
            ["done"] = new JsonObject
            {
                ["created"] = Applied(OpKinds.Create),
                ["updated"] = Applied(OpKinds.Update),
                ["removed"] = Applied(OpKinds.Delete),
                ["reverted"] = journal.Ops.Count(item => item.Status == OpStatus.Reverted),
                ["stepsCompleted"] = journal.Steps.Count(step => step.Status is StepStatus.Completed or StepStatus.CompletedWithConflicts),
                ["stepsTotal"] = journal.Steps.Count,
                ["actionsExecuted"] = journal.Actions.Count(action => action.Status == JournalAction.Executed),
                ["actionsTotal"] = journal.Actions.Count
            },
            // Sem nenhuma verificação registrada, "validado" não existe: a interface diz isso, em vez de um zero que pareceria falha.
            ["validated"] = journal.Evals.Count == 0
                ? null
                : new JsonObject
                {
                    ["total"] = journal.Evals.Count,
                    ["passed"] = Evals("Passed"),
                    ["failed"] = Evals("Failed"),
                    ["blocked"] = Evals("Blocked"),
                    ["notRun"] = Evals("NotRun", "not_run"),
                    ["requiredNotPassed"] = journal.Evals.Count(item => item.Required && !string.Equals(item.Status, "Passed", StringComparison.OrdinalIgnoreCase))
                },
            ["leftOut"] = new JsonObject
            {
                ["notApplied"] = journal.NotApplied.Count,
                ["pending"] = journal.Pending.Count,
                ["conflicts"] = journal.Ops.Count(item => item.Status == OpStatus.Conflict),
                ["actionsNotExecuted"] = journal.Actions.Count(action =>
                    action.Status is JournalAction.Skipped or JournalAction.Conflict or JournalAction.Failed)
            }
        };
    }

    public JsonObject Link(string projectId, string runId, string externalId, string? integrationId) =>
        new() { ["links"] = Links().Link(projectId, runId, externalId, integrationId) };

    public JsonObject Unlink(string projectId, string runId, string integrationId, string kind, string externalId) =>
        new() { ["links"] = Links().Unlink(projectId, runId, integrationId, kind, externalId) };

    /// <summary>Os itens do projeto que têm execuções ligadas. Só leitura.</summary>
    public JsonObject Linked(string projectId) => new() { ["items"] = Links().ForProject(projectId) };

    private RunLinkService Links() =>
        links ?? throw new EngineException("RUN_LINKS_UNAVAILABLE", "A ligação com itens de trabalho não está disponível nesta sessão do motor.");

    /// <summary>O projeto está autorizado: só então suas execuções podem ser vistas ou canceladas.</summary>
    public bool CanAccess(string projectId) => workspace.IsAuthorized(projectId);

    private AuthorizedProject Require(string projectId) =>
        workspace.Projects().FirstOrDefault(project => project.ProjectId == projectId) ?? throw EngineErrors.ProjectNotAuthorized();

    private JsonObject Item(RunJournal journal, string projectName) => new()
    {
        ["runId"] = journal.RunId,
        ["projectId"] = journal.ProjectId,
        ["projectName"] = projectName,
        ["kind"] = journal.Kind,
        ["state"] = journal.State,
        ["startedAt"] = journal.StartedAt.ToString("O", CultureInfo.InvariantCulture),
        ["finishedAt"] = journal.FinishedAt?.ToString("O", CultureInfo.InvariantCulture),
        // Andando neste processo agora, e não só "running" no diário: uma queda deixa o diário assim sem ninguém executando.
        ["live"] = tracker.Find(journal.RunId) is { } handle && !RunStates.IsTerminal(handle.State),
        ["changesApplied"] = journal.ChangesApplied && !journal.RolledBack,
        ["reverted"] = journal.RevertedByRunId is not null,
        ["canRevert"] = journal.CanBeReverted,
        ["revertsRunId"] = journal.RevertsRunId,
        ["error"] = Clip(journal.Error),
        ["operations"] = journal.Ops.Count,
        ["pending"] = journal.Pending.Count,
        ["notApplied"] = journal.NotApplied.Count,
        ["tokens"] = journal.ModelUsage is { } usage ? usage.InputTokens + usage.OutputTokens : null
    };

    private static string? Clip(string? text) =>
        string.IsNullOrWhiteSpace(text) ? null : text.Length <= MaxErrorText ? text : text[..MaxErrorText];

    private static string CursorOf(RunJournal journal) =>
        Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{journal.StartedAt.UtcTicks}|{journal.RunId}"));

    private static (long Ticks, string RunId)? ParseCursor(string? cursor)
    {
        if (string.IsNullOrEmpty(cursor)) return null;
        try
        {
            var parts = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(cursor)).Split('|');
            if (parts.Length == 2 && long.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var ticks) && parts[1].Length > 0)
                return (ticks, parts[1]);
        }
        catch (FormatException)
        {
        }

        throw new ParamException("'cursor' não é um cursor válido.");
    }

    /// <summary>Vem depois do cursor na ordem da lista (do mais novo ao mais antigo, com o id desempatando).</summary>
    private static bool Precedes(RunJournal journal, (long Ticks, string RunId) position) =>
        journal.StartedAt.UtcTicks < position.Ticks
        || (journal.StartedAt.UtcTicks == position.Ticks && string.CompareOrdinal(journal.RunId, position.RunId) < 0);
}
