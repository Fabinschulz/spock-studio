using Mariliz.Core.Policy;
using System.Globalization;
using System.Text;
using Mariliz.Application.Cloud;
using Mariliz.Application.Engineering;
using Mariliz.Application.Execution;
using Mariliz.Application.Integrations;
using Mariliz.Application.Ports;
using Mariliz.Application.Workspace;
using Mariliz.Core.Text;

namespace Mariliz.Application.Context;

// F2 (CONTEXT_ENGINE.md): para um projeto, uma intenção e um orçamento, devolve um pacote ordenado, compacto e rastreável.
// Só compõe o que os serviços já sabem; não calcula métrica, não lê conteúdo de fonte externa e não usa modelo para escolher.
public sealed class ContextEngine(
    WorkspaceAuthorizer workspace,
    HarnessStatusService harness,
    ITechnologyProfileStore profiles,
    ITokenizers tokenizers,
    IClock clock,
    IntegrationHub? integrations = null,
    CloudHub? cloud = null)
{
    public const int DefaultBootstrapBudget = 1500;
    public const int MinBudget = 200;
    public const int MaxBudget = 20_000;

    // Prazos de frescor do estudo (§5): dados de gestão 24 h; perfil 7 dias.
    public static readonly TimeSpan IntegrationFreshFor = TimeSpan.FromHours(24);
    public static readonly TimeSpan ProfileFreshFor = TimeSpan.FromDays(7);

    private static readonly (string Id, string Title)[] BootstrapSections =
    [
        ("identity", "Identidade"),
        ("policies", "O que cada vínculo libera"),
        ("connections", "Conexões e frescor"),
        ("harness", "Harness"),
        ("warnings", "Avisos")
    ];

    public ContextPackage Compose(string projectId, string intent, int? budgetTokens = null)
    {
        if (!workspace.IsAuthorized(projectId)) throw EngineErrors.ProjectNotAuthorized();
        if (!ContextIntents.Available.Contains(intent, StringComparer.Ordinal))
            throw new EngineException("CONTEXT_INTENT_UNKNOWN",
                $"Intenção '{intent}' não disponível. Disponíveis nesta versão: {string.Join(", ", ContextIntents.Available)}.");
        var budget = budgetTokens ?? DefaultBootstrapBudget;
        if (budget is < MinBudget or > MaxBudget)
            throw new EngineException("CONTEXT_BUDGET_INVALID", $"O orçamento precisa ficar entre {MinBudget} e {MaxBudget} tokens.");

        var now = clock.UtcNow;
        var project = workspace.Projects().Single(p => p.ProjectId == projectId);
        var stableId = workspace.StableIdOf(projectId);
        var naming = workspace.NamingOf(projectId);
        var status = harness.Inspect(projectId);
        var links = integrations?.Projects.List(projectId) ?? [];
        var cloudLinks = cloud?.Projects.List(projectId) ?? [];
        var limitations = new List<string>();
        if (integrations is null) limitations.Add("As integrações não estão disponíveis nesta sessão do motor: o pacote não fala de Jira, GitHub e afins.");
        if (cloud is null) limitations.Add("As conexões cloud não estão disponíveis nesta sessão do motor.");

        var candidates = new List<ContextItem>();
        candidates.AddRange(Identity(project, stableId, now, naming));
        candidates.AddRange(Policies(links, cloudLinks));
        candidates.AddRange(Connections(links, cloudLinks, now));
        candidates.AddRange(Harness(status, naming));
        candidates.AddRange(Warnings(projectId, stableId, now, naming));

        var counter = tokenizers.For(null, null);
        var title = $"# Contexto do projeto {project.DisplayName} ({intent})";
        var kept = new List<ContextItem>();
        var omitted = new List<ContextOmission>();
        foreach (var item in candidates)
        {
            if (counter.Count(Render(title, [.. kept, item], limitations)) <= budget) kept.Add(item);
            else omitted.Add(new ContextOmission(item.Section, item.Ref, "Não coube no orçamento de tokens."));
        }
        // O aviso do que ficou de fora também gasta tokens: se ele estourar o orçamento, sai mais um item do fim.
        var text = Render(title, kept, limitations, omitted.Count);
        while (kept.Count > 0 && counter.Count(text) > budget)
        {
            var last = kept[^1];
            kept.RemoveAt(kept.Count - 1);
            omitted.Insert(0, new ContextOmission(last.Section, last.Ref, "Não coube no orçamento de tokens."));
            text = Render(title, kept, limitations, omitted.Count);
        }

        return new ContextPackage(projectId, stableId, intent, budget, counter.Count(text), counter.Method,
            Version(project, stableId, links, cloudLinks, status), now, kept, omitted, limitations, text);
    }

    private static IEnumerable<ContextItem> Identity(AuthorizedProject project, string? stableId, DateTimeOffset now, HarnessNaming naming)
    {
        yield return stableId is null
            ? new ContextItem("identity", $"{project.DisplayName}: cópia {project.ProjectId} em {project.DisplayPath}, sem id estável.",
                "spock", project.ProjectId, ContextClassifications.Discovered, now, ContextFreshness.Fresh)
            : new ContextItem("identity", $"{project.DisplayName}: projeto {stableId}, cópia {project.ProjectId} em {project.DisplayPath}.",
                naming.Declaration, stableId, ContextClassifications.Declared, now, ContextFreshness.Fresh);
    }

    private IEnumerable<ContextItem> Policies(IReadOnlyList<ProjectIntegration> links, IReadOnlyList<CloudProjectLink> cloudLinks)
    {
        foreach (var link in links)
        {
            var reach = link.Policy.FeedsKnowledge
                ? "o conteúdo entra no índice de conhecimento do projeto"
                : "o conteúdo fica só na gestão, fora do índice de conhecimento";
            yield return new ContextItem("policies", $"{IntegrationName(link)} ({link.Provider}) · {link.Scope.ResourceName}: {reach}.",
                "spock", link.Id, ContextClassifications.Declared, link.LinkedAt, ContextFreshness.Fresh);
        }
        foreach (var link in cloudLinks)
        {
            var agents = link.AgentAccess ? "agentes podem ler" : "agentes não leem";
            yield return new ContextItem("policies",
                $"{CloudName(link)} ({link.Provider}): {link.Capabilities.Count} leituras liberadas; {agents}.",
                "spock", link.Id, ContextClassifications.Declared, link.LinkedAt, ContextFreshness.Fresh);
        }
    }

    private IEnumerable<ContextItem> Connections(IReadOnlyList<ProjectIntegration> links, IReadOnlyList<CloudProjectLink> cloudLinks,
        DateTimeOffset now)
    {
        if (links.Count == 0 && cloudLinks.Count == 0)
        {
            yield return new ContextItem("connections", "Nenhuma integração nem conexão cloud vinculada a este projeto.",
                "spock", "connections", ContextClassifications.Declared, now, ContextFreshness.Fresh);
            yield break;
        }
        foreach (var link in links)
        {
            var items = integrations!.Projects.Data(link).Items.Values;
            DateTimeOffset? synced = items.Count == 0 ? null : items.Max(item => item.SyncedAt);
            var freshness = synced is null ? ContextFreshness.Unknown
                : now - synced.Value <= IntegrationFreshFor ? ContextFreshness.Fresh : ContextFreshness.Stale;
            var when = synced is { } at ? $"dados lidos em {Stamp(at)}" : "ainda sem dados lidos";
            var age = freshness == ContextFreshness.Stale ? " (mais de 24 h: podem estar desatualizados)" : "";
            var connection = integrations.Connections.List().FirstOrDefault(c => c.Id == link.ConnectionId);
            var state = connection is null || connection.Status == ConnectionStatus.Connected ? "" : $"; conexão {Describe(connection.Status)}";
            yield return new ContextItem("connections", $"{IntegrationName(link)} · {link.Scope.ResourceName}: {when}{age}{state}.",
                link.Provider, link.Id, ContextClassifications.Integration, synced, freshness);
        }
        foreach (var link in cloudLinks)
        {
            var connection = cloud!.Connections.List().FirstOrDefault(c => c.Id == link.ConnectionId);
            if (connection is null) continue;
            var freshness = now - connection.CheckedAt <= IntegrationFreshFor ? ContextFreshness.Fresh : ContextFreshness.Stale;
            yield return new ContextItem("connections",
                $"{connection.Name} ({link.Provider}): {Describe(connection.Status)}, verificada em {Stamp(connection.CheckedAt)}; as leituras são feitas na hora.",
                $"cloud:{link.Provider}", link.Id, ContextClassifications.Integration, connection.CheckedAt, freshness);
        }
    }

    private static IEnumerable<ContextItem> Harness(HarnessStatus status, HarnessNaming naming)
    {
        if (!status.Applied)
        {
            yield return new ContextItem("harness", "Harness não aplicado: o projeto não tem as regras gerenciadas pelo Spock.",
                naming.Lock, "harness", ContextClassifications.Discovered, status.InspectedAt, ContextFreshness.Fresh);
            yield break;
        }
        var target = status.AiEnvironment is null ? "Aplicado" : $"Aplicado para {status.AiEnvironment}";
        var specification = status.SpecificationVersion is null ? "" : $", especificação {status.SpecificationVersion}";
        yield return new ContextItem("harness",
            $"{target}{specification}: {status.Intact} íntegros, {status.Kept} mantidos pela equipe, {status.Modified} modificados à mão, {status.Missing} faltando.",
            naming.Lock, "harness", ContextClassifications.Discovered, status.InspectedAt, ContextFreshness.Fresh);
        foreach (var component in status.Components.Where(component => component.State != "intact"))
            yield return new ContextItem("harness", $"{component.Label}: {State(component.State)}.",
                naming.Lock, component.Id, ContextClassifications.Discovered, status.InspectedAt, ContextFreshness.Fresh);
    }

    private IEnumerable<ContextItem> Warnings(string projectId, string? stableId, DateTimeOffset now, HarnessNaming naming)
    {
        if (stableId is null)
            yield return new ContextItem("warnings",
                $"Sem id estável (project.id no {naming.Declaration}): planeje e aplique o Harness no Spock para que clones e worktrees sejam o mesmo projeto.",
                "spock", "project.id", ContextClassifications.Calculated, now, ContextFreshness.Fresh);
        var profile = profiles.Read(projectId);
        if (profile is null)
            yield return new ContextItem("warnings", "Tecnologias ainda não analisadas no Spock.",
                "spock", "profile", ContextClassifications.Calculated, now, ContextFreshness.Unknown);
        else if (now - profile.AnalyzedAt > ProfileFreshFor)
            yield return new ContextItem("warnings",
                $"Perfil de tecnologias de {Stamp(profile.AnalyzedAt)}, com mais de 7 dias: reanalise no Spock se o projeto mudou.",
                "spock", "profile", ContextClassifications.Calculated, profile.AnalyzedAt, ContextFreshness.Stale);
    }

    private static string Render(string title, IReadOnlyList<ContextItem> items, IReadOnlyList<string> limitations, int omitted = 0)
    {
        var text = new StringBuilder(title).Append('\n');
        foreach (var (id, heading) in BootstrapSections)
        {
            var section = items.Where(item => item.Section == id).ToList();
            if (section.Count == 0) continue;
            text.Append("\n## ").Append(heading).Append('\n');
            foreach (var item in section) text.Append("- ").Append(item.Text).Append('\n');
        }
        if (limitations.Count > 0 || omitted > 0)
        {
            text.Append("\n## Limitações\n");
            foreach (var limitation in limitations) text.Append("- ").Append(limitation).Append('\n');
            if (omitted > 0) text.Append("- ").Append(omitted).Append(" itens ficaram de fora pelo orçamento de tokens.\n");
        }
        return text.ToString();
    }

    // Só o que é declarado ou aprovado: frescor, contagens e estado dos arquivos ficam de fora, senão a versão mudaria a cada
    // sincronização ou edição.
    private string Version(AuthorizedProject project, string? stableId, IReadOnlyList<ProjectIntegration> links,
        IReadOnlyList<CloudProjectLink> cloudLinks, HarnessStatus status)
    {
        var parts = new List<string> { project.ProjectId, stableId ?? "", project.DisplayName };
        parts.AddRange(links.OrderBy(link => link.Id, StringComparer.Ordinal)
            .Select(link => $"i|{link.Id}|{IntegrationName(link)}|{link.Scope.ResourceName}|{link.Policy.FeedsKnowledge}"));
        parts.AddRange(cloudLinks.OrderBy(link => link.Id, StringComparer.Ordinal)
            .Select(link => $"c|{link.Id}|{CloudName(link)}|{string.Join(",", link.Capabilities.Order(StringComparer.Ordinal))}|{link.AgentAccess}"));
        parts.Add($"h|{status.Applied}|{status.AiEnvironment}|{status.SpecificationVersion}");
        parts.AddRange(status.Components.Select(component => $"h|{component.Id}|{component.Version}").Order(StringComparer.Ordinal));
        parts.AddRange(status.Excluded.Select(component => $"x|{component.Id}").Order(StringComparer.Ordinal));
        return "ctx-" + Hashing.Sha256(string.Join("\n", parts))[..16];
    }

    private string IntegrationName(ProjectIntegration link) =>
        integrations?.Connections.List().FirstOrDefault(c => c.Id == link.ConnectionId)?.Name ?? link.Provider;

    private string CloudName(CloudProjectLink link) =>
        cloud?.Connections.List().FirstOrDefault(c => c.Id == link.ConnectionId)?.Name ?? link.Provider;

    private static string Stamp(DateTimeOffset at) => at.UtcDateTime.ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture);

    private static string Describe(ConnectionStatus status) => status switch
    {
        ConnectionStatus.Connected => "conectada",
        ConnectionStatus.Error => "com erro",
        _ => "desconectada (os dados já lidos continuam)"
    };

    private static string Describe(CloudConnectionStatus status) => status switch
    {
        CloudConnectionStatus.Connected => "conectada",
        CloudConnectionStatus.NotAuthenticated => "aguardando autenticação",
        CloudConnectionStatus.Expired => "credencial vencida",
        CloudConnectionStatus.InvalidIdentity => "identidade inválida",
        CloudConnectionStatus.PermissionDenied => "sem permissão",
        CloudConnectionStatus.InvalidRegion => "região inválida",
        _ => "indisponível"
    };

    private static string State(string state) => state switch
    {
        "kept" => "mantido pela equipe",
        "modified" => "modificado à mão",
        "missing" => "faltando",
        "outOfScope" => "fora do que o Spock gerencia",
        _ => state
    };
}
