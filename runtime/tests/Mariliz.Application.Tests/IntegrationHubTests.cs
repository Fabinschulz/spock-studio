using System.Text.Json.Nodes;
using Mariliz.Application.Execution;
using Mariliz.Application.Integrations;
using Mariliz.Application.Tests.Support;
using Mariliz.Infrastructure.Integrations;

namespace Mariliz.Application.Tests;

// Os provedores são dublês com a forma da API real; nenhuma chamada sai para a rede.
public class IntegrationHubTests
{
    [Fact]
    public async Task Conexao_so_e_gravada_depois_que_o_provedor_aceita_e_a_credencial_fica_so_no_cofre()
    {
        using var h = new IntegrationHarness();

        var created = h.Request("connection.create", new JsonObject
        {
            ["provider"] = "github", ["name"] = "GitHub da equipe", ["settings"] = new JsonObject(), ["secret"] = FakeGitHub.Token
        });

        var connection = created["result"]!["connection"]!;
        Assert.Equal("octo", connection["account"]!.GetValue<string>());
        Assert.Equal("connected", connection["status"]!.GetValue<string>());
        Assert.Equal(["repo", "read:org"], connection["scopes"]!.AsArray().Select(s => s!.GetValue<string>()));
        Assert.Equal(FakeGitHub.Token, h.Vault.Secrets[connection["id"]!.GetValue<string>()]);

        Assert.DoesNotContain(FakeGitHub.Token, created.ToJsonString());
        Assert.DoesNotContain(FakeGitHub.Token, h.Request("connection.list", new JsonObject()).ToJsonString());
        Assert.DoesNotContain(FakeGitHub.Token, h.EverythingOnDisk());
        Assert.DoesNotContain(h.Audit.Entries, e => e.Detail.Contains(FakeGitHub.Token));
        Assert.Contains(h.Audit.Entries, e => e.Action == "connection.created");
        await Task.CompletedTask;
    }

    [Fact]
    public void Credencial_recusada_pelo_provedor_nao_deixa_rastro()
    {
        using var h = new IntegrationHarness();

        var refused = h.Request("connection.create", new JsonObject
        {
            ["provider"] = "github", ["name"] = "GitHub", ["settings"] = new JsonObject(), ["secret"] = "ghp_token-errado-0000"
        });

        Assert.Equal("INTEGRATION_AUTH_FAILED", refused["error"]!["code"]!.GetValue<string>());
        Assert.DoesNotContain("ghp_token-errado-0000", refused.ToJsonString());
        Assert.Empty(h.Hub.Connections.List());
        Assert.Empty(h.Vault.Secrets);
    }

    [Fact]
    public async Task Sem_cofre_protegido_nenhuma_conexao_e_criada()
    {
        using var h = new IntegrationHarness();
        h.Vault.Available = false;

        var error = await Assert.ThrowsAsync<EngineException>(() => h.ConnectGitHubAsync());

        Assert.Equal("CREDENTIAL_VAULT_UNAVAILABLE", error.Code);
        Assert.Empty(h.Transport.Requests);
        Assert.Empty(h.Hub.Connections.List());
    }

    [Fact]
    public async Task Trocar_a_credencial_confere_a_nova_antes_e_mantem_a_antiga_se_for_recusada()
    {
        using var h = new IntegrationHarness();
        var connection = await h.ConnectGitHubAsync();

        var error = await Assert.ThrowsAsync<EngineException>(() => h.Hub.Connections.RotateAsync(connection.Id, "ghp_outro-token-0000", CancellationToken.None));

        Assert.Equal("INTEGRATION_AUTH_FAILED", error.Code);
        Assert.Equal(FakeGitHub.Token, h.Vault.Secrets[connection.Id]);
    }

    [Fact]
    public async Task Teste_de_conexao_registra_a_recusa_na_conexao_em_vez_de_falhar()
    {
        using var h = new IntegrationHarness();
        var connection = await h.ConnectGitHubAsync();
        h.Vault.Secrets[connection.Id] = "ghp_token-revogado-0000";

        var tested = await h.Hub.Connections.TestAsync(connection.Id, CancellationToken.None);

        Assert.Equal(ConnectionStatus.Error, tested.Status);
        Assert.Equal("INTEGRATION_AUTH_FAILED", tested.LastError!.Code);
    }

    [Fact]
    public async Task Desconectar_apaga_a_credencial_mantem_os_dados_e_bloqueia_novas_leituras()
    {
        using var h = new IntegrationHarness();
        h.GitHub.Issues.Add(FakeGitHub.Issue(1, "2026-11-01T10:00:00Z"));
        var integration = await h.LinkGitHubAsync();
        await h.SyncAsync(integration);
        var calls = h.Transport.Requests.Count;

        var disconnected = h.Hub.Connections.Disconnect(integration.ConnectionId);
        var sync = await h.SyncAsync(integration);

        Assert.Equal(ConnectionStatus.Disconnected, disconnected.Status);
        Assert.Empty(h.Vault.Secrets);
        Assert.Equal(SyncStatus.Failed, sync.Status);
        Assert.Equal("CONNECTION_DISCONNECTED", sync.Error!.Code);
        Assert.Equal(calls, h.Transport.Requests.Count);
        Assert.Single(h.Hub.Projects.Data(integration).Items);
        Assert.Equal("CONNECTION_IN_USE", Assert.Throws<EngineException>(() => h.Hub.Connections.Remove(integration.ConnectionId)).Code);
    }

    [Fact]
    public async Task Vinculo_exige_projeto_autorizado_recurso_alcancavel_e_tipos_que_o_conector_oferece()
    {
        using var h = new IntegrationHarness();
        var connection = await h.ConnectGitHubAsync();
        Task<ProjectIntegration> Link(string project, string resource, params string[] kinds) =>
            h.Hub.Projects.LinkAsync(project, connection.Id, resource, kinds, CancellationToken.None);

        Assert.Equal("PROJECT_NOT_AUTHORIZED", (await Assert.ThrowsAsync<EngineException>(() => Link("p-000000000000", "acme/app", "issues"))).Code);
        Assert.Equal("INTEGRATION_RESOURCE_INVALID", (await Assert.ThrowsAsync<EngineException>(() => Link(h.ProjectId, "../acme", "issues"))).Code);
        Assert.Equal("INTEGRATION_RESOURCE_NOT_FOUND", (await Assert.ThrowsAsync<EngineException>(() => Link(h.ProjectId, "acme/segredo", "issues"))).Code);
        Assert.Equal("INTEGRATION_SCOPE_INVALID", (await Assert.ThrowsAsync<EngineException>(() => Link(h.ProjectId, "acme/app", "wiki"))).Code);
        Assert.Equal("INTEGRATION_SCOPE_INVALID", (await Assert.ThrowsAsync<EngineException>(() => Link(h.ProjectId, "acme/app"))).Code);

        var linked = await Link(h.ProjectId, "acme/app", "issues");
        Assert.False(linked.Policy.FeedsKnowledge);
        Assert.Equal("INTEGRATION_ALREADY_LINKED", (await Assert.ThrowsAsync<EngineException>(() => Link(h.ProjectId, "ACME/App", "issues"))).Code);
    }

    [Fact]
    public async Task Um_projeto_nao_enxerga_nem_altera_a_integracao_de_outro()
    {
        using var h = new IntegrationHarness();
        h.GitHub.Issues.Add(FakeGitHub.Issue(1, "2026-11-01T10:00:00Z"));
        var integration = await h.LinkGitHubAsync();
        await h.SyncAsync(integration);

        Assert.Empty(h.Hub.Projects.List(h.OtherProjectId));
        foreach (var attempt in new Action[]
                 {
                     () => h.Hub.Projects.Require(h.OtherProjectId, integration.Id),
                     () => h.Hub.Projects.Configure(h.OtherProjectId, integration.Id, [DataKinds.Issues], true),
                     () => h.Hub.Projects.Unlink(h.OtherProjectId, integration.Id),
                     () => h.Hub.Sync.Start(h.OtherProjectId, integration.Id),
                     () => h.Hub.Sync.History(h.OtherProjectId, integration.Id)
                 })
            Assert.Equal("INTEGRATION_NOT_FOUND", Assert.Throws<EngineException>(attempt).Code);

        var listed = h.Request("integration.list", new JsonObject { ["projectId"] = h.OtherProjectId });
        Assert.Empty(listed["result"]!["integrations"]!.AsArray());
        Assert.True(File.Exists(Path.Combine(h.DataDir, "integrations", "data", h.ProjectId, integration.Id + ".json")));
        Assert.False(Directory.Exists(Path.Combine(h.DataDir, "integrations", "data", h.OtherProjectId)));
        Assert.Single(h.Hub.Projects.Data(integration).Items);
    }

    [Fact]
    public async Task Sincronizacao_so_chama_o_recurso_do_recorte_e_so_guarda_os_tipos_habilitados()
    {
        using var h = new IntegrationHarness();
        h.GitHub.Issues.Add(FakeGitHub.Issue(1, "2026-11-01T10:00:00Z"));
        h.GitHub.Issues.Add(FakeGitHub.Issue(2, "2026-11-02T10:00:00Z", pullRequest: true));
        var integration = await h.LinkGitHubAsync(DataKinds.Issues);
        h.Transport.Requests.Clear();

        var sync = await h.SyncAsync(integration);

        Assert.Equal(SyncStatus.Succeeded, sync.Status);
        Assert.All(h.Transport.Requests, r => Assert.StartsWith("/repos/acme/app/", r.Url.AbsolutePath));
        var items = h.Hub.Projects.Data(integration).Items.Values;
        Assert.Equal(DataKinds.Issues, Assert.Single(items).Item.Kind);
        Assert.Equal(1, sync.Fetched);
    }

    [Fact]
    public async Task Reduzir_o_recorte_apaga_o_que_ficou_de_fora_e_fica_na_auditoria()
    {
        using var h = new IntegrationHarness();
        h.GitHub.Issues.Add(FakeGitHub.Issue(1, "2026-11-01T10:00:00Z"));
        h.GitHub.Issues.Add(FakeGitHub.Issue(2, "2026-11-02T10:00:00Z", open: false, pullRequest: true, mergedAt: "2026-11-02T10:00:00Z"));
        var integration = await h.LinkGitHubAsync();
        await h.SyncAsync(integration);
        Assert.Equal(2, h.Hub.Projects.Data(integration).Items.Count);

        var configured = h.Hub.Projects.Configure(h.ProjectId, integration.Id, [DataKinds.PullRequests], feedsKnowledge: true);

        var remaining = Assert.Single(h.Hub.Projects.Data(integration).Items.Values).Item;
        Assert.Equal(DataKinds.PullRequests, remaining.Kind);
        Assert.Equal("merged", remaining.Status);
        Assert.Equal(StatusCategories.Done, remaining.StatusCategory);
        Assert.True(configured.Policy.FeedsKnowledge);
        Assert.Contains(h.Audit.Entries, e => e.Action == "integration.scope_changed" && e.Detail.Contains("\"feedsKnowledge\":true"));
    }

    [Fact]
    public async Task Desvincular_apaga_os_dados_sincronizados_do_vinculo()
    {
        using var h = new IntegrationHarness();
        h.GitHub.Issues.Add(FakeGitHub.Issue(1, "2026-11-01T10:00:00Z"));
        var integration = await h.LinkGitHubAsync();
        await h.SyncAsync(integration);

        h.Hub.Projects.Unlink(h.ProjectId, integration.Id);

        Assert.Empty(h.Hub.Projects.List(h.ProjectId));
        Assert.False(File.Exists(Path.Combine(h.DataDir, "integrations", "data", h.ProjectId, integration.Id + ".json")));
        h.Hub.Connections.Remove(integration.ConnectionId);
        Assert.Empty(h.Hub.Connections.List());
    }

    [Fact]
    public async Task Sincronizar_de_novo_nao_duplica_e_so_pede_o_que_mudou()
    {
        using var h = new IntegrationHarness();
        for (var n = 1; n <= 5; n++) h.GitHub.Issues.Add(FakeGitHub.Issue(n, $"2026-11-0{n}T10:00:00Z"));
        var integration = await h.LinkGitHubAsync();

        var first = await h.SyncAsync(integration);
        h.Transport.Requests.Clear();
        var second = await h.SyncAsync(integration);

        Assert.Equal((false, 3, 5, 5, 0, 0), (first.Incremental, first.Pages, first.Fetched, first.Created, first.Updated, first.Unchanged));
        Assert.Equal((true, 1, 1, 0, 0, 1), (second.Incremental, second.Pages, second.Fetched, second.Created, second.Updated, second.Unchanged));
        Assert.Contains("since=2026-11-05T10%3A00%3A00Z", h.Transport.Requests.Single().Url.Query);
        Assert.Equal(5, h.Hub.Projects.Data(integration).Items.Count);

        h.GitHub.Issues[0]["state"] = "closed";
        h.GitHub.Issues[0]["updated_at"] = "2026-11-09T08:00:00Z";
        h.GitHub.Issues.Add(FakeGitHub.Issue(6, "2026-11-09T09:00:00Z"));
        var third = await h.SyncAsync(integration);

        Assert.Equal((3, 1, 1, 1), (third.Fetched, third.Created, third.Updated, third.Unchanged));
        var data = h.Hub.Projects.Data(integration);
        Assert.Equal(6, data.Items.Count);
        Assert.Equal(StatusCategories.Done, data.Items["issues:1"].Item.StatusCategory);
        Assert.Equal(DateTimeOffset.Parse("2026-11-09T09:00:00Z"), data.Checkpoint);
    }

    [Fact]
    public async Task Falha_no_meio_mantem_o_que_ja_chegou_e_a_proxima_retoma_do_ponto_gravado()
    {
        using var h = new IntegrationHarness();
        for (var n = 1; n <= 5; n++) h.GitHub.Issues.Add(FakeGitHub.Issue(n, $"2026-11-0{n}T10:00:00Z"));
        var integration = await h.LinkGitHubAsync();
        h.GitHub.FailPage = page => page == 2 ? new ConnectorResponse(503, FakeConnectorTransport.Headers(), "") : null;

        var failed = await h.SyncAsync(integration);

        Assert.Equal(SyncStatus.Failed, failed.Status);
        Assert.Equal("INTEGRATION_UNAVAILABLE", failed.Error!.Code);
        Assert.Equal([TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4)], h.Delays);
        var partial = h.Hub.Projects.Data(integration);
        Assert.Equal(2, partial.Items.Count);
        Assert.Equal(DateTimeOffset.Parse("2026-11-02T10:00:00Z"), partial.Checkpoint);
        Assert.Equal("INTEGRATION_UNAVAILABLE", h.Hub.Connections.Require(integration.ConnectionId).LastError!.Code);

        h.GitHub.FailPage = null;
        h.Transport.Requests.Clear();
        var resumed = await h.SyncAsync(integration);

        Assert.Equal(SyncStatus.Succeeded, resumed.Status);
        Assert.True(resumed.Incremental);
        Assert.All(h.Transport.Requests, r => Assert.Contains("since=2026-11-02T10%3A00%3A00Z", r.Url.Query));
        Assert.Equal(5, h.Hub.Projects.Data(integration).Items.Count);
        Assert.Equal((3, 1), (resumed.Created, resumed.Unchanged));
        Assert.Null(h.Hub.Connections.Require(integration.ConnectionId).LastError);
        Assert.Equal([SyncStatus.Succeeded, SyncStatus.Failed], h.Hub.Sync.History(h.ProjectId, integration.Id).Select(e => e.Status));
    }

    [Fact]
    public async Task Teto_de_paginas_encerra_como_parcial_e_a_proxima_continua()
    {
        using var h = new IntegrationHarness();
        h.GitHub.PageSize = 1;
        for (var n = 0; n < SyncService.MaxPages + 3; n++)
            h.GitHub.Issues.Add(FakeGitHub.Issue(n + 1, DateTimeOffset.Parse("2026-10-01T00:00:00Z").AddHours(n).ToString("yyyy-MM-dd'T'HH:mm:ss'Z'")));
        var integration = await h.LinkGitHubAsync();

        var first = await h.SyncAsync(integration);
        var second = await h.SyncAsync(integration);

        Assert.Equal((SyncStatus.Partial, SyncService.MaxPages), (first.Status, first.Pages));
        Assert.Equal(SyncStatus.Succeeded, second.Status);
        Assert.Equal(SyncService.MaxPages + 3, h.Hub.Projects.Data(integration).Items.Count);
    }

    [Fact]
    public async Task Execucao_cortada_por_uma_queda_vira_falha_na_proxima_leitura()
    {
        using var h = new IntegrationHarness();
        var integration = await h.LinkGitHubAsync();
        var store = new FileSyncedDataStore(h.DataDir);
        var data = store.Read(h.ProjectId, integration.Id);
        data.Record(new SyncExecution("sync-000000000001", h.Clock.UtcNow, null, SyncStatus.Running, false, 1, 2, 2, 0, 0, null));
        store.Write(h.ProjectId, integration.Id, data);

        var interrupted = Assert.Single(h.Hub.Sync.History(h.ProjectId, integration.Id));

        Assert.Equal(SyncStatus.Failed, interrupted.Status);
        Assert.Equal("SYNC_INTERRUPTED", interrupted.Error!.Code);
        Assert.Equal(SyncStatus.Succeeded, (await h.SyncAsync(integration)).Status);
    }

    [Fact]
    public async Task Sincronizacao_pelo_protocolo_responde_na_hora_e_nao_abre_duas_ao_mesmo_tempo()
    {
        using var h = new IntegrationHarness();
        h.GitHub.Issues.Add(FakeGitHub.Issue(1, "2026-11-01T10:00:00Z"));
        var integration = await h.LinkGitHubAsync();
        var release = new TaskCompletionSource();
        var inner = h.Transport.Handler;
        h.Transport.Handler = request =>
        {
            release.Task.Wait(TimeSpan.FromSeconds(10));
            return inner(request);
        };
        var parameters = new JsonObject { ["projectId"] = h.ProjectId, ["integrationId"] = integration.Id };

        var started = h.Request("integration.sync", (JsonObject)parameters.DeepClone())["result"]!["sync"]!;
        var again = h.Request("integration.sync", (JsonObject)parameters.DeepClone())["result"]!["sync"]!;
        var blocked = h.Request("integration.unlink", (JsonObject)parameters.DeepClone());

        Assert.Equal("running", started["status"]!.GetValue<string>());
        Assert.Equal(started["id"]!.GetValue<string>(), again["id"]!.GetValue<string>());
        Assert.Equal("INTEGRATION_SYNC_RUNNING", blocked["error"]!["code"]!.GetValue<string>());

        release.SetResult();
        await h.Hub.Sync.ShutdownAsync();
        for (var i = 0; i < 100 && h.Hub.Activity.IsRunning(integration.Id); i++) await Task.Delay(20);

        var finished = Assert.Single(h.Hub.Sync.History(h.ProjectId, integration.Id));
        Assert.NotEqual(SyncStatus.Running, finished.Status);
        Assert.NotNull(finished.FinishedAt);
    }

    [Fact]
    public async Task Limite_de_chamadas_curto_espera_e_segue_e_longo_vira_erro_com_a_hora()
    {
        var clock = new FakeClock();
        var delays = new List<TimeSpan>();
        var responses = new Queue<ConnectorResponse>();
        var transport = new FakeConnectorTransport { Handler = _ => responses.Dequeue() };
        var client = new ConnectorClient(transport, clock, (wait, _) =>
        {
            delays.Add(wait);
            return Task.CompletedTask;
        });
        var request = new ConnectorRequest(new Uri("https://api.github.com/user"), new Dictionary<string, string>());

        responses.Enqueue(new ConnectorResponse(429, FakeConnectorTransport.Headers(("retry-after", "3")), ""));
        responses.Enqueue(new ConnectorResponse(200, FakeConnectorTransport.Headers(), "{}"));
        Assert.Equal(200, (await client.GetAsync(request, CancellationToken.None)).Status);
        Assert.Equal([TimeSpan.FromSeconds(3)], delays);

        var reset = clock.UtcNow.AddMinutes(20).ToUnixTimeSeconds().ToString();
        responses.Enqueue(new ConnectorResponse(403, FakeConnectorTransport.Headers(("x-ratelimit-remaining", "0"), ("x-ratelimit-reset", reset)), ""));
        var limited = await Assert.ThrowsAsync<EngineException>(() => client.GetAsync(request, CancellationToken.None));
        Assert.Equal("INTEGRATION_RATE_LIMITED", limited.Code);
        Assert.Contains("12:20", limited.Message);
        Assert.Empty(responses);

        // Um 403 sem sinal de cota é falta de permissão: volta como está, sem repetir.
        responses.Enqueue(new ConnectorResponse(403, FakeConnectorTransport.Headers(), ""));
        Assert.Equal(403, (await client.GetAsync(request, CancellationToken.None)).Status);

        await Assert.ThrowsAsync<EngineException>(() =>
            client.GetAsync(new ConnectorRequest(new Uri("http://api.github.com/user"), new Dictionary<string, string>()), CancellationToken.None));
    }

    [Fact]
    public async Task Jira_so_aceita_site_atlassian_e_le_issues_com_historico_de_status()
    {
        using var h = new IntegrationHarness();
        foreach (var site in new[] { "http://acme.atlassian.net", "https://acme.atlassian.net.evil.example", "https://evil.example/acme.atlassian.net", "https://10.0.0.1" })
        {
            var refused = await Assert.ThrowsAsync<EngineException>(() => h.Hub.Connections.CreateAsync(IntegrationProviders.Jira, "Jira",
                new Dictionary<string, string> { ["site"] = site, ["email"] = FakeJira.Email }, FakeJira.Token, CancellationToken.None));
            Assert.Equal("INTEGRATION_SETTINGS_INVALID", refused.Code);
        }
        Assert.Empty(h.Transport.Requests);

        h.Jira.Issues.Add(FakeJira.Issue("APP-2", "2026-11-08T15:30:00.000-0300", "indeterminate", "Em andamento"));
        h.Jira.Issues.Add(FakeJira.Issue("APP-3", "2026-11-09T09:00:00.000+0000", "done", "Concluído"));
        var connection = await h.ConnectJiraAsync();
        Assert.Equal(("Ana Souza", FakeJira.Site), (connection.Account, connection.Settings["site"]));
        var integration = await h.Hub.Projects.LinkAsync(h.ProjectId, connection.Id, "app", [DataKinds.Issues], CancellationToken.None);
        Assert.Equal(("APP", "Aplicativo"), (integration.Scope.Resource, integration.Scope.ResourceName));

        var first = await h.SyncAsync(integration);
        h.Clock.UtcNow = DateTimeOffset.Parse("2026-11-09T10:00:00Z");
        await h.SyncAsync(integration);

        Assert.Equal((SyncStatus.Succeeded, 2, 2), (first.Status, first.Pages, first.Created));
        Assert.Equal("project = \"APP\" ORDER BY updated ASC", h.Jira.SeenJql[0]);
        Assert.Equal("project = \"APP\" AND updated >= -65m ORDER BY updated ASC", h.Jira.SeenJql[^1]);
        var item = h.Hub.Projects.Data(integration).Items["issues:APP-2"].Item;
        Assert.Equal(("Em andamento", StatusCategories.InProgress, "Story", "APP-1"), (item.Status, item.StatusCategory, item.Type, item.Parent));
        Assert.Equal(DateTimeOffset.Parse("2026-11-08T18:30:00Z"), item.UpdatedAt);
        Assert.Equal(new DateOnly(2026, 12, 1), item.DueDate);
        Assert.Equal("https://acme.atlassian.net/browse/APP-2", item.Url);
        var transition = Assert.Single(item.Transitions);
        Assert.Equal(("A fazer", "Em andamento", DateTimeOffset.Parse("2026-10-05T13:00:00Z")), (transition.From, transition.To, transition.At));
        Assert.DoesNotContain(FakeJira.Token, h.EverythingOnDisk());
    }

    [Fact]
    public void Cofre_do_windows_guarda_cifrado_e_devolve_a_mesma_credencial()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var project = TempProject.Dotnet();
        var vault = new DpapiSecretVault(project.DataDir);
        const string id = "conn-0123456789ab";

        vault.Store(id, FakeGitHub.Token);

        var file = Path.Combine(project.DataDir, "integrations", "secrets", id + ".bin");
        Assert.DoesNotContain(FakeGitHub.Token, System.Text.Encoding.Latin1.GetString(File.ReadAllBytes(file)));
        Assert.True(vault.TryRead(id, out var secret));
        Assert.Equal(FakeGitHub.Token, secret);
        File.WriteAllBytes(file, [1, 2, 3]);
        Assert.False(vault.TryRead(id, out _));
        vault.Remove(id);
        Assert.False(File.Exists(file));

        try
        {
            vault.Store("../fora", "x");
            Assert.Fail("Um id forjado não pode virar caminho.");
        }
        catch (EngineException)
        {
        }
    }

    [Fact]
    public void Identificador_forjado_nao_aponta_para_fora_da_pasta_de_dados()
    {
        using var project = TempProject.Dotnet();
        var store = new FileSyncedDataStore(project.DataDir);

        Assert.Throws<EngineException>(() => store.Read("p-000000000000", "../../workspaces"));
        Assert.Throws<EngineException>(() => store.Write("../..", "pint-000000000000", new SyncedData()));
    }

    private static readonly bool Update = Environment.GetEnvironmentVariable("MARILIZ_UPDATE_GOLDEN") == "1";

    // O Zod da interface é conferido contra os mesmos arquivos. Para regenerar depois de uma mudança deliberada: MARILIZ_UPDATE_GOLDEN=1 dotnet test.
    [Fact]
    public async Task Respostas_da_central_conferem_com_as_fixtures_do_contrato()
    {
        using var h = new IntegrationHarness();
        h.GitHub.Issues.Add(FakeGitHub.Issue(1, "2026-11-01T10:00:00Z"));
        h.GitHub.Issues.Add(FakeGitHub.Issue(2, "2026-11-02T10:00:00Z", open: false, pullRequest: true, mergedAt: "2026-11-02T10:00:00Z"));
        var integration = await h.LinkGitHubAsync();
        await h.ConnectJiraAsync();
        await h.SyncAsync(integration);
        var scope = new JsonObject { ["projectId"] = h.ProjectId, ["integrationId"] = integration.Id };

        var scenarios = new Dictionary<string, JsonNode>
        {
            ["catalog"] = h.Request("integration.catalog", new JsonObject())["result"]!,
            ["connections"] = h.Request("connection.list", new JsonObject())["result"]!,
            ["integrations"] = h.Request("integration.list", new JsonObject { ["projectId"] = h.ProjectId })["result"]!,
            ["reach"] = h.Request("connection.reach", new JsonObject())["result"]!,
            ["history"] = h.Request("integration.history", scope)["result"]!
        };

        foreach (var (name, result) in scenarios)
        {
            var actual = Normalize(result, h.ProjectId).Replace(Path.GetFileName(h.Project.Root), "loja");
            var path = ContractFiles.Path_("fixtures", "integrations", name + ".json");
            if (Update || !File.Exists(path))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, actual + "\n");
                continue;
            }
            Assert.True(actual == File.ReadAllText(path).ReplaceLineEndings("\n").TrimEnd(),
                $"{name}: a resposta mudou. Se foi deliberado, regenere com MARILIZ_UPDATE_GOLDEN=1 e ajuste o Zod da interface.");
        }
    }

    private static string Normalize(JsonNode node, string projectId)
    {
        var text = node.ToJsonString(new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });
        text = text.Replace(projectId, "p-000000000000");
        var connections = new Dictionary<string, string>();
        text = System.Text.RegularExpressions.Regex.Replace(text, "conn-[0-9a-f]{12}", match =>
        {
            if (!connections.TryGetValue(match.Value, out var stable))
                connections[match.Value] = stable = $"conn-{connections.Count + 1:x12}";
            return stable;
        });
        text = System.Text.RegularExpressions.Regex.Replace(text, "pint-[0-9a-f]{12}", "pint-000000000001");
        text = System.Text.RegularExpressions.Regex.Replace(text, "sync-[0-9a-f]{12}", "sync-000000000001");
        return text.ReplaceLineEndings("\n");
    }
}
