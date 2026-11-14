using System.Text.Json.Nodes;
using Mariliz.Application.Execution;
using Mariliz.Application.Integrations;
using Mariliz.Application.Ports;
using Mariliz.Application.Workspace;

namespace Mariliz.Application.Audit;

public static class AuditCategories
{
    public const string Access = "access";
    public const string Context = "context";
    public const string Credential = "credential";
    public const string Read = "read";
    public const string Decision = "decision";
    public const string Change = "change";
    public const string Model = "model";

    public static readonly IReadOnlyList<(string Id, string Label)> All =
    [
        (Access, "Acesso a fontes"),
        (Context, "Contexto de IA"),
        (Credential, "Conexões"),
        (Read, "Leituras de fontes"),
        (Decision, "Decisões"),
        (Change, "Mudanças no projeto"),
        (Model, "Uso de modelo")
    ];
}

public static class AuditSources
{
    public const string Journal = "journal";
    public const string Audit = "audit";
}

// A linha do arquivo de auditoria nunca muda de lugar (o arquivo só cresce). Uma entrada tem uma das duas referências.
public sealed record AuditEntry(
    string Id, DateTimeOffset At, string Category, string Title, string Detail, string Source, string? RunId, int? AuditLine);

public sealed record AuditSource(string Source, string Label, int Count, bool Available);

// A contagem é da trilha inteira, sem olhar o filtro.
public sealed record AuditCategory(string Id, string Label, int Count);

/// <param name="NextBefore">Passe como <c>before</c> para a página seguinte. Nulo no fim.</param>
public sealed record ProjectAuditTrail(
    string ProjectId,
    DateTimeOffset GeneratedAt,
    IReadOnlyList<AuditEntry> Entries,
    DateTimeOffset? NextBefore,
    IReadOnlyList<AuditSource> Sources,
    IReadOnlyList<AuditCategory> Categories);

// Junta o arquivo de auditoria e o diário das execuções sem copiar nada para um terceiro lugar. Nunca mostra credencial, texto de pedido, título de item nem o texto de uma decisão. Não diz quem: o produto ainda não tem identidade de pessoa.
public sealed class ProjectAuditService(WorkspaceAuthorizer workspace, IClock clock, IRunStore store, IAuditReader? audit = null, IntegrationHub? integrations = null)
{
    public const int DefaultLimit = 50;
    public const int MaxLimit = 200;

    public ProjectAuditTrail Read(string projectId, IReadOnlyList<string> categories, int? limit, DateTimeOffset? before)
    {
        if (!workspace.IsAuthorized(projectId)) throw EngineErrors.ProjectNotAuthorized();
        var known = AuditCategories.All.Select(category => category.Id).ToHashSet(StringComparer.Ordinal);
        if (categories.Any(category => !known.Contains(category))) throw new EngineException("AUDIT_CATEGORY_UNKNOWN", "Essa categoria de auditoria não existe.");
        var page = Math.Clamp(limit ?? DefaultLimit, 1, MaxLimit);

        var all = new List<AuditEntry>();
        var sources = new List<AuditSource>();
        Collect(AuditSources.Audit, "Arquivo de auditoria", audit is null ? null : () => FromAuditFile(projectId), all, sources);
        Collect(AuditSources.Journal, "Diário das execuções", () => FromJournals(projectId), all, sources);

        var visible = all
            .Where(entry => categories.Count == 0 || categories.Contains(entry.Category))
            .Where(entry => before is null || entry.At < before)
            .OrderByDescending(entry => entry.At)
            .ThenBy(entry => entry.Source, StringComparer.Ordinal)
            .ThenByDescending(entry => entry.AuditLine ?? 0)
            .ThenBy(entry => entry.Id, StringComparer.Ordinal)
            .ToList();

        var entries = visible.Take(page).ToList();
        // Quem tem exatamente o instante da fronteira entra nesta página: um empate não faz uma entrada sumir entre duas páginas.
        if (visible.Count > page) entries.AddRange(visible.Skip(page).TakeWhile(entry => entry.At == entries[^1].At));
        var next = visible.Count > entries.Count ? entries[^1].At : (DateTimeOffset?)null;
        var counts = AuditCategories.All.Select(category => new AuditCategory(category.Id, category.Label, all.Count(entry => entry.Category == category.Id))).ToList();
        return new ProjectAuditTrail(projectId, clock.UtcNow, entries, next, sources, counts);
    }

    private static void Collect(string source, string label, Func<IReadOnlyList<AuditEntry>>? read, List<AuditEntry> all, List<AuditSource> sources)
    {
        if (read is null) return;
        try
        {
            var entries = read();
            all.AddRange(entries);
            sources.Add(new AuditSource(source, label, entries.Count, true));
        }
        catch (Exception ex) when (ex is EngineException or IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            sources.Add(new AuditSource(source, label, 0, false));
        }
    }

    private IReadOnlyList<AuditEntry> FromAuditFile(string projectId)
    {
        var records = audit!.ReadAll();
        // Os eventos de conexão não carregam o projeto: valem para este projeto as conexões que ele já usou ou usa.
        var connectionIds = records
            .Where(record => ProjectOf(record) == projectId && record.Detail["connectionId"] is JsonValue)
            .Select(record => Text(record.Detail, "connectionId"))
            .Concat(integrations?.Projects.List(projectId).Select(integration => integration.ConnectionId) ?? [])
            .ToHashSet(StringComparer.Ordinal);

        var result = new List<AuditEntry>();
        // O estado de cada vínculo, na ordem do arquivo, para dizer "de fechada para aberta" e não só "aberta".
        var state = new Dictionary<string, (bool Feeds, string Kinds)>(StringComparer.Ordinal);
        // O recurso de cada vínculo: pelo arquivo (vínculo, recorte, desvínculo) ou, para um vínculo de hoje, pelo que ele é agora.
        var names = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var integration in integrations?.Projects.List(projectId) ?? []) names[integration.Id] = Clean(integration.Scope.ResourceName);
        foreach (var record in records.Where(record => ProjectOf(record) == projectId && record.Detail["resource"] is JsonValue))
            names[Text(record.Detail, "integrationId")] = Clean(Text(record.Detail, "resource"));

        foreach (var record in records.OrderBy(record => record.Line))
        {
            var inProject = ProjectOf(record) == projectId;
            var isConnection = record.Action.StartsWith("connection.", StringComparison.Ordinal);
            if (!inProject && !(isConnection && connectionIds.Contains(Text(record.Detail, "connectionId")))) continue;
            result.AddRange(Describe(record, state, names));
        }
        return result;
    }

    private static string? ProjectOf(AuditRecord record) => record.Detail["projectId"] is JsonValue ? Text(record.Detail, "projectId") : null;

    private static IEnumerable<AuditEntry> Describe(AuditRecord record, Dictionary<string, (bool Feeds, string Kinds)> state, Dictionary<string, string> names)
    {
        var d = record.Detail;
        var integrationId = Text(d, "integrationId");
        var resource = Clean(Text(d, "resource"));
        AuditEntry Entry(string suffix, string category, string title, string detail) =>
            new($"audit:{record.Line}{suffix}", record.At, category, title, detail, AuditSources.Audit, null, record.Line);

        switch (record.Action)
        {
            case "integration.linked":
            {
                var kinds = KindsText(d);
                var feeds = Flag(d, "feedsKnowledge");
                state[integrationId] = (feeds, kinds);
                yield return Entry("", AuditCategories.Access, $"Fonte vinculada ao projeto: {resource}", $"Dados lidos: {kinds}. Política de contexto de IA: {Policy(feeds)}.");
                break;
            }
            case "integration.scope_changed":
            {
                var kinds = KindsText(d);
                var feeds = Flag(d, "feedsKnowledge");
                var known = state.TryGetValue(integrationId, out var previous);
                state[integrationId] = (feeds, kinds);
                var changedPolicy = !known || previous.Feeds != feeds;
                var changedKinds = !known || previous.Kinds != kinds;
                if (known && changedPolicy)
                    yield return Entry("p", AuditCategories.Context, $"Política de contexto de IA de {resource}: de {Policy(previous.Feeds)} para {Policy(feeds)}",
                        feeds ? "Os itens sincronizados passam a poder virar contexto dos agentes." : "Os itens sincronizados deixam de poder virar contexto dos agentes.");
                if (known && changedKinds)
                    yield return Entry("k", AuditCategories.Access, $"Recorte de {resource} alterado: de {previous.Kinds} para {kinds}",
                        "O que saiu do recorte foi apagado do que já estava sincronizado.");
                if (!known)
                    yield return Entry("", AuditCategories.Access, $"Recorte ou política de {resource} alterados", $"Dados lidos: {kinds}. Política de contexto de IA: {Policy(feeds)}.");
                else if (!changedPolicy && !changedKinds)
                    yield return Entry("", AuditCategories.Access, $"Recorte e política de {resource} salvos sem mudança", $"Dados lidos: {kinds}. Política de contexto de IA: {Policy(feeds)}.");
                break;
            }
            case "integration.unlinked":
                state.Remove(integrationId);
                yield return Entry("", AuditCategories.Access, $"Fonte desvinculada do projeto: {resource}", "O que estava sincronizado dela foi apagado deste computador.");
                break;
            case "integration.synced":
                yield return Entry("", AuditCategories.Read, $"Leitura de {names.GetValueOrDefault(integrationId) ?? "uma fonte vinculada"}", $"{SyncStatus(Text(d, "status"))}; {Count(Count(d, "fetched"), "item lido", "itens lidos")}.");
                break;
            case "integration.sync_listener_failed":
                yield return Entry("", AuditCategories.Read, "Um ouvinte do fim da sincronização falhou", "A sincronização foi gravada; o erro ficou no tipo registrado.");
                break;
            case "decision.recorded":
                yield return Entry("", AuditCategories.Decision, "Decisão gerencial registrada",
                    $"{Count(Count(d, "facts"), "fato do momento", "fatos do momento")} e {Count(Count(d, "runs"), "execução relacionada", "execuções relacionadas")}." + (Flag(d, "redacted") ? " Houve redação de segredo no texto." : ""));
                break;
            case "connection.created":
                yield return Entry("", AuditCategories.Credential, $"Conexão criada: {Clean(Text(d, "name"))}", $"Provedor: {Clean(Text(d, "provider"))}. Conexão usada por este projeto.");
                break;
            case "connection.credential_updated":
                yield return Entry("", AuditCategories.Credential, $"Credencial atualizada: {Clean(Text(d, "name"))}", "A nova credencial foi conferida com o provedor antes de substituir a antiga.");
                break;
            case "connection.disconnected":
                yield return Entry("", AuditCategories.Credential, $"Conexão desconectada: {Clean(Text(d, "name"))}", "A credencial foi apagada deste computador.");
                break;
            case "connection.removed":
                yield return Entry("", AuditCategories.Credential, $"Conexão removida: {Clean(Text(d, "name"))}", "A conexão e a credencial saíram deste computador.");
                break;
            default:
                yield return Entry("", AuditCategories.Change, $"Operação registrada: {Clean(record.Action)}", "");
                break;
        }
    }

    private IReadOnlyList<AuditEntry> FromJournals(string projectId)
    {
        var result = new List<AuditEntry>();
        foreach (var journal in store.ListJournals(projectId))
        {
            var at = journal.FinishedAt ?? journal.StartedAt;
            AuditEntry Entry(string suffix, DateTimeOffset when, string category, string title, string detail) =>
                new($"journal:{journal.RunId}{suffix}", when, category, title, detail, AuditSources.Journal, journal.RunId, null);

            if (journal.Kind is RunKinds.Apply or RunKinds.Revert or RunKinds.Security or RunKinds.Idea)
            {
                var applied = journal.Ops.Count(op => op.Status == OpStatus.Applied);
                var reverted = journal.Ops.Count(op => op.Status == OpStatus.Reverted);
                if (journal.Kind == RunKinds.Idea && applied > 0)
                    result.Add(Entry("", at, AuditCategories.Change, "Documentos da ideia gravados",
                        $"{Count(applied, "arquivo gravado", "arquivos gravados")} em .mariliz/arquitetura depois da aprovação."));
                else if (journal.Kind == RunKinds.Security && applied > 0)
                    result.Add(Entry("", at, AuditCategories.Change, "Correção de segurança aplicada",
                        $"{Count(applied, "arquivo gravado", "arquivos gravados")} depois da aprovação."));
                else if (journal.Kind == RunKinds.Apply && applied > 0)
                    result.Add(Entry("", at, AuditCategories.Change, journal.RolledBack ? "Aplicação do Harness desfeita na hora" : "Harness aplicado ao projeto",
                        $"{Count(applied, "arquivo gravado", "arquivos gravados")}{(journal.RolledBack ? ", e tudo foi desfeito porque a aplicação falhou no meio" : "")}."));
                else if (journal.Kind == RunKinds.Revert && reverted > 0)
                    result.Add(Entry("", at, AuditCategories.Change, "Aplicação do Harness revertida", $"{Count(reverted, "arquivo restaurado", "arquivos restaurados")} ao que era antes."));
            }

            // A aprovação que liga as ações propostas por agentes ao que foi executado: o que um agente fez no projeto.
            var executed = journal.Actions.Where(action => action.Status == JournalAction.Executed).ToList();
            if (journal.ApprovedAt is { } approvedAt && journal.Actions.Count > 0)
                result.Add(Entry("a", approvedAt, AuditCategories.Change, "Ações propostas por agentes aprovadas",
                    $"{Count(journal.Actions.Count, "ação", "ações")} no conjunto aprovado; {Count(executed.Count, "executada", "executadas")}."));
            foreach (var (action, index) in executed.Select((action, index) => (action, index)))
                result.Add(Entry($"x{index}", at, AuditCategories.Change, $"Ação de agente executada: {Clean(action.Summary)}", $"Destino: {Clean(action.Target)}."));

            if (journal.ModelUsage is { } usage && usage.Calls > 0)
                result.Add(Entry("m", at, AuditCategories.Model, "Uso de modelo de IA",
                    $"{Count(usage.Calls, "chamada", "chamadas")} ao provedor, {usage.InputTokens + usage.OutputTokens} tokens ({usage.InputTokens} de entrada, {usage.OutputTokens} de saída)."));
        }
        return result;
    }

    private static string Policy(bool feeds) => feeds ? "aberta" : "fechada";

    private static string KindsText(JsonObject d) =>
        d["dataKinds"] is JsonArray kinds && kinds.Count > 0
            ? string.Join(", ", kinds.OfType<JsonValue>().Select(kind => kind.TryGetValue<string>(out var text) ? KindLabel(text) : "?"))
            : "nenhum";

    private static string KindLabel(string kind) => kind switch { DataKinds.Issues => "issues", DataKinds.PullRequests => "pull requests", _ => Clean(kind) };

    private static string SyncStatus(string status) => status switch
    {
        "succeeded" or "Succeeded" => "Concluída",
        "partial" or "Partial" => "Parcial",
        "failed" or "Failed" => "Falhou",
        _ => "Terminou"
    };

    private static string Count(int value, string one, string many) => $"{value} {(value == 1 ? one : many)}";

    private static int Count(JsonObject d, string name) => d[name] is JsonValue value && value.TryGetValue<int>(out var number) ? number : 0;

    private static bool Flag(JsonObject d, string name) => d[name] is JsonValue value && value.TryGetValue<bool>(out var flag) && flag;

    private static string Text(JsonObject d, string name) => d[name] is JsonValue value && value.TryGetValue<string>(out var text) ? text : "";

    private static string Clean(string text)
    {
        var flat = string.Join(' ', new string(text.Select(ch => char.IsControl(ch) ? ' ' : ch).ToArray()).Split(' ', StringSplitOptions.RemoveEmptyEntries));
        return flat.Length > 160 ? flat[..160].TrimEnd() + "…" : flat;
    }
}
