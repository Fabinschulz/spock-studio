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
            var isConnection = record.Action.StartsWith("connection.", StringComparison.Ordinal)
                || record.Action.StartsWith("cloud.connection_", StringComparison.Ordinal) || record.Action == "cloud.permissions_checked";
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
            case "ide.context_read":
                yield return Entry("", AuditCategories.Context, IdeReadTitle(Text(d, "tool")), IdeReadDetail(d));
                break;
            case "ide.bootstrap":
                yield return Entry("", AuditCategories.Context, "Contexto de abertura entregue à IDE pelo hook de sessão",
                    $"Versão {Clean(Text(d, "contextVersion"))}: {Count(Count(d, "items"), "item", "itens")}, {Count(Count(d, "usedTokens"), "token", "tokens")}.");
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
            case "cloud.linked":
                yield return Entry("", AuditCategories.Access, $"Conexão cloud vinculada: {Clean(Text(d, "name"))}",
                    $"{Count(Count(d, "capabilities"), "leitura liberada", "leituras liberadas")}; agentes: {(Flag(d, "agentAccess") ? "liberados" : "fechados")}.");
                break;
            case "cloud.scope_changed":
                yield return Entry("", AuditCategories.Access, "Escopo de uma conexão cloud alterado",
                    $"{Count(d["capabilities"] is JsonArray caps ? caps.Count : 0, "leitura liberada", "leituras liberadas")}; agentes: de {(Flag(d, "agentAccessBefore") ? "liberados" : "fechados")} para {(Flag(d, "agentAccess") ? "liberados" : "fechados")}.");
                break;
            case "cloud.unlinked":
                yield return Entry("", AuditCategories.Access, "Conexão cloud desvinculada do projeto", "Nenhuma leitura dela vale mais para este projeto.");
                break;
            case "cloud.read":
                yield return Entry("", AuditCategories.Read, $"Leitura cloud: {Clean(Text(d, "capability"))}", CloudReadDetail(d));
                break;
            case "cloud.connection_created":
                yield return Entry("", AuditCategories.Credential, $"Conexão cloud criada: {Clean(Text(d, "name"))}", $"Provedor: {Clean(Text(d, "provider"))}. Nenhuma credencial foi guardada pelo Spock.");
                break;
            case "cloud.connection_validated":
                yield return Entry("", AuditCategories.Credential, $"Conexão cloud validada: {Clean(Text(d, "name"))}", $"Estado: {Clean(Text(d, "status"))}.");
                break;
            case "cloud.permissions_checked":
                yield return Entry("", AuditCategories.Credential, $"Permissões verificadas: {Clean(Text(d, "name"))}",
                    $"{Count(d, "allowed")} liberadas, {Count(d, "denied")} negadas, {Count(d, "failed")} sem resposta.");
                break;
            case "cloud.connection_removed":
                yield return Entry("", AuditCategories.Credential, $"Conexão cloud removida: {Clean(Text(d, "name"))}", "Só o registro saiu: a credencial nunca esteve com o Spock.");
                break;
            case "iac.runtime.pinned":
                yield return Entry("", AuditCategories.Change, $"{(Text(d, "engine") == "openTofu" ? "OpenTofu" : "Terraform")} {Clean(Text(d, "version"))} fixado no projeto",
                    (Flag(d, "automatic") ? "Fixado no primeiro uso, para que uma instalação nova não troque a versão. " : "Escolha feita na tela Infraestrutura. ")
                    + (Text(d, "source") == "managed" ? "Binário gerenciado pelo Spock." : "Binário instalado fora do Spock."));
                break;
            case "iac.validate":
                yield return Entry("", AuditCategories.Read, "Infraestrutura validada",
                    $"{Count(d, "folders")} pasta(s); {Count(d, "invalid")} inválida(s), {Count(d, "unformatted")} fora do formato. Nada mudou na nuvem nem no projeto.");
                break;
            case "iac.plan":
                yield return Entry("", AuditCategories.Read, $"Plano de infraestrutura gerado ({Clean(Text(d, "workspace"))})",
                    $"Criar {Count(d, "create")}, alterar {Count(d, "update")}, substituir {Count(d, "replace")}, destruir {Count(d, "delete")}; " +
                    $"{Count(d, "findings")} achado(s). Nada mudou na nuvem.");
                break;
            case "iac.drift":
                yield return Entry("", AuditCategories.Read, $"Drift de infraestrutura verificado ({Clean(Text(d, "workspace"))})",
                    Text(d, "status") == "failed" ? "A verificação falhou. Nada mudou na nuvem nem no state."
                    : $"{Count(d, "changed")} recurso(s) alterado(s) e {Count(d, "deleted")} apagado(s) fora da ferramenta; {Count(d, "unmanaged")} na conta fora do state. " +
                      "Nada mudou na nuvem nem no state.");
                break;
            case "iac.drift_schedule_changed":
                yield return Entry("", AuditCategories.Change, Flag(d, "enabled") ? $"Verificação de drift agendada a cada {Count(d, "intervalHours")} h" : "Verificação de drift agendada desligada",
                    $"Pasta {Clean(Text(d, "folder"))}, ambiente {Clean(Text(d, "workspace"))}. Roda enquanto o Spock está aberto e só lê a nuvem.");
                break;
            case "iac.drift_schedule_failed":
                yield return Entry("", AuditCategories.Read, "Verificação de drift agendada não rodou", $"Código {Clean(Text(d, "code"))}. Tenta de novo no próximo intervalo.");
                break;
            case "iac.bootstrap_written":
                yield return Entry("", AuditCategories.Change, $"Bootstrap do state gravado ({Clean(Text(d, "provider"))})",
                    $"{Count(d, "files")} arquivo(s) em {Clean(Text(d, "folder"))}. O bucket só é criado depois, pelo plano e apply aprovados.");
                break;
            case "iac.generated":
                yield return Entry("", AuditCategories.Change, $"Infraestrutura gerada no modelo ({Clean(Text(d, "provider"))})",
                    $"{Count(d, "files")} arquivo(s) em {Clean(Text(d, "folder"))}. Nada foi criado na nuvem: isso só acontece pelo plano e apply aprovados.");
                break;
            case "iac.architecture_drafted":
                yield return Entry("", AuditCategories.Read, "Desenho de infraestrutura proposto pela IA",
                    $"{Count(d, "attempts")} tentativa(s), {Count(d, "inputTokens")} tokens de entrada e {Count(d, "outputTokens")} de saída. Nada foi gravado.");
                break;
            case "iac.state_read":
                yield return Entry("", AuditCategories.Read, "State de infraestrutura consultado",
                    $"Pasta {Clean(Text(d, "folder"))}, ambiente {Clean(Text(d, "workspace"))}: {Count(d, "resources")} recurso(s). Só endereços, nenhum valor.");
                break;
            case "iac.state_unlocked":
                yield return Entry("", AuditCategories.Change, "Trava do state liberada (force-unlock)",
                    $"Pasta {Clean(Text(d, "folder"))}, ambiente {Clean(Text(d, "workspace"))}, trava {Clean(Text(d, "lockId"))}, com confirmação da pessoa.");
                break;
            case "iac.apply_approved":
                yield return Entry("", AuditCategories.Decision, $"Plano {Clean(Text(d, "planId"))} aprovado para aplicar",
                    $"Ambiente {Clean(Text(d, "workspace"))}; destruição {(Flag(d, "destroyConfirmed") ? "confirmada" : "não havia")}; " +
                    $"riscos {(Flag(d, "risksAccepted") ? "aceitos" : "não havia graves")}.");
                break;
            case "iac.apply":
                yield return Entry("", AuditCategories.Change, $"Plano {Clean(Text(d, "planId"))} aplicado na nuvem",
                    Flag(d, "verified") ? "Verificado: o plano seguinte não tem mudanças."
                    : Flag(d, "applied") ? $"Aplicado com divergência: {Count(d, "remaining")} mudança(s) ainda pendente(s)."
                    : $"Falhou; {Count(d, "remaining")} mudança(s) ficaram pendentes.");
                break;
            case "iac.runtime.unpinned":
                yield return Entry("", AuditCategories.Change, "Ferramenta de infraestrutura do projeto de volta à escolha automática", "Vale a versão compatível mais nova até o próximo uso.");
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

            if (journal.Kind is RunKinds.Apply or RunKinds.Revert or RunKinds.Security or RunKinds.Idea or RunKinds.Infrastructure)
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
            {
                var (title, detail) = Decision(journal, executed.Count);
                result.Add(Entry("a", approvedAt, AuditCategories.Change, title, detail));
            }
            foreach (var (action, index) in executed.Select((action, index) => (action, index)))
                result.Add(Entry($"x{index}", at, AuditCategories.Change, $"Ação de agente executada: {Clean(action.Summary)}", $"Destino: {Clean(action.Target)}."));

            if (journal.ModelUsage is { } usage && usage.Calls > 0)
                result.Add(Entry("m", at, AuditCategories.Model, "Uso de modelo de IA",
                    $"{Count(usage.Calls, "chamada", "chamadas")} ao provedor, {usage.InputTokens + usage.OutputTokens} tokens ({usage.InputTokens} de entrada, {usage.OutputTokens} de saída)."));
        }
        return result;
    }

    // A decisão da pessoa sobre as ações propostas: recusar tudo também passa pela aprovação aberta, e não pode virar "aprovadas".
    private static (string Title, string Detail) Decision(RunJournal journal, int executed)
    {
        var total = journal.Actions.Count;
        var approved = journal.ApprovedActions
            ?? journal.Actions.Count(action => action.Status is JournalAction.Approved or JournalAction.Executed or JournalAction.Conflict or JournalAction.Failed);
        return approved == 0
            ? ("Ações propostas por agentes recusadas", $"Nenhuma das {Count(total, "ação proposta", "ações propostas")} foi aprovada; nada foi executado.")
            : ("Ações propostas por agentes aprovadas",
                $"{(approved == total ? Count(total, "ação aprovada", "ações aprovadas") : $"{approved} de {total} ações aprovadas")}; {Count(executed, "executada", "executadas")}.");
    }

    private const int ShownPaths = 8;

    private static string IdeReadTitle(string tool) => tool switch
    {
        "search_project" => "Trechos do índice entregues à IDE",
        "project_briefing" => "Resumo do projeto entregue à IDE",
        "decisions" => "Decisões registradas entregues à IDE",
        _ => $"Contexto entregue à IDE: {Clean(tool)}"
    };

    // A IDE diz o próprio nome no início da conversa MCP; a trilha repete o que ela disse, sem conferir.
    private static string IdeReadDetail(JsonObject d)
    {
        var tool = Text(d, "tool");
        var paths = d["paths"] is JsonArray list ? list.OfType<JsonValue>().Select(item => item.TryGetValue<string>(out var path) ? Clean(path) : "").Where(path => path.Length > 0).ToList() : [];
        var files = Count(d, "files");
        var what = tool switch
        {
            "search_project" when paths.Count > 0 =>
                $"{Count(Count(d, "items"), "trecho", "trechos")} de {Count(files, "arquivo ou decisão", "arquivos ou decisões")}: {string.Join(", ", paths.Take(ShownPaths))}{(files > ShownPaths ? $" e mais {files - ShownPaths}" : "")}.",
            "decisions" => $"{Count(Count(d, "items"), "decisão", "decisões")}.",
            "project_briefing" => "Tecnologias, comandos, estado do Harness e decisões recentes.",
            _ => $"{Count(Count(d, "items"), "item", "itens")}."
        };
        var client = Clean(Text(d, "client"));
        return client.Length > 0 ? $"{what} Cliente MCP informado: {client}." : what;
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

    private static string CloudReadDetail(JsonObject d)
    {
        var who = Text(d, "caller") == "agent" ? $"agente{(Text(d, "client") is { Length: > 0 } client ? $" ({Clean(client)})" : "")}" : "pessoa";
        var where = Text(d, "region") is { Length: > 0 } region ? $", região {Clean(region)}" : "";
        var outcome = Text(d, "outcome") is "ok" or "" ? Count(Count(d, "rows"), "item lido", "itens lidos") + (Flag(d, "truncated") ? " (havia mais)" : "") : $"recusada: {Clean(Text(d, "outcome"))}";
        return $"{Clean(Text(d, "provider"))}{where}, pedida por {who}; {outcome}.";
    }

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
