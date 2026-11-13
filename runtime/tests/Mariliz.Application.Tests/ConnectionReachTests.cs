using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Mariliz.Application.Integrations;
using Mariliz.Application.Tests.Support;
using Mariliz.Application.Workspace;
using Mariliz.Infrastructure;
using Mariliz.Infrastructure.Integrations;

namespace Mariliz.Application.Tests;

/// <summary>
/// O resumo por conexão: onde é usada, o que se lê, o que pode fazer e se alimenta IA. É só leitura do que o motor já guarda, e
/// uma conexão ativa nunca concede nada por si: o recorte e a política de cada vínculo é que valem.
/// </summary>
public class ConnectionReachTests
{
    private static ConnectionReach ReachOf(IntegrationHarness h, string connectionId) => h.Hub.Reach.List().Single(reach => reach.ConnectionId == connectionId);

    [Fact]
    public async Task Conexao_sem_projeto_diz_que_nenhum_projeto_a_usa_e_que_so_le()
    {
        using var h = new IntegrationHarness();
        var connection = await h.ConnectGitHubAsync();

        var reach = ReachOf(h, connection.Id);

        Assert.Empty(reach.Projects);
        Assert.Equal(ImpactKinds.Read, reach.Impact);
    }

    [Fact]
    public async Task Vinculo_mostra_o_projeto_o_recurso_e_o_que_se_le_com_o_nome_que_a_pessoa_ve()
    {
        using var h = new IntegrationHarness();
        var integration = await h.LinkGitHubAsync(DataKinds.PullRequests);

        var project = Assert.Single(ReachOf(h, integration.ConnectionId).Projects);

        Assert.Equal(h.ProjectId, project.ProjectId);
        Assert.Equal(Path.GetFileName(h.Project.Root), project.ProjectName);
        Assert.Equal(FakeGitHub.Repository, project.Resource);
        Assert.Equal([DataKinds.PullRequests], project.DataKinds.Select(kind => kind.Id));
        Assert.All(project.DataKinds, kind => Assert.NotEqual(kind.Id, kind.Label));
    }

    [Fact]
    public async Task A_politica_nasce_fechada_e_o_resumo_mostra_a_mudanca()
    {
        using var h = new IntegrationHarness();
        var integration = await h.LinkGitHubAsync();
        Assert.False(ReachOf(h, integration.ConnectionId).Projects.Single().FeedsKnowledge);

        h.Hub.Projects.Configure(h.ProjectId, integration.Id, null, true);
        Assert.True(ReachOf(h, integration.ConnectionId).Projects.Single().FeedsKnowledge);

        h.Hub.Projects.Configure(h.ProjectId, integration.Id, null, false);
        Assert.False(ReachOf(h, integration.ConnectionId).Projects.Single().FeedsKnowledge);
    }

    [Fact]
    public async Task Cada_conexao_lista_so_os_vinculos_dela()
    {
        using var h = new IntegrationHarness();
        var first = await h.LinkGitHubAsync(DataKinds.Issues);
        var second = await h.ConnectGitHubAsync("Outro GitHub");

        Assert.Single(ReachOf(h, first.ConnectionId).Projects);
        Assert.Empty(ReachOf(h, second.Id).Projects);
        Assert.Equal(2, h.Hub.Reach.List().Count);
    }

    [Fact]
    public async Task Projeto_que_nao_esta_autorizado_neste_computador_aparece_sem_nome()
    {
        using var h = new IntegrationHarness();
        var integration = await h.LinkGitHubAsync();
        var nobody = new ConnectionReachService(new FileIntegrationStore(h.DataDir), h.Hub.Connectors, new WorkspaceAuthorizer(new LocalWorkspaceFileSystem()));

        var project = Assert.Single(nobody.List().Single(reach => reach.ConnectionId == integration.ConnectionId).Projects);

        Assert.Null(project.ProjectName);
        Assert.Equal(h.ProjectId, project.ProjectId);
    }

    [Fact]
    public async Task Conector_que_nao_existe_mais_nao_afirma_o_que_faz()
    {
        using var h = new IntegrationHarness();
        var connection = await h.ConnectGitHubAsync();
        var store = new FileIntegrationStore(h.DataDir);
        store.SaveConnection(connection with { Provider = "antigo" });

        Assert.Equal(ImpactKinds.Unknown, ReachOf(h, connection.Id).Impact);
    }

    [Fact]
    public void Todo_conector_do_catalogo_declara_que_so_le_e_o_contrato_de_transporte_nao_tem_como_escrever()
    {
        using var h = new IntegrationHarness();

        Assert.All(h.Hub.Connectors.Catalog(), descriptor => Assert.Equal(ImpactKinds.Read, descriptor.Impact));
        // O que torna "só lê" verdade: o transporte só tem GET, e um pedido não carrega método nem corpo.
        Assert.Equal(["GetAsync"], typeof(IConnectorTransport).GetMethods().Select(method => method.Name));
        Assert.Equal(["Headers", "Url"], typeof(ConnectorRequest).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.Name != "EqualityContract").Select(property => property.Name).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task O_resumo_nao_carrega_credencial_nem_caminho_de_pasta_e_nao_escreve_nada()
    {
        using var h = new IntegrationHarness();
        await h.LinkGitHubAsync();
        var before = h.EverythingOnDisk();

        var text = JsonSerializer.Serialize(h.Hub.Reach.List(), IntegrationJson.Options);

        Assert.DoesNotContain(FakeGitHub.Token, text);
        Assert.DoesNotContain(h.Project.Root, text);
        Assert.Equal(before, h.EverythingOnDisk());
    }

    [Fact]
    public async Task O_metodo_responde_para_todas_as_conexoes_e_o_catalogo_traz_o_impacto()
    {
        using var h = new IntegrationHarness();
        await h.LinkGitHubAsync();
        await h.ConnectJiraAsync();

        var reach = h.Request("connection.reach", new JsonObject())["result"]!["connections"]!.AsArray();
        var catalog = h.Request("integration.catalog", new JsonObject())["result"]!["connectors"]!.AsArray();

        Assert.Equal(2, reach.Count);
        Assert.All(reach, item => Assert.Equal("read", item!["impact"]!.GetValue<string>()));
        Assert.All(catalog, item => Assert.Equal("read", item!["impact"]!.GetValue<string>()));
    }
}
