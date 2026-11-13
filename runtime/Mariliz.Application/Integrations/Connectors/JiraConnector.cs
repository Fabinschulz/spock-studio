using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Mariliz.Application.Ports;

namespace Mariliz.Application.Integrations;

// O site precisa ser *.atlassian.net: a credencial nunca é enviada a outro domínio. Jira Server e Data Center não são atendidos.
public sealed partial class JiraConnector(ConnectorClient client, IClock clock) : IIntegrationConnector
{
    public const int PageSize = 100;
    public const string SiteSetting = "site";
    public const string EmailSetting = "email";

    private const string Fields = "summary,status,issuetype,assignee,labels,created,updated,resolutiondate,duedate,parent,issuelinks,fixVersions";

    public ConnectorDescriptor Descriptor { get; } = new(
        IntegrationProviders.Jira,
        "Jira Cloud",
        "1.0.0",
        Settings:
        [
            new ConnectorField(SiteSetting, "Endereço do site", "Por exemplo https://sua-empresa.atlassian.net."),
            new ConnectorField(EmailSetting, "E-mail da conta", "O e-mail da conta Atlassian dona do token.")
        ],
        Secret: new ConnectorField("token", "Token de API", "Um token de API da conta Atlassian."),
        Resource: new ConnectorField("project", "Projeto do Jira", "A chave do projeto, por exemplo APP."),
        DataKinds: [new ConnectorDataKind(DataKinds.Issues, "Issues")],
        ScopesNote: "O token de API não informa escopos: o alcance é o da conta no Jira.");

    public IReadOnlyDictionary<string, string> NormalizeSettings(IReadOnlyDictionary<string, string> settings)
    {
        var site = (settings.GetValueOrDefault(SiteSetting) ?? "").Trim().TrimEnd('/').ToLowerInvariant();
        if (!SitePattern().IsMatch(site))
            throw IntegrationErrors.SettingsInvalid("O endereço do site precisa ser https://<nome>.atlassian.net.");

        var email = (settings.GetValueOrDefault(EmailSetting) ?? "").Trim();
        if (email.Length is < 3 or > 254 || !EmailPattern().IsMatch(email))
            throw IntegrationErrors.SettingsInvalid("Informe o e-mail da conta Atlassian.");

        return new Dictionary<string, string> { [SiteSetting] = site, [EmailSetting] = email };
    }

    public string NormalizeResource(string resource)
    {
        var key = resource.Trim().ToUpperInvariant();
        if (!ProjectKeyPattern().IsMatch(key))
            throw IntegrationErrors.ResourceInvalid("Informe a chave do projeto do Jira, por exemplo APP.");
        return key;
    }

    public async Task<ConnectionProbe> ProbeAsync(ConnectorAccess access, CancellationToken ct)
    {
        var myself = ConnectorJson.Parse(await client.GetAsync(Request(access, "/rest/api/3/myself"), ct));
        return new ConnectionProbe(ConnectorJson.RequiredText(myself["displayName"]), []);
    }

    public async Task<ResourceProbe> ProbeResourceAsync(ConnectorAccess access, string resource, CancellationToken ct)
    {
        var project = ConnectorJson.Parse(await client.GetAsync(Request(access, "/rest/api/3/project/" + NormalizeResource(resource)), ct));
        return new ResourceProbe(NormalizeResource(ConnectorJson.RequiredText(project["key"])), ConnectorJson.RequiredText(project["name"]));
    }

    public async Task<SyncPage> FetchPageAsync(ConnectorAccess access, DataScope scope, SyncCursor cursor, CancellationToken ct)
    {
        if (!scope.DataKinds.Contains(DataKinds.Issues)) return new SyncPage([], null);

        var query = $"/rest/api/3/search/jql?jql={Uri.EscapeDataString(Jql(scope, cursor))}&fields={Fields}&expand=changelog&maxResults={PageSize}";
        if (cursor.Page is not null) query += "&nextPageToken=" + Uri.EscapeDataString(cursor.Page);

        var result = ConnectorJson.Parse(await client.GetAsync(Request(access, query), ct));
        if (result["issues"] is not JsonArray issues) throw IntegrationErrors.InvalidResponse();

        var site = access.Settings[SiteSetting];
        var items = issues.OfType<JsonObject>().Select(issue => ToWorkItem(issue, site)).ToList();
        var next = ConnectorJson.Flag(result["isLast"]) ? null : ConnectorJson.Text(result["nextPageToken"]);
        return new SyncPage(items, next);
    }

    /// <summary>
    /// O JQL interpreta data absoluta no fuso da conta, que o motor não conhece. A janela relativa em minutos não depende de
    /// fuso; a folga cobre a diferença de relógio, e reler um item já gravado não o altera.
    /// </summary>
    private string Jql(DataScope scope, SyncCursor cursor)
    {
        var jql = $"project = \"{NormalizeResource(scope.Resource)}\"";
        if (cursor.UpdatedSince is { } since)
        {
            var minutes = Math.Max(0, (long)Math.Ceiling((clock.UtcNow - since).TotalMinutes)) + 5;
            jql += $" AND updated >= -{minutes.ToString(CultureInfo.InvariantCulture)}m";
        }
        return jql + " ORDER BY updated ASC";
    }

    private static WorkItem ToWorkItem(JsonObject issue, string site)
    {
        var key = ConnectorJson.RequiredText(issue["key"]);
        var fields = issue["fields"] as JsonObject ?? throw IntegrationErrors.InvalidResponse();
        var status = fields["status"] as JsonObject;
        var assignee = ConnectorJson.Text(fields["assignee"]?["displayName"]);

        return new WorkItem(
            Kind: DataKinds.Issues,
            ExternalId: key,
            Title: ConnectorJson.Text(fields["summary"]) ?? "",
            Type: ConnectorJson.Text(fields["issuetype"]?["name"]) ?? "",
            Status: ConnectorJson.Text(status?["name"]) ?? "",
            StatusCategory: ConnectorJson.Text(status?["statusCategory"]?["key"]) switch
            {
                "done" => StatusCategories.Done,
                "indeterminate" => StatusCategories.InProgress,
                _ => StatusCategories.Todo
            },
            Assignees: assignee is null ? [] : [assignee],
            Labels: fields["labels"] is JsonArray labels ? labels.Select(ConnectorJson.Text).OfType<string>().ToList() : [],
            CreatedAt: ConnectorJson.RequiredDate(fields["created"]),
            UpdatedAt: ConnectorJson.RequiredDate(fields["updated"]),
            ClosedAt: ConnectorJson.Date(fields["resolutiondate"]),
            DueDate: ConnectorJson.Day(fields["duedate"]),
            Parent: ConnectorJson.Text(fields["parent"]?["key"]),
            Url: $"{site}/browse/{key}",
            Transitions: Transitions(issue),
            BlockedBy: BlockedBy(fields),
            Milestones: Milestones(fields));
    }

    // Com inwardIssue, a relação deste item com o outro é a descrição de entrada ("is blocked by"). Só o tipo padrão "Blocks" é lido porque os campos de bloqueio mudam em cada site; um site que o renomeou não vira "sem bloqueio". Sem o campo, devolve null (não lido).
    private static List<string>? BlockedBy(JsonObject fields)
    {
        if (fields["issuelinks"] is not JsonArray links) return null;

        return ConnectorJson.Objects(links)
            .Where(link => link["inwardIssue"] is JsonObject && IsBlocksLink(link["type"] as JsonObject))
            .Select(link => link["inwardIssue"] as JsonObject)
            .Where(blocker => ConnectorJson.Text(blocker?["fields"]?["status"]?["statusCategory"]?["key"]) != "done")
            .Select(blocker => ConnectorJson.Text(blocker?["key"]))
            .OfType<string>()
            .ToList();
    }

    // Sem o campo, devolve null (não lido); com o campo vazio, lista vazia (o item não tem versão).
    private static List<WorkItemMilestone>? Milestones(JsonObject fields)
    {
        if (fields["fixVersions"] is not JsonArray versions) return null;
        return ConnectorJson.Objects(versions)
            .Select(version => (Name: ConnectorJson.Text(version["name"]), Due: ConnectorJson.Day(version["releaseDate"])))
            .Where(version => !string.IsNullOrEmpty(version.Name))
            .Select(version => new WorkItemMilestone(version.Name!, version.Due))
            .ToList();
    }

    private static bool IsBlocksLink(JsonObject? type) =>
        string.Equals(ConnectorJson.Text(type?["name"]), "Blocks", StringComparison.OrdinalIgnoreCase)
        || string.Equals(ConnectorJson.Text(type?["inward"]), "is blocked by", StringComparison.OrdinalIgnoreCase);

    private static List<WorkItemTransition> Transitions(JsonObject issue) =>
        ConnectorJson.Objects(issue["changelog"]?["histories"])
            .SelectMany(history => ConnectorJson.Objects(history["items"])
                .Where(change => ConnectorJson.Text(change["field"]) == "status" && ConnectorJson.Date(history["created"]) is not null)
                .Select(change => new WorkItemTransition(
                    ConnectorJson.Date(history["created"])!.Value,
                    ConnectorJson.Text(change["fromString"]) ?? "",
                    ConnectorJson.Text(change["toString"]) ?? "")))
            .OrderBy(transition => transition.At)
            .ToList();

    private static ConnectorRequest Request(ConnectorAccess access, string pathAndQuery)
    {
        var site = access.Settings.GetValueOrDefault(SiteSetting) ?? "";
        if (!SitePattern().IsMatch(site)) throw IntegrationErrors.SettingsInvalid("O endereço do site precisa ser https://<nome>.atlassian.net.");
        var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{access.Settings.GetValueOrDefault(EmailSetting)}:{access.Secret}"));
        return new ConnectorRequest(new Uri(site + pathAndQuery), new Dictionary<string, string>
        {
            ["Authorization"] = "Basic " + basic,
            ["Accept"] = "application/json"
        });
    }

    [GeneratedRegex(@"^https://[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?\.atlassian\.net$")]
    private static partial Regex SitePattern();

    [GeneratedRegex(@"^[^\s@:]+@[^\s@:]+\.[^\s@:]+$")]
    private static partial Regex EmailPattern();

    [GeneratedRegex("^[A-Z][A-Z0-9_]{1,19}$")]
    private static partial Regex ProjectKeyPattern();
}
