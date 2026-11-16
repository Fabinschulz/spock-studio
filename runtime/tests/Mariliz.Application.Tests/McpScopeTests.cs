using System.Text.Json.Nodes;
using Mariliz.Application.Management;
using Mariliz.Application.Mcp;
using Mariliz.Application.Tests.Support;
using Mariliz.Application.Workspace;
using Mariliz.Core.Text;
using Mariliz.Infrastructure;
using Mariliz.Infrastructure.Engineering;
using Mariliz.Infrastructure.Integrations;

namespace Mariliz.Application.Tests;

// Validação real M3: qualquer cliente que rodasse o executável lia todos os projetos autorizados pelo argumento "project".
public sealed class McpScopeTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 11, 15, 12, 0, 0, TimeSpan.Zero);
    private readonly TempProject _loja = TempProject.Dotnet();
    private readonly TempProject _folha = TempProject.Dotnet();
    private readonly MemoryAudit _audit = new();

    public void Dispose()
    {
        _loja.Dispose();
        _folha.Dispose();
    }

    private WorkspaceAuthorizer Workspace() => new(new LocalWorkspaceFileSystem(), new FileWorkspaceRegistry(_loja.DataDir));

    private (string Loja, string Folha) Authorize() =>
        (Workspace().Authorize(_loja.Root).Project!.ProjectId, Workspace().Authorize(_folha.Root).Project!.ProjectId);

    private McpServer Server(IReadOnlySet<string> scope, string? workingDirectory = null) => new("1.2.3", new ProjectContextTools(
        Workspace, new TestFilesFactory(), new FakeClock { UtcNow = Now }, new FileTechnologyProfileStore(_loja.DataDir),
        new FileDecisionStore(_loja.DataDir), workingDirectory ?? Path.GetTempPath(), scope, audit: _audit).Tools());

    private static (string Text, bool Failed) Call(McpServer server, string tool, JsonObject? arguments = null)
    {
        var line = new JsonObject
        {
            ["jsonrpc"] = "2.0", ["id"] = 1, ["method"] = "tools/call",
            ["params"] = new JsonObject { ["name"] = tool, ["arguments"] = arguments ?? [] }
        }.ToJsonString();
        var result = server.HandleAsync(line).GetAwaiter().GetResult()!["result"]!;
        return (result["content"]![0]!["text"]!.GetValue<string>(), result["isError"]!.GetValue<bool>());
    }

    // H0-1 do estudo de 16/11: sem --project o servidor expunha todos os projetos autorizados; agora não sobe.
    [Fact]
    public void Sem_argumento_o_servidor_nao_sobe_e_diz_o_que_fazer()
    {
        var error = Assert.Throws<McpOptionsException>(() => McpOptions.Parse([]));

        Assert.Contains("--project", error.Message);
        Assert.Contains("nenhum projeto é exposto", error.Message);
    }

    [Fact]
    public void Cada_project_entra_uma_vez_no_escopo()
    {
        var scope = McpOptions.Parse(["--project", "p-aaaaaaaaaaaa", "--project", "p-bbbbbbbbbbbb", "--project", "p-aaaaaaaaaaaa"]).Scope;

        Assert.Equal(["p-aaaaaaaaaaaa", "p-bbbbbbbbbbbb"], scope.Order(StringComparer.Ordinal));
    }

    [Theory]
    [InlineData("--projeto", "p-aaaaaaaaaaaa")]
    [InlineData("--project")]
    [InlineData("--project", "../outro")]
    [InlineData("--project", "loja")]
    [InlineData("--project", "p-aaaaaaaaaaaa", "--verbose")]
    public void Argumento_que_nao_se_entende_derruba_em_vez_de_abrir_tudo(params string[] args)
    {
        Assert.Throws<McpOptionsException>(() => McpOptions.Parse(args));
    }

    [Fact]
    public void Mais_projetos_que_o_teto_e_recusado()
    {
        var args = Enumerable.Range(0, McpOptions.MaxProjects + 1).SelectMany(n => new[] { "--project", $"p-{n:x12}" }).ToArray();

        Assert.Throws<McpOptionsException>(() => McpOptions.Parse(args));
    }

    [Fact]
    public void Com_escopo_a_lista_mostra_so_os_projetos_da_conexao()
    {
        var (loja, folha) = Authorize();

        var (scoped, _) = Call(Server(McpScope.Of(loja)), "list_projects");
        var (empty, _) = Call(Server(McpScope.Of("p-ffffffffffff")), "list_projects");

        Assert.StartsWith("Projetos disponíveis para esta conexão:", scoped);
        Assert.Contains(loja, scoped);
        Assert.DoesNotContain(folha, scoped);
        Assert.DoesNotContain(loja, empty);
        Assert.DoesNotContain(folha, empty);
        Assert.Contains("Nenhum dos projetos desta conexão", empty);
    }

    [Fact]
    public void Projeto_fora_do_escopo_responde_como_se_nao_existisse_e_nao_entra_na_trilha()
    {
        var (loja, folha) = Authorize();
        new FileDecisionStore(_loja.DataDir).Save(new ManagementDecision("dec-000000000001", folha, Now, "Folha", "Decisão sigilosa da folha", "Motivo.", [], [], []));
        var server = Server(McpScope.Of(loja));

        var byId = Call(server, "decisions", new JsonObject { ["project"] = folha });
        var unknown = Call(server, "decisions", new JsonObject { ["project"] = "p-ffffffffffff" });

        Assert.True(byId.Failed);
        Assert.Equal(unknown.Text.Replace("p-ffffffffffff", folha, StringComparison.Ordinal), byId.Text);
        Assert.DoesNotContain("sigilosa", byId.Text);
        Assert.Empty(_audit.Entries);
    }

    [Fact]
    public void A_pasta_atual_de_um_projeto_fora_do_escopo_nao_o_escolhe()
    {
        var (loja, _) = Authorize();

        var (text, failed) = Call(Server(McpScope.Of(loja), _folha.Root), "project_briefing");
        var (own, ownFailed) = Call(Server(McpScope.Of(loja), _loja.Root), "project_briefing");

        Assert.True(failed);
        Assert.Contains("não é de um projeto disponível para esta conexão", text);
        Assert.False(ownFailed);
        Assert.Contains(Workspace().Projects().Single(project => project.ProjectId == loja).DisplayName, own);
    }

    // Validação real B2: o nome exibido passou a vir do manifesto; quem pedia o projeto pelo nome da pasta continua sendo atendido.
    [Fact]
    public void Projeto_e_achado_pelo_nome_do_manifesto_e_pelo_nome_da_pasta()
    {
        _loja.Write("package.json", "{\"name\": \"loja-virtual\"}");
        var (loja, _) = Authorize();
        var folder = Path.GetFileName(_loja.Root);

        var (byManifest, manifestFailed) = Call(Server(McpScope.Of(loja)), "project_briefing", new JsonObject { ["project"] = "loja-virtual" });
        var (byFolder, folderFailed) = Call(Server(McpScope.Of(loja)), "project_briefing", new JsonObject { ["project"] = folder });

        Assert.False(manifestFailed);
        Assert.False(folderFailed);
        Assert.Contains("# loja-virtual", byManifest);
        Assert.Contains(loja, byFolder);
    }

    // Revalidação 2026-11-15: o Claude Desktop não abre numa pasta de projeto, e com --project de um só o briefing falhava.
    [Fact]
    public void Fora_de_qualquer_projeto_o_escopo_de_um_so_vale_sem_o_argumento()
    {
        var (loja, folha) = Authorize();

        var (single, singleFailed) = Call(Server(McpScope.Of(loja)), "project_briefing");
        var (two, twoFailed) = Call(Server(McpScope.Of(loja, folha)), "project_briefing");

        Assert.False(singleFailed);
        Assert.Contains(Workspace().Projects().Single(project => project.ProjectId == loja).DisplayName, single);
        Assert.True(twoFailed);
        Assert.Contains("Informe 'project'", two);
    }

    [Fact]
    public void A_descricao_para_a_ide_diz_o_argumento_do_escopo()
    {
        var described = new IdeConnection("C:/spock/mariliz-runtime.exe").Describe();

        Assert.Equal(McpOptions.ProjectArgument, described["projectArgument"]!.GetValue<string>());
        Assert.True(Ids.IsProjectId("p-aaaaaaaaaaaa"));
    }
}
