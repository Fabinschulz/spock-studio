using System.Text.Json.Nodes;
using Mariliz.Application.Cloud;
using Mariliz.Application.Execution;
using Mariliz.Application.Mcp;
using Mariliz.Application.Tests.Support;
using Mariliz.Application.Workspace;
using Mariliz.Infrastructure;
using Mariliz.Infrastructure.Cloud;

namespace Mariliz.Application.Tests;

// Regras de aplicação dos conectores cloud com um provedor de teste. A prova de funcionamento real fica em
// docs/plataforma/CLOUD_CONNECTORS_STATUS.md: estes testes não falam com nenhuma cloud.
public sealed class CloudConnectorTests : IDisposable
{
    private readonly TempProject _project = TempProject.Dotnet();
    private readonly MemoryAudit _audit = new();
    private readonly FakeClock _clock = new();
    private readonly FakeProvider _provider = new();

    public void Dispose() => _project.Dispose();

    private WorkspaceAuthorizer Workspace() => new(new LocalWorkspaceFileSystem(), new FileWorkspaceRegistry(_project.DataDir));

    private CloudHub Hub() => new(new FileCloudStore(_project.DataDir), new CloudProviderRegistry([_provider]), Workspace(), _clock, _audit);

    private string Project() => Workspace().Authorize(_project.Root).Project!.ProjectId;

    private static Dictionary<string, string> Settings(string region = "lab-1") => new() { ["region"] = region };

    [Fact]
    public async Task Conexao_sem_credencial_e_gravada_como_aguardando_autenticacao_e_sem_segredo()
    {
        _provider.Identify = () => throw CloudErrors.NotAuthenticated("Faça login.");
        var hub = Hub();

        var created = await hub.Connections.CreateAsync("fake", "Teste", "simple", Settings(), CancellationToken.None);

        Assert.Equal(CloudConnectionStatus.NotAuthenticated, created.Status);
        Assert.Equal(CloudErrors.NotAuthenticatedCode, created.LastError!.Code);
        Assert.Single(hub.Connections.List());
        var file = File.ReadAllText(Path.Combine(_project.DataDir, "cloud", "connections.json"));
        Assert.DoesNotContain("secret", file, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(_audit.Entries, entry => entry.Action == "cloud.connection_created");
    }

    [Fact]
    public async Task Configuracao_invalida_e_recusada_antes_de_gravar()
    {
        var hub = Hub();

        var unknownField = await Assert.ThrowsAsync<EngineException>(() =>
            hub.Connections.CreateAsync("fake", "Teste", "simple", new Dictionary<string, string> { ["region"] = "lab-1", ["secret"] = "x" }, CancellationToken.None));
        var badRegion = await Assert.ThrowsAsync<EngineException>(() => hub.Connections.CreateAsync("fake", "Teste", "simple", Settings("marte"), CancellationToken.None));
        var mechanism = await Assert.ThrowsAsync<EngineException>(() => hub.Connections.CreateAsync("fake", "Teste", "chaves", Settings(), CancellationToken.None));

        Assert.Equal("CLOUD_SETTINGS_INVALID", unknownField.Code);
        Assert.Equal(CloudErrors.RegionInvalidCode, badRegion.Code);
        Assert.Equal("CLOUD_SETTINGS_INVALID", mechanism.Code);
        Assert.Empty(hub.Connections.List());
    }

    [Fact]
    public async Task Verificacao_separa_leitura_permitida_negada_e_sem_resposta()
    {
        _provider.Read = query => query.Capability switch
        {
            "fake.items" => new CloudTable([["a", "1"]], false),
            "fake.denied" => throw CloudErrors.PermissionDenied(),
            _ => throw CloudErrors.Unavailable()
        };
        var hub = Hub();
        var connection = await hub.Connections.CreateAsync("fake", "Teste", "simple", Settings(), CancellationToken.None);

        var checkedOnce = await hub.Connections.CheckPermissionsAsync(connection.Id, CancellationToken.None);

        Assert.Equal(CloudPermissionState.Allowed, checkedOnce.Permissions.Single(p => p.Capability == "fake.items").State);
        Assert.Equal(CloudPermissionState.Denied, checkedOnce.Permissions.Single(p => p.Capability == "fake.denied").State);
        Assert.Equal(CloudPermissionState.Error, checkedOnce.Permissions.Single(p => p.Capability == "fake.flaky").State);
        Assert.Equal(CloudConnectionStatus.Connected, checkedOnce.Status);
    }

    [Fact]
    public async Task Agente_so_le_o_que_o_projeto_liberou_para_agentes()
    {
        var projectId = Project();
        var hub = Hub();
        var connection = await hub.Connections.CreateAsync("fake", "Teste", "simple", Settings(), CancellationToken.None);
        var agent = CloudCaller.Agent("ide");

        var withoutProject = await Assert.ThrowsAsync<EngineException>(() => hub.Reads.ReadAsync(new("" + connection.Id, "fake.items", null, 5, null), agent, CancellationToken.None));
        var notLinked = await Assert.ThrowsAsync<EngineException>(() => hub.Reads.ReadAsync(new(connection.Id, "fake.items", null, 5, projectId), agent, CancellationToken.None));
        var link = hub.Projects.Link(projectId, connection.Id, ["fake.items"], agentAccess: false);
        var closed = await Assert.ThrowsAsync<EngineException>(() => hub.Reads.ReadAsync(new(connection.Id, "fake.items", null, 5, projectId), agent, CancellationToken.None));
        hub.Projects.Configure(projectId, link.Id, null, agentAccess: true);
        var notEnabled = await Assert.ThrowsAsync<EngineException>(() => hub.Reads.ReadAsync(new(connection.Id, "fake.denied", null, 5, projectId), agent, CancellationToken.None));
        var read = await hub.Reads.ReadAsync(new(connection.Id, "fake.items", null, 5, projectId), agent, CancellationToken.None);

        Assert.Equal("CLOUD_NOT_LINKED", withoutProject.Code);
        Assert.Equal("CLOUD_NOT_LINKED", notLinked.Code);
        Assert.Equal("CLOUD_AGENT_NOT_ALLOWED", closed.Code);
        Assert.Equal("CLOUD_CAPABILITY_NOT_ENABLED", notEnabled.Code);
        Assert.Equal("lab-1", read.Region);
        Assert.Contains(_audit.Entries, entry => entry.Action == "cloud.read" && entry.Detail.Contains("\"caller\":\"agent\""));
    }

    [Fact]
    public async Task Pessoa_explora_a_propria_conexao_sem_projeto_e_o_limite_corta_o_resultado()
    {
        _provider.Read = _ => new CloudTable([["a", "1"], ["b", "2"], ["c", "3"]], false);
        var hub = Hub();
        var connection = await hub.Connections.CreateAsync("fake", "Teste", "simple", Settings(), CancellationToken.None);

        var read = await hub.Reads.ReadAsync(new(connection.Id, "fake.items", null, 2, null), CloudCaller.Person, CancellationToken.None);

        Assert.Equal(2, read.Rows.Count);
        Assert.True(read.Truncated);
    }

    [Fact]
    public async Task Linha_fora_das_colunas_e_resposta_invalida_e_credencial_vencida_muda_o_estado()
    {
        var hub = Hub();
        var connection = await hub.Connections.CreateAsync("fake", "Teste", "simple", Settings(), CancellationToken.None);

        _provider.Read = _ => new CloudTable([["só uma coluna"]], false);
        var invalid = await Assert.ThrowsAsync<EngineException>(() => hub.Reads.ReadAsync(new(connection.Id, "fake.items", null, 5, null), CloudCaller.Person, CancellationToken.None));
        _provider.Read = _ => throw CloudErrors.Expired("Renove.");
        await Assert.ThrowsAsync<EngineException>(() => hub.Reads.ReadAsync(new(connection.Id, "fake.items", null, 5, null), CloudCaller.Person, CancellationToken.None));

        Assert.Equal("CLOUD_INVALID_RESPONSE", invalid.Code);
        Assert.Equal(CloudConnectionStatus.Expired, hub.Connections.Require(connection.Id).Status);
    }

    [Fact]
    public async Task Conexao_vinculada_nao_sai_e_vinculo_repetido_e_recusado()
    {
        var projectId = Project();
        var hub = Hub();
        var connection = await hub.Connections.CreateAsync("fake", "Teste", "simple", Settings(), CancellationToken.None);
        hub.Projects.Link(projectId, connection.Id, [], agentAccess: false);

        Assert.Equal("CLOUD_CONNECTION_IN_USE", Assert.Throws<EngineException>(() => hub.Connections.Remove(connection.Id)).Code);
        Assert.Equal("CLOUD_ALREADY_LINKED", Assert.Throws<EngineException>(() => hub.Projects.Link(projectId, connection.Id, [], false)).Code);
        Assert.Equal("CLOUD_CAPABILITY_UNKNOWN", Assert.Throws<EngineException>(() => hub.Projects.Link(projectId, connection.Id, ["aws.s3.buckets"], false)).Code);
    }

    [Fact]
    public async Task Mcp_so_mostra_as_leituras_liberadas_para_agentes_e_confere_o_vinculo_em_cada_chamada()
    {
        var projectId = Project();
        var hub = Hub();
        var connection = await hub.Connections.CreateAsync("fake", "Teste", "simple", Settings(), CancellationToken.None);
        var link = hub.Projects.Link(projectId, connection.Id, ["fake.items"], agentAccess: true);
        var tools = new CloudMcpTools(hub, Workspace, _project.Root, McpScope.Of(projectId)).Tools();

        hub.Projects.Configure(projectId, link.Id, null, agentAccess: false);
        var revoked = await Assert.ThrowsAsync<McpToolException>(() => tools.Single(t => t.Info.Name == "fake_items").Call(new McpCall([], "ide", CancellationToken.None)));

        Assert.Equal([CloudMcpTools.ConnectionsTool, "fake_items"], tools.Select(t => t.Info.Name));
        Assert.True(tools.Single(t => t.Info.Name == "fake_items").Info.OpenWorld);
        Assert.Contains("não liberou", revoked.Message);
    }

    [Fact]
    public void Os_provedores_registrados_seguem_as_regras_comuns()
    {
        var registry = new CloudProviderRegistry(LocalCloudHub.Providers());

        Assert.Equal(["aws", "azure", "gcp"], registry.Catalog().Select(d => d.Provider).Order(StringComparer.Ordinal));
        foreach (var descriptor in registry.Catalog())
        {
            Assert.All(descriptor.Capabilities, c => Assert.Equal(CloudImpacts.Read, c.Impact));
            Assert.All(descriptor.Mechanisms, m => Assert.DoesNotContain(m.Fields, f => f.Name.Contains("secret", StringComparison.OrdinalIgnoreCase) || f.Name.Contains("key", StringComparison.Ordinal) && f.Name != "keyFile"));
        }
    }

    [Theory]
    [InlineData("aws", "profile", "profile=dev;region=SA-EAST-1", true)]
    [InlineData("aws", "profile", "profile=dev;region=marte-1", false)]
    [InlineData("aws", "assume-role", "roleArn=arn:aws:iam::123456789012:role/leitura;region=us-east-1", true)]
    [InlineData("aws", "assume-role", "roleArn=arn:aws:iam::123:role/x;region=us-east-1", false)]
    [InlineData("aws", "profile", "profile=dev perfil;region=us-east-1", false)]
    [InlineData("azure", "azure-cli", "subscriptionId=00000000-0000-0000-0000-000000000000;region=brazilsouth", true)]
    [InlineData("azure", "default", "tenantId=contoso.onmicrosoft.com", true)]
    [InlineData("azure", "default", "subscriptionId=minha-sub", false)]
    [InlineData("gcp", "adc", "projectId=minha-empresa-dev;region=southamerica-east1", true)]
    [InlineData("gcp", "adc", "projectId=X", false)]
    [InlineData("gcp", "service-account-file", "keyFile=chave.json;projectId=minha-empresa-dev", false)]
    public void Cada_provedor_confere_o_formato_da_propria_configuracao(string provider, string mechanism, string raw, bool valid)
    {
        var registry = new CloudProviderRegistry(LocalCloudHub.Providers());
        var values = raw.Split(';').Select(pair => pair.Split('=', 2)).ToDictionary(pair => pair[0], pair => pair[1]);

        if (valid)
            Assert.NotNull(registry.Normalize(provider, mechanism, values));
        else
            Assert.StartsWith("CLOUD_", Assert.Throws<EngineException>(() => registry.Normalize(provider, mechanism, values)).Code);
    }

    private sealed class FakeProvider : ICloudProvider
    {
        public Func<CloudIdentity> Identify { get; set; } = () => new CloudIdentity("000000000000", null, "arn:teste", null);
        public Func<CloudQuery, CloudTable> Read { get; set; } = _ => new CloudTable([["a", "1"]], false);

        private static readonly CloudColumn[] Columns = [new("name", "Nome"), new("value", "Valor")];

        public CloudProviderDescriptor Descriptor { get; } = new("fake", "Fake", "1.0.0",
            [new CloudMechanism("simple", "Simples", "Teste.", [new CloudField("region", "Região", "", Required: true)])],
            [
                new CloudCapability("fake.items", "Itens", "Itens.", "Teste", ["fake:List"], CloudRegionUse.Required, Columns),
                new CloudCapability("fake.denied", "Negada", "Negado.", "Teste", ["fake:Denied"], CloudRegionUse.None, Columns),
                new CloudCapability("fake.flaky", "Instável", "Sem resposta.", "Teste", ["fake:Flaky"], CloudRegionUse.None, Columns)
            ],
            "Teste.");

        public CloudSettings Normalize(CloudSettings settings) => settings with
        {
            Values = new Dictionary<string, string>(settings.Values) { ["region"] = NormalizeRegion(settings.Region ?? "") }
        };

        public string NormalizeRegion(string region) =>
            region.StartsWith("lab-", StringComparison.Ordinal) ? region : throw CloudErrors.RegionInvalid("Região de teste inválida.");

        public Task<CloudIdentity> IdentifyAsync(CloudSettings settings, CancellationToken ct) => Task.FromResult(Identify());

        public Task<CloudTable> ReadAsync(CloudSettings settings, CloudQuery query, CancellationToken ct) => Task.FromResult(Read(query));
    }
}
