using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Mariliz.Application.Integrations;
using Mariliz.Application.Ports;
using Mariliz.Application.Workspace;

namespace Mariliz.Application.Execution;

/// <summary>
/// Uma execução ligada a um item de trabalho (issue, pull request ou história) que já foi sincronizado. Guarda só a
/// referência, nunca o conteúdo do item: título e situação saem dos dados sincronizados na hora de mostrar.
/// </summary>
public sealed record RunLink(string IntegrationId, string Kind, string ExternalId, DateTimeOffset LinkedAt);

/// <summary>Onde as ligações de uma execução ficam, ao lado do diário e fora dele: ligar não reescreve o diário.</summary>
public interface IRunLinkStore
{
    IReadOnlyList<RunLink> LoadLinks(string projectId, string runId);
    void SaveLinks(string projectId, string runId, IReadOnlyList<RunLink> links);
}

/// <summary>
/// Liga e desliga execuções de itens de trabalho. A pessoa informa o identificador (o número no GitHub, a chave no Jira) e o
/// motor o procura entre o que as integrações do projeto já sincronizaram: um item que não foi lido não pode ser ligado.
/// </summary>
public sealed partial class RunLinkService(
    IRunStore runs, IRunLinkStore links, IntegrationHub hub, WorkspaceAuthorizer workspace, IClock clock)
{
    public const int MaxLinksPerRun = 10;
    public const int MaxIdentifierLength = 64;
    private readonly object _gate = new();

    /// <summary>
    /// Liga a execução ao item. <paramref name="integrationId"/> só é preciso quando mais de uma integração do projeto tem um
    /// item com o mesmo identificador. Ligar de novo o mesmo item não muda nada.
    /// </summary>
    public JsonArray Link(string projectId, string runId, string externalId, string? integrationId)
    {
        RequireRun(projectId, runId);
        var identifier = NormalizeIdentifier(externalId);

        var matches = Matches(projectId, identifier, integrationId);
        if (matches.Count == 0)
            throw new EngineException("LINK_ITEM_NOT_FOUND",
                "Nenhum item sincronizado deste projeto tem esse identificador. Confira o número ou a chave e sincronize a integração.");
        if (matches.Count > 1)
            throw new EngineException("LINK_ITEM_AMBIGUOUS",
                "Mais de um item sincronizado deste projeto tem esse identificador. Informe de qual integração ele é.");

        var (integration, stored) = matches[0];
        lock (_gate)
        {
            var current = links.LoadLinks(projectId, runId).ToList();
            if (!current.Any(Same(integration.Id, stored.Item.Kind, stored.Item.ExternalId)))
            {
                if (current.Count >= MaxLinksPerRun)
                    throw new EngineException("LINK_LIMIT", $"Uma execução liga a até {MaxLinksPerRun} itens.");
                current.Add(new RunLink(integration.Id, stored.Item.Kind, stored.Item.ExternalId, clock.UtcNow));
                links.SaveLinks(projectId, runId, current);
                hub.Audit.Record("run.linked", new JsonObject
                {
                    ["projectId"] = projectId,
                    ["runId"] = runId,
                    ["integrationId"] = integration.Id,
                    ["kind"] = stored.Item.Kind,
                    ["externalId"] = stored.Item.ExternalId
                });
            }
        }

        return For(projectId, runId);
    }

    /// <summary>Desliga a execução do item. Desligar o que não está ligado não faz nada.</summary>
    public JsonArray Unlink(string projectId, string runId, string integrationId, string kind, string externalId)
    {
        RequireRun(projectId, runId);
        lock (_gate)
        {
            var current = links.LoadLinks(projectId, runId).ToList();
            if (current.RemoveAll(link => Same(integrationId, kind, externalId)(link)) > 0)
            {
                links.SaveLinks(projectId, runId, current);
                hub.Audit.Record("run.unlinked", new JsonObject
                {
                    ["projectId"] = projectId,
                    ["runId"] = runId,
                    ["integrationId"] = integrationId,
                    ["kind"] = kind,
                    ["externalId"] = externalId
                });
            }
        }

        return For(projectId, runId);
    }

    /// <summary>
    /// As ligações da execução, com título e situação lidos agora dos dados sincronizados. Se o item saiu do recorte da
    /// integração, a ligação continua e aparece como indisponível, sem inventar o que ele era.
    /// </summary>
    public JsonArray For(string projectId, string runId)
    {
        var integrations = hub.Projects.List(projectId).ToDictionary(item => item.Id);
        var result = new JsonArray();
        foreach (var link in links.LoadLinks(projectId, runId))
        {
            integrations.TryGetValue(link.IntegrationId, out var integration);
            var item = integration is null
                ? null
                : hub.Projects.Data(integration).Items.GetValueOrDefault($"{link.Kind}:{link.ExternalId}")?.Item;
            result.Add(new JsonObject
            {
                ["integrationId"] = link.IntegrationId,
                ["provider"] = integration?.Provider,
                ["kind"] = link.Kind,
                ["externalId"] = link.ExternalId,
                ["linkedAt"] = link.LinkedAt.ToString("O", CultureInfo.InvariantCulture),
                ["available"] = item is not null,
                ["title"] = item?.Title,
                ["status"] = item?.Status,
                ["statusCategory"] = item?.StatusCategory
            });
        }

        return result;
    }

    private void RequireRun(string projectId, string runId)
    {
        if (!workspace.IsAuthorized(projectId)) throw EngineErrors.ProjectNotAuthorized();
        RunJournal? journal;
        try
        {
            journal = runs.LoadJournal(projectId, runId);
        }
        catch (ArgumentException)
        {
            // O id vem do cliente e vira nome de pasta: formato inválido é "não existe", não falha interna.
            journal = null;
        }

        if (journal is null) throw EngineErrors.RunNotFound();
    }

    private List<(ProjectIntegration Integration, StoredWorkItem Stored)> Matches(string projectId, string identifier, string? integrationId)
    {
        var found = new List<(ProjectIntegration, StoredWorkItem)>();
        foreach (var integration in hub.Projects.List(projectId).Where(item => integrationId is null || item.Id == integrationId))
        foreach (var stored in hub.Projects.Data(integration).Items.Values)
            if (string.Equals(stored.Item.ExternalId, identifier, StringComparison.OrdinalIgnoreCase))
                found.Add((integration, stored));
        return found;
    }

    /// <summary>"#123" e "123" são o mesmo item; o resto do formato é o do provedor (número, ou chave como APP-12).</summary>
    private static string NormalizeIdentifier(string value)
    {
        var text = value.Trim().TrimStart('#');
        if (text.Length is 0 or > MaxIdentifierLength || !IdentifierPattern().IsMatch(text))
            throw new EngineException("LINK_IDENTIFIER_INVALID", "Informe o número do item (por exemplo 123) ou a chave dele (por exemplo APP-12).");
        return text;
    }

    private static Func<RunLink, bool> Same(string integrationId, string kind, string externalId) =>
        link => link.IntegrationId == integrationId && link.Kind == kind
            && string.Equals(link.ExternalId, externalId, StringComparison.OrdinalIgnoreCase);

    [GeneratedRegex("^[A-Za-z0-9_-]+$")]
    private static partial Regex IdentifierPattern();
}
