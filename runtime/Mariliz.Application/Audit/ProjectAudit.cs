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
            if (!inProject && !(AuditActions.IsConnection(record.Action) && connectionIds.Contains(Text(record.Detail, "connectionId")))) continue;
            result.AddRange(Describe(record, state, names));
        }
        return result;
    }

    private static string? ProjectOf(AuditRecord record) => record.Detail["projectId"] is JsonValue ? Text(record.Detail, "projectId") : null;

    internal static IEnumerable<AuditEntry> Describe(AuditRecord record, Dictionary<string, (bool Feeds, string Kinds)> state, Dictionary<string, string> names)
    {
        var d = record.Detail;
        var integrationId = Text(d, "integrationId");
        var resource = Clean(Text(d, "resource"));
        AuditEntry Entry(string suffix, string category, string title, string detail) =>
            new($"audit:{record.Line}{suffix}", record.At, category, title, detail, AuditSources.Audit, null, record.Line);

        switch (record.Action)
        {
            case AuditActions.IntegrationLinked:
            {
                var kinds = KindsText(d);
                var feeds = Flag(d, "feedsKnowledge");
                state[integrationId] = (feeds, kinds);
                yield return Entry("", AuditCategories.Access, $"Fonte vinculada ao projeto: {resource}", $"Dados lidos: {kinds}. Política de contexto de IA: {Policy(feeds)}.");
                break;
            }
            case AuditActions.IntegrationScopeChanged:
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
            case AuditActions.IntegrationUnlinked:
                state.Remove(integrationId);
                yield return Entry("", AuditCategories.Access, $"Fonte desvinculada do projeto: {resource}", "O que estava sincronizado dela foi apagado deste computador.");
                break;
            case AuditActions.IntegrationSynced:
                yield return Entry("", AuditCategories.Read, $"Leitura de {names.GetValueOrDefault(integrationId) ?? "uma fonte vinculada"}", $"{SyncStatus(Text(d, "status"))}; {Count(Count(d, "fetched"), "item lido", "itens lidos")}.");
                break;
            case AuditActions.IntegrationSyncListenerFailed:
                yield return Entry("", AuditCategories.Read, "Um ouvinte do fim da sincronização falhou", "A sincronização foi gravada; o erro ficou no tipo registrado.");
                break;
            case AuditActions.DecisionRecorded:
                yield return Entry("", AuditCategories.Decision, "Decisão gerencial registrada",
                    $"{Count(Count(d, "facts"), "fato do momento", "fatos do momento")} e {Count(Count(d, "runs"), "execução relacionada", "execuções relacionadas")}." + (Flag(d, "redacted") ? " Houve redação de segredo no texto." : ""));
                break;
            case AuditActions.IdeContextRead:
                yield return Entry("", AuditCategories.Context, IdeReadTitle(Text(d, "tool")), IdeReadDetail(d));
                break;
            case AuditActions.IdeBootstrap:
                yield return Entry("", AuditCategories.Context, "Contexto de abertura entregue à IDE pelo hook de sessão",
                    $"Versão {Clean(Text(d, "contextVersion"))}: {Count(Count(d, "items"), "item", "itens")}, {Count(Count(d, "usedTokens"), "token", "tokens")}.");
                break;
            case AuditActions.IdePrompt:
                yield return Entry("", AuditCategories.Context, "Aviso entregue à IDE pelo hook por prompt", PromptSignalsDetail(d));
                break;
            case AuditActions.IdeBoot:
                yield return Entry("", AuditCategories.Context, "Boot pedido pela IDE", IdeBootDetail(d));
                break;
            case AuditActions.ConnectionCreated:
                yield return Entry("", AuditCategories.Credential, $"Conexão criada: {Clean(Text(d, "name"))}", $"Provedor: {Clean(Text(d, "provider"))}. Conexão usada por este projeto.");
                break;
            case AuditActions.ConnectionCredentialUpdated:
                yield return Entry("", AuditCategories.Credential, $"Credencial atualizada: {Clean(Text(d, "name"))}", "A nova credencial foi conferida com o provedor antes de substituir a antiga.");
                break;
            case AuditActions.ConnectionDisconnected:
                yield return Entry("", AuditCategories.Credential, $"Conexão desconectada: {Clean(Text(d, "name"))}", "A credencial foi apagada deste computador.");
                break;
            case AuditActions.ConnectionRemoved:
                yield return Entry("", AuditCategories.Credential, $"Conexão removida: {Clean(Text(d, "name"))}", "A conexão e a credencial saíram deste computador.");
                break;
            case AuditActions.CloudLinked:
                yield return Entry("", AuditCategories.Access, $"Conexão cloud vinculada: {Clean(Text(d, "name"))}",
                    $"{Count(Count(d, "capabilities"), "leitura liberada", "leituras liberadas")}; agentes: {(Flag(d, "agentAccess") ? "liberados" : "fechados")}.");
                break;
            case AuditActions.CloudScopeChanged:
                yield return Entry("", AuditCategories.Access, "Escopo de uma conexão cloud alterado",
                    $"{Count(d["capabilities"] is JsonArray caps ? caps.Count : 0, "leitura liberada", "leituras liberadas")}; agentes: de {(Flag(d, "agentAccessBefore") ? "liberados" : "fechados")} para {(Flag(d, "agentAccess") ? "liberados" : "fechados")}.");
                break;
            case AuditActions.CloudUnlinked:
                yield return Entry("", AuditCategories.Access, "Conexão cloud desvinculada do projeto", "Nenhuma leitura dela vale mais para este projeto.");
                break;
            case AuditActions.CloudRead:
                yield return Entry("", AuditCategories.Read, $"Leitura cloud: {Clean(Text(d, "capability"))}", CloudReadDetail(d));
                break;
            case AuditActions.CloudConnectionCreated:
                yield return Entry("", AuditCategories.Credential, $"Conexão cloud criada: {Clean(Text(d, "name"))}", $"Provedor: {Clean(Text(d, "provider"))}. Nenhuma credencial foi guardada pelo Spock.");
                break;
            case AuditActions.CloudConnectionValidated:
                yield return Entry("", AuditCategories.Credential, $"Conexão cloud validada: {Clean(Text(d, "name"))}", $"Estado: {Clean(Text(d, "status"))}.");
                break;
            case AuditActions.CloudPermissionsChecked:
                yield return Entry("", AuditCategories.Credential, $"Permissões verificadas: {Clean(Text(d, "name"))}",
                    $"{Count(d, "allowed")} liberadas, {Count(d, "denied")} negadas, {Count(d, "failed")} sem resposta.");
                break;
            case AuditActions.CloudConnectionRemoved:
                yield return Entry("", AuditCategories.Credential, $"Conexão cloud removida: {Clean(Text(d, "name"))}", "Só o registro saiu: a credencial nunca esteve com o Spock.");
                break;
            case AuditActions.IacRuntimePinned:
                yield return Entry("", AuditCategories.Change, $"{(Text(d, "engine") == "openTofu" ? "OpenTofu" : "Terraform")} {Clean(Text(d, "version"))} fixado no projeto",
                    (Flag(d, "automatic") ? "Fixado no primeiro uso, para que uma instalação nova não troque a versão. " : "Escolha feita na tela Infraestrutura. ")
                    + (Text(d, "source") == "managed" ? "Binário gerenciado pelo Spock." : "Binário instalado fora do Spock."));
                break;
            case AuditActions.IacValidate:
                yield return Entry("", AuditCategories.Read, "Infraestrutura validada",
                    $"{Count(d, "folders")} pasta(s); {Count(d, "invalid")} inválida(s), {Count(d, "unformatted")} fora do formato. Nada mudou na nuvem nem no projeto.");
                break;
            case AuditActions.IacPlan:
                yield return Entry("", AuditCategories.Read, $"Plano de infraestrutura gerado ({Clean(Text(d, "workspace"))})",
                    $"Criar {Count(d, "create")}, alterar {Count(d, "update")}, substituir {Count(d, "replace")}, destruir {Count(d, "delete")}; " +
                    $"{Count(d, "findings")} achado(s). Nada mudou na nuvem.");
                break;
            case AuditActions.IacDrift:
                yield return Entry("", AuditCategories.Read, $"Drift de infraestrutura verificado ({Clean(Text(d, "workspace"))})",
                    Text(d, "status") == "failed" ? "A verificação falhou. Nada mudou na nuvem nem no state."
                    : $"{Count(d, "changed")} recurso(s) alterado(s) e {Count(d, "deleted")} apagado(s) fora da ferramenta; {Count(d, "unmanaged")} na conta fora do state. " +
                      "Nada mudou na nuvem nem no state.");
                break;
            case AuditActions.IacDriftScheduleChanged:
                yield return Entry("", AuditCategories.Change, Flag(d, "enabled") ? $"Verificação de drift agendada a cada {Count(d, "intervalHours")} h" : "Verificação de drift agendada desligada",
                    $"Pasta {Clean(Text(d, "folder"))}, ambiente {Clean(Text(d, "workspace"))}. Roda enquanto o Spock está aberto e só lê a nuvem.");
                break;
            case AuditActions.IacDriftScheduleFailed:
                yield return Entry("", AuditCategories.Read, "Verificação de drift agendada não rodou", $"Código {Clean(Text(d, "code"))}. Tenta de novo no próximo intervalo.");
                break;
            case AuditActions.IacBootstrapWritten:
                yield return Entry("", AuditCategories.Change, $"Bootstrap do state gravado ({Clean(Text(d, "provider"))})",
                    $"{Count(d, "files")} arquivo(s) em {Clean(Text(d, "folder"))}. O bucket só é criado depois, pelo plano e apply aprovados.");
                break;
            case AuditActions.IacGenerated:
                yield return Entry("", AuditCategories.Change, $"Infraestrutura gerada no modelo ({Clean(Text(d, "provider"))})",
                    $"{Count(d, "files")} arquivo(s) em {Clean(Text(d, "folder"))}. Nada foi criado na nuvem: isso só acontece pelo plano e apply aprovados.");
                break;
            case AuditActions.IacArchitectureDrafted:
                yield return Entry("", AuditCategories.Read, "Desenho de infraestrutura proposto pela IA",
                    $"{Count(d, "attempts")} tentativa(s), {Count(d, "inputTokens")} tokens de entrada e {Count(d, "outputTokens")} de saída. Nada foi gravado.");
                break;
            case AuditActions.IacStateRead:
                yield return Entry("", AuditCategories.Read, "State de infraestrutura consultado",
                    $"Pasta {Clean(Text(d, "folder"))}, ambiente {Clean(Text(d, "workspace"))}: {Count(d, "resources")} recurso(s). Só endereços, nenhum valor.");
                break;
            case AuditActions.IacStateUnlocked:
                yield return Entry("", AuditCategories.Change, "Trava do state liberada (force-unlock)",
                    $"Pasta {Clean(Text(d, "folder"))}, ambiente {Clean(Text(d, "workspace"))}, trava {Clean(Text(d, "lockId"))}, com confirmação da pessoa.");
                break;
            case AuditActions.IacApplyApproved:
                yield return Entry("", AuditCategories.Decision, $"Plano {Clean(Text(d, "planId"))} aprovado para aplicar",
                    $"Ambiente {Clean(Text(d, "workspace"))}; destruição {(Flag(d, "destroyConfirmed") ? "confirmada" : "não havia")}; " +
                    $"riscos {(Flag(d, "risksAccepted") ? "aceitos" : "não havia graves")}.");
                break;
            case AuditActions.IacApply:
                yield return Entry("", AuditCategories.Change, $"Plano {Clean(Text(d, "planId"))} aplicado na nuvem",
                    Flag(d, "verified") ? "Verificado: o plano seguinte não tem mudanças."
                    : Flag(d, "applied") ? $"Aplicado com divergência: {Count(d, "remaining")} mudança(s) ainda pendente(s)."
                    : $"Falhou; {Count(d, "remaining")} mudança(s) ficaram pendentes.");
                break;
            case AuditActions.IacRuntimeUnpinned:
                yield return Entry("", AuditCategories.Change, "Ferramenta de infraestrutura do projeto de volta à escolha automática", "Vale a versão compatível mais nova até o próximo uso.");
                break;
            case AuditActions.IntegrationSyncBookkeepingFailed:
                yield return Entry("", AuditCategories.Read, "O registro do fim de uma sincronização falhou", "Os itens lidos ficaram gravados; o erro ficou no tipo registrado.");
                break;
            case AuditActions.RunLinked:
                yield return Entry("", AuditCategories.Change, $"Execução ligada a {ItemText(d)}", $"Execução {Clean(Text(d, "runId"))}. O vínculo fica só neste computador.");
                break;
            case AuditActions.RunUnlinked:
                yield return Entry("", AuditCategories.Change, $"Execução desligada de {ItemText(d)}", $"Execução {Clean(Text(d, "runId"))}.");
                break;
            case AuditActions.ProfileRefreshed:
                yield return Entry("", AuditCategories.Read, "Perfil de tecnologias do projeto atualizado",
                    $"{Count(Count(d, "technologies"), "tecnologia", "tecnologias")}; {Count(Count(d, "changes"), "mudança", "mudanças")} desde o perfil anterior.");
                break;
            case AuditActions.AssistantAnswered:
                yield return Entry("", AuditCategories.Model, "Pergunta de gestão respondida pela IA",
                    $"{Count(Count(d, "facts"), "fato consultado", "fatos consultados")}, {Count(d, "cited")} citado(s) e {Count(d, "rejected")} recusado(s) na conferência; " +
                    $"{Count(d, "inputTokens")} tokens de entrada e {Count(d, "outputTokens")} de saída.");
                break;
            case AuditActions.InvestigationAnswered:
                yield return Entry("", AuditCategories.Model, "Investigação de gestão concluída pela IA",
                    $"{Count(Count(d, "rounds"), "rodada", "rodadas")}, {Count(Count(d, "consulted"), "evidência citada", "evidências citadas")} e {Count(d, "rejected")} recusada(s); " +
                    $"{Count(Count(d, "modelCalls"), "chamada", "chamadas")} ao modelo, {Count(d, "inputTokens")} tokens de entrada e {Count(d, "outputTokens")} de saída.");
                break;
            case AuditActions.BootStarted:
                yield return Entry("", AuditCategories.Context, "Boot iniciado", $"{BootLabel(Text(d, "mode"))} a partir de {Clean(Text(d, "origin"))}. O texto do pedido não é registrado.");
                break;
            case AuditActions.BootIntentModel:
                yield return Entry("", AuditCategories.Model, "Pedido interpretado com ajuda da IA",
                    $"{(Text(d, "stage") == "failed" ? "A resposta do modelo não serviu" : "O modelo respondeu")}; {Count(Count(d, "proposed"), "capacidade proposta", "capacidades propostas")}; " +
                    $"{Count(d, "inputTokens")} tokens de entrada e {Count(d, "outputTokens")} de saída. O texto do pedido não é registrado.");
                break;
            case AuditActions.QualityReviewed:
                yield return Entry("", AuditCategories.Read, "Alterações revisadas pelo Spock",
                    $"Revisão de {(Text(d, "mode") == "branch" ? "branch" : "alterações locais")}: {Clean(Text(d, "verdict"))}; {Count(Count(d, "files"), "arquivo", "arquivos")} e {Count(Count(d, "findings"), "achado", "achados")}. Só leitura: nada no projeto foi alterado.");
                break;
            case AuditActions.BootFinished:
                yield return Entry("", AuditCategories.Context, "Boot encerrado",
                    $"{BootLabel(Text(d, "mode"))}: {Clean(Text(d, "state"))}{(Text(d, "outcome") is { Length: > 0 } outcome ? $", resultado {Clean(outcome)}" : "")}; {Count((d["capabilities"] as JsonArray)?.Count ?? 0, "capacidade usada", "capacidades usadas")}.");
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
                        $"{Count(applied, "arquivo gravado", "arquivos gravados")} na pasta de arquitetura do projeto depois da aprovação."));
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
    // O texto do prompt nunca é gravado; só o que motivou o aviso.
    private static string PromptSignalsDetail(JsonObject d)
    {
        var signals = d["signals"] is JsonArray list ? list.OfType<JsonValue>().Select(item => item.TryGetValue<string>(out var s) ? s : "").ToList() : [];
        var what = signals.Select(signal => signal switch
        {
            "version_changed" => "o contexto mudou desde o começo da sessão",
            "hint:management" => "sugestão de ferramenta para gestão",
            "hint:architecture" => "sugestão de ferramenta para arquitetura",
            "hint:infrastructure" => "sugestão de ferramenta para infraestrutura",
            _ when signal.StartsWith("stale:", StringComparison.Ordinal) => "fonte desatualizada",
            _ => ""
        }).Where(text => text.Length > 0).Distinct().ToList();
        var version = Clean(Text(d, "contextVersion"));
        return what.Count == 0 ? $"Versão {version}." : $"Versão {version}: {string.Join("; ", what)}.";
    }

    // O texto do pedido nunca é gravado: só o modo, o que foi usado e como terminou.
    private static string IdeBootDetail(JsonObject d)
    {
        var capabilities = d["capabilities"] is JsonArray list
            ? list.OfType<JsonValue>().Select(item => item.TryGetValue<string>(out var id) ? Clean(id) : "").Where(id => id.Length > 0).ToList()
            : [];
        var outcome = Text(d, "outcome");
        var used = capabilities.Count == 0 ? "nenhuma capacidade usada" : $"{Count(capabilities.Count, "capacidade usada", "capacidades usadas")}: {string.Join(", ", capabilities.Take(ShownPaths))}";
        var client = Clean(Text(d, "client"));
        return $"{BootLabel(Text(d, "mode"))}: {Clean(Text(d, "state"))}{(outcome.Length > 0 ? $", resultado {Clean(outcome)}" : "")}; {used}. Só leitura, e o texto do pedido não é registrado." +
               (client.Length > 0 ? $" Cliente MCP informado: {client}." : "");
    }

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

    private static string BootLabel(string mode) => mode switch
    {
        "project" => "Project Boot",
        "recovery" => "Boot de recuperação",
        "work" => "Work Boot",
        "autonomous" => "Boot autônomo",
        _ => "Boot de pedido"
    };

    private static string Count(int value, string one, string many) => $"{value} {(value == 1 ? one : many)}";

    private static string ItemText(JsonObject d) =>
        $"{(Text(d, "kind") == DataKinds.PullRequests ? "pull request" : "issue")} {Clean(Text(d, "externalId"))}";

    // A trilha mostra campo ausente como zero ou vazio: o registro é antigo ou de uma versão que não gravava o campo.
    private static int Count(JsonObject d, string name) => JsonRead.Int(d, name) ?? 0;

    private static bool Flag(JsonObject d, string name) => JsonRead.Flag(d, name);

    private static string Text(JsonObject d, string name) => JsonRead.Text(d, name) ?? "";

    private static string Clean(string text)
    {
        var flat = string.Join(' ', new string(text.Select(ch => char.IsControl(ch) ? ' ' : ch).ToArray()).Split(' ', StringSplitOptions.RemoveEmptyEntries));
        return flat.Length > 160 ? flat[..160].TrimEnd() + "…" : flat;
    }
}
