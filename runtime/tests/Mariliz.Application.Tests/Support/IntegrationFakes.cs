using System.Text;
using System.Text.Json.Nodes;
using System.Web;
using Mariliz.Application.Integrations;
using Mariliz.Application.Management;
using Mariliz.Application.Ports;
using Mariliz.Application.Protocol;
using Mariliz.Application.Workspace;
using Mariliz.Infrastructure;
using Mariliz.Infrastructure.Integrations;

namespace Mariliz.Application.Tests.Support;

internal sealed class FakeClock : IClock
{
    public DateTimeOffset UtcNow { get; set; } = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);
}

internal sealed class MemoryVault : ISecretVault
{
    public Dictionary<string, string> Secrets { get; } = [];
    public bool Available { get; set; } = true;
    public void Store(string connectionId, string secret) => Secrets[connectionId] = secret;
    public bool TryRead(string connectionId, out string secret) => Secrets.TryGetValue(connectionId, out secret!);
    public void Remove(string connectionId) => Secrets.Remove(connectionId);
}

internal sealed class MemoryAudit : IAuditLog
{
    public List<(string Action, string Detail)> Entries { get; } = [];
    public void Record(string action, JsonObject detail) => Entries.Add((action, detail.ToJsonString()));
}

internal sealed class FakeConnectorTransport : IConnectorTransport
{
    public List<ConnectorRequest> Requests { get; } = [];
    public Func<ConnectorRequest, ConnectorResponse> Handler { get; set; } = _ => new ConnectorResponse(404, Headers(), "{}");

    public Task<ConnectorResponse> GetAsync(ConnectorRequest request, CancellationToken ct)
    {
        Requests.Add(request);
        return Task.FromResult(Handler(request));
    }

    public static Dictionary<string, string> Headers(params (string Name, string Value)[] headers) =>
        headers.ToDictionary(h => h.Name, h => h.Value, StringComparer.OrdinalIgnoreCase);

    public static ConnectorResponse Json(JsonNode body, params (string Name, string Value)[] headers) =>
        new(200, Headers(headers), body.ToJsonString());
}

/// <summary>Um GitHub de mentira com a forma da API real: credencial, repositório único, paginação e filtro por data.</summary>
internal sealed class FakeGitHub
{
    public const string Token = "ghp_token-de-teste-0001";
    public const string Repository = "acme/app";

    public List<JsonObject> Issues { get; } = [];
    public int PageSize { get; set; } = 2;
    public Func<int, ConnectorResponse?>? FailPage { get; set; }

    public ConnectorResponse Respond(ConnectorRequest request)
    {
        Assert.Equal("api.github.com", request.Url.Host);
        if (request.Headers.GetValueOrDefault("Authorization") != "Bearer " + Token)
            return new ConnectorResponse(401, FakeConnectorTransport.Headers(), "{\"message\":\"Bad credentials\"}");

        // Como no GitHub real, dono e repositório não diferenciam maiúsculas e a resposta traz o nome canônico.
        var path = request.Url.AbsolutePath.ToLowerInvariant();
        if (path == "/user")
            return FakeConnectorTransport.Json(new JsonObject { ["login"] = "octo" }, ("x-oauth-scopes", "repo, read:org"));
        if (path == "/repos/" + Repository)
            return FakeConnectorTransport.Json(new JsonObject { ["full_name"] = Repository });
        if (path != "/repos/" + Repository + "/issues")
            return new ConnectorResponse(404, FakeConnectorTransport.Headers(), "{\"message\":\"Not Found\"}");

        var query = HttpUtility.ParseQueryString(request.Url.Query);
        var page = int.Parse(query["page"] ?? "1");
        if (FailPage?.Invoke(page) is { } failure) return failure;

        var since = query["since"] is { } text ? DateTimeOffset.Parse(text) : (DateTimeOffset?)null;
        var matching = Issues
            .Where(i => since is null || DateTimeOffset.Parse(i["updated_at"]!.GetValue<string>()) >= since)
            .OrderBy(i => DateTimeOffset.Parse(i["updated_at"]!.GetValue<string>()))
            .ToList();
        var slice = matching.Skip((page - 1) * PageSize).Take(PageSize).Select(i => i.DeepClone()).ToArray();
        var more = matching.Count > page * PageSize;
        return FakeConnectorTransport.Json(new JsonArray(slice),
            more ? [("link", $"<https://api.github.com/repositories/1/issues?page={page + 1}>; rel=\"next\"")] : []);
    }

    public static JsonObject Issue(int number, string updatedAt, bool open = true, bool pullRequest = false, string? mergedAt = null) => new()
    {
        ["number"] = number,
        ["title"] = "Item " + number,
        ["state"] = open ? "open" : "closed",
        ["state_reason"] = open ? null : "completed",
        ["assignees"] = new JsonArray(new JsonObject { ["login"] = "dev-" + number }),
        ["labels"] = new JsonArray(new JsonObject { ["name"] = "backend" }),
        ["created_at"] = "2026-10-01T09:00:00Z",
        ["updated_at"] = updatedAt,
        ["closed_at"] = open ? null : updatedAt,
        ["html_url"] = $"https://github.com/{Repository}/issues/{number}",
        ["pull_request"] = pullRequest ? new JsonObject { ["merged_at"] = mergedAt } : null
    };
}

/// <summary>Um Jira Cloud de mentira: autenticação Basic, um projeto, busca por JQL com token de continuação.</summary>
internal sealed class FakeJira
{
    public const string Site = "https://acme.atlassian.net";
    public const string Email = "ana@example.com";
    public const string Token = "jira-token-de-teste-0001";

    public List<string> SeenJql { get; } = [];
    public List<JsonObject> Issues { get; } = [];

    public ConnectorResponse Respond(ConnectorRequest request)
    {
        Assert.Equal("acme.atlassian.net", request.Url.Host);
        var expected = "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes($"{Email}:{Token}"));
        if (request.Headers.GetValueOrDefault("Authorization") != expected)
            return new ConnectorResponse(401, FakeConnectorTransport.Headers(), "{}");

        var path = request.Url.AbsolutePath;
        if (path == "/rest/api/3/myself") return FakeConnectorTransport.Json(new JsonObject { ["displayName"] = "Ana Souza" });
        if (path == "/rest/api/3/project/APP") return FakeConnectorTransport.Json(new JsonObject { ["key"] = "APP", ["name"] = "Aplicativo" });
        if (path != "/rest/api/3/search/jql") return new ConnectorResponse(404, FakeConnectorTransport.Headers(), "{}");

        var query = HttpUtility.ParseQueryString(request.Url.Query);
        SeenJql.Add(query["jql"] ?? "");
        var second = query["nextPageToken"] == "t2";
        var slice = second ? Issues.Skip(1) : Issues.Take(1);
        var body = new JsonObject { ["issues"] = new JsonArray(slice.Select(i => i.DeepClone()).ToArray()) };
        if (!second && Issues.Count > 1) body["nextPageToken"] = "t2";
        else body["isLast"] = true;
        return FakeConnectorTransport.Json(body);
    }

    public static JsonObject Issue(string key, string updated, string category, string status) => new()
    {
        ["key"] = key,
        ["fields"] = new JsonObject
        {
            ["summary"] = "História " + key,
            ["status"] = new JsonObject { ["name"] = status, ["statusCategory"] = new JsonObject { ["key"] = category } },
            ["issuetype"] = new JsonObject { ["name"] = "Story" },
            ["assignee"] = new JsonObject { ["displayName"] = "Bruno Lima" },
            ["labels"] = new JsonArray("mobile"),
            ["created"] = "2026-10-01T09:00:00.000-0300",
            ["updated"] = updated,
            ["resolutiondate"] = category == "done" ? updated : null,
            ["duedate"] = "2026-12-01",
            ["parent"] = new JsonObject { ["key"] = "APP-1" }
        },
        ["changelog"] = new JsonObject
        {
            ["histories"] = new JsonArray(new JsonObject
            {
                ["created"] = "2026-10-05T10:00:00.000-0300",
                ["items"] = new JsonArray(
                    new JsonObject { ["field"] = "status", ["fromString"] = "A fazer", ["toString"] = "Em andamento" },
                    new JsonObject { ["field"] = "assignee", ["fromString"] = null, ["toString"] = "Bruno Lima" })
            })
        }
    };
}

/// <summary>A central de integrações sobre arquivos reais numa pasta temporária, com rede, cofre, relógio e esperas sob controle do teste.</summary>
internal sealed class IntegrationHarness : IDisposable
{
    public TempProject Project { get; } = TempProject.Dotnet();
    public TempProject OtherProject { get; } = TempProject.Dotnet();
    public FakeClock Clock { get; } = new();
    public MemoryVault Vault { get; } = new();
    public MemoryAudit Audit { get; } = new();
    public FakeConnectorTransport Transport { get; } = new();
    public FakeGitHub GitHub { get; } = new();
    public FakeJira Jira { get; } = new();
    public List<TimeSpan> Delays { get; } = [];
    public IntegrationHub Hub { get; }
    public ManagementService Management { get; }
    public WorkspaceAuthorizer Workspace { get; }
    public EngineSession Session { get; }
    public string ProjectId { get; }
    public string OtherProjectId { get; }
    public string DataDir => Project.DataDir;

    public IntegrationHarness()
    {
        var workspace = Workspace = new WorkspaceAuthorizer(new LocalWorkspaceFileSystem());
        ProjectId = workspace.Authorize(Project.Root).Project!.ProjectId;
        OtherProjectId = workspace.Authorize(OtherProject.Root).Project!.ProjectId;
        Transport.Handler = request => request.Url.Host == "api.github.com" ? GitHub.Respond(request) : Jira.Respond(request);

        var client = new ConnectorClient(Transport, Clock, (wait, _) =>
        {
            Delays.Add(wait);
            return Task.CompletedTask;
        });
        var connectors = new ConnectorRegistry([new GitHubConnector(client), new JiraConnector(client, Clock)]);
        Hub = new IntegrationHub(new FileIntegrationStore(DataDir), new FileSyncedDataStore(DataDir), Vault, connectors, workspace, Clock, Audit);
        Management = new ManagementService(workspace, Hub, Clock);
        Session = new EngineSession("test", integrations: Hub, management: Management);
    }

    public Task<IntegrationConnection> ConnectGitHubAsync(string name = "GitHub da equipe") =>
        Hub.Connections.CreateAsync(IntegrationProviders.GitHub, name, new Dictionary<string, string>(), FakeGitHub.Token, CancellationToken.None);

    public Task<IntegrationConnection> ConnectJiraAsync() =>
        Hub.Connections.CreateAsync(IntegrationProviders.Jira, "Jira da equipe",
            new Dictionary<string, string> { ["site"] = FakeJira.Site + "/", ["email"] = FakeJira.Email }, FakeJira.Token, CancellationToken.None);

    public async Task<ProjectIntegration> LinkGitHubAsync(params string[] kinds)
    {
        var connection = await ConnectGitHubAsync();
        return await Hub.Projects.LinkAsync(ProjectId, connection.Id, FakeGitHub.Repository,
            kinds.Length == 0 ? [DataKinds.Issues, DataKinds.PullRequests] : kinds, CancellationToken.None);
    }

    public Task<SyncExecution> SyncAsync(ProjectIntegration integration) =>
        Hub.Sync.RunToEndAsync(integration.ProjectId, integration.Id, CancellationToken.None);

    public JsonObject Request(string method, JsonObject parameters) =>
        Session.Handle(new JsonObject { ["v"] = 1, ["kind"] = "request", ["id"] = "t", ["method"] = method, ["params"] = parameters }.ToJsonString()).Response;

    /// <summary>Tudo o que o motor gravou no diretório de dados, para provar que a credencial não está em arquivo nenhum.</summary>
    public string EverythingOnDisk() =>
        string.Join("\n", Directory.EnumerateFiles(DataDir, "*", SearchOption.AllDirectories).Select(File.ReadAllText));

    public void Dispose()
    {
        Project.Dispose();
        OtherProject.Dispose();
    }
}
