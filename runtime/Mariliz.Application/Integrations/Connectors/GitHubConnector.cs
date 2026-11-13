using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Mariliz.Application.Integrations;

// Para a API, todo pull request é uma issue: uma página serve aos dois tipos e o recorte decide o que fica.
public sealed partial class GitHubConnector(ConnectorClient client) : IIntegrationConnector
{
    public const string ApiVersion = "2026-03-10";
    public const int PageSize = 100;

    private const string Api = "https://api.github.com";

    public ConnectorDescriptor Descriptor { get; } = new(
        IntegrationProviders.GitHub,
        "GitHub",
        "1.0.0",
        Settings: [],
        Secret: new ConnectorField("token", "Token de acesso pessoal",
            "Um token com leitura de issues e pull requests dos repositórios que você vai vincular."),
        Resource: new ConnectorField("repository", "Repositório", "No formato dono/repositório."),
        DataKinds: [new ConnectorDataKind(DataKinds.Issues, "Issues"), new ConnectorDataKind(DataKinds.PullRequests, "Pull requests")],
        ScopesNote: "Tokens clássicos informam os escopos. Tokens de acesso refinado não informam: o alcance é o configurado no GitHub.");

    public IReadOnlyDictionary<string, string> NormalizeSettings(IReadOnlyDictionary<string, string> settings) =>
        new Dictionary<string, string>();

    public string NormalizeResource(string resource)
    {
        var trimmed = resource.Trim();
        if (!RepositoryPattern().IsMatch(trimmed) || trimmed.EndsWith("/.", StringComparison.Ordinal) || trimmed.EndsWith("/..", StringComparison.Ordinal))
            throw IntegrationErrors.ResourceInvalid("Informe o repositório no formato dono/repositório.");
        return trimmed;
    }

    public async Task<ConnectionProbe> ProbeAsync(ConnectorAccess access, CancellationToken ct)
    {
        var response = await client.GetAsync(Request($"{Api}/user", access), ct);
        var user = ConnectorJson.Parse(response);
        var scopes = response.Headers.TryGetValue("x-oauth-scopes", out var header)
            ? header.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            : [];
        return new ConnectionProbe(ConnectorJson.RequiredText(user["login"]), scopes);
    }

    public async Task<ResourceProbe> ProbeResourceAsync(ConnectorAccess access, string resource, CancellationToken ct)
    {
        var repository = ConnectorJson.Parse(await client.GetAsync(Request($"{Api}/repos/{NormalizeResource(resource)}", access), ct));
        var fullName = NormalizeResource(ConnectorJson.RequiredText(repository["full_name"]));
        return new ResourceProbe(fullName, fullName);
    }

    public async Task<SyncPage> FetchPageAsync(ConnectorAccess access, DataScope scope, SyncCursor cursor, CancellationToken ct)
    {
        var page = cursor.Page is null ? 1 : int.Parse(cursor.Page, CultureInfo.InvariantCulture);
        var url = $"{Api}/repos/{NormalizeResource(scope.Resource)}/issues?state=all&sort=updated&direction=asc&per_page={PageSize}&page={page}";
        if (cursor.UpdatedSince is { } since)
            url += "&since=" + Uri.EscapeDataString(since.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture));

        var response = await client.GetAsync(Request(url, access), ct);
        if (ConnectorJson.Parse(response) is not JsonArray entries) throw IntegrationErrors.InvalidResponse();

        var items = entries.OfType<JsonObject>().Select(ToWorkItem).Where(item => scope.DataKinds.Contains(item.Kind)).ToList();
        // O endereço da próxima página vem no cabeçalho Link, mas em outra forma (/repositories/{id}); em vez de seguir um
        // endereço que o recorte não reconhece, o cabeçalho só diz se há próxima e a chamada é montada de novo pelo recorte.
        var hasNext = response.Headers.TryGetValue("link", out var link) && link.Contains("rel=\"next\"", StringComparison.Ordinal);
        return new SyncPage(items, hasNext ? (page + 1).ToString(CultureInfo.InvariantCulture) : null);
    }

    private static WorkItem ToWorkItem(JsonObject entry)
    {
        var pullRequest = entry["pull_request"] as JsonObject;
        var open = ConnectorJson.RequiredText(entry["state"]) == "open";
        var number = entry["number"] is JsonValue value && value.TryGetValue<long>(out var parsed) ? parsed : throw IntegrationErrors.InvalidResponse();

        return new WorkItem(
            Kind: pullRequest is null ? DataKinds.Issues : DataKinds.PullRequests,
            ExternalId: number.ToString(CultureInfo.InvariantCulture),
            Title: ConnectorJson.Text(entry["title"]) ?? "",
            Type: pullRequest is null ? "Issue" : "Pull request",
            Status: StatusOf(entry, pullRequest, open),
            // Um pull request aberto é trabalho em revisão; uma issue aberta só diz que não foi concluída.
            StatusCategory: !open ? StatusCategories.Done : pullRequest is null ? StatusCategories.Todo : StatusCategories.InProgress,
            Assignees: ConnectorJson.Objects(entry["assignees"]).Select(a => ConnectorJson.Text(a["login"])).OfType<string>().ToList(),
            Labels: ConnectorJson.Objects(entry["labels"]).Select(l => ConnectorJson.Text(l["name"])).OfType<string>().ToList(),
            CreatedAt: ConnectorJson.RequiredDate(entry["created_at"]),
            UpdatedAt: ConnectorJson.RequiredDate(entry["updated_at"]),
            ClosedAt: ConnectorJson.Date(entry["closed_at"]),
            DueDate: null,
            Parent: null,
            Url: ConnectorJson.Text(entry["html_url"]) ?? "",
            Transitions: [],
            Milestones: MilestonesOf(entry));
    }

    private static List<WorkItemMilestone> MilestonesOf(JsonObject entry)
    {
        if (entry["milestone"] is not JsonObject milestone || ConnectorJson.Text(milestone["title"]) is not { Length: > 0 } name) return [];
        var due = ConnectorJson.Date(milestone["due_on"]);
        return [new WorkItemMilestone(name, due is null ? null : DateOnly.FromDateTime(due.Value.UtcDateTime))];
    }

    private static string StatusOf(JsonObject entry, JsonObject? pullRequest, bool open)
    {
        if (pullRequest is not null)
        {
            if (ConnectorJson.Date(pullRequest["merged_at"]) is not null) return "merged";
            return open && ConnectorJson.Flag(entry["draft"]) ? "draft" : open ? "open" : "closed";
        }
        return open ? "open" : ConnectorJson.Text(entry["state_reason"]) ?? "closed";
    }

    private static ConnectorRequest Request(string url, ConnectorAccess access) => new(new Uri(url), new Dictionary<string, string>
    {
        ["Authorization"] = "Bearer " + access.Secret,
        ["Accept"] = "application/vnd.github+json",
        ["X-GitHub-Api-Version"] = ApiVersion,
        ["User-Agent"] = "Mariliz-Studio"
    });

    [GeneratedRegex("^[A-Za-z0-9](?:[A-Za-z0-9-]{0,38})/[A-Za-z0-9._-]{1,100}$")]
    private static partial Regex RepositoryPattern();
}
