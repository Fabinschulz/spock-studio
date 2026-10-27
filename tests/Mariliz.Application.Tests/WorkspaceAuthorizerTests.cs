using System.Text.Json.Nodes;
using Mariliz.Application.Protocol;
using Mariliz.Application.Workspace;

namespace Mariliz.Application.Tests;

public class WorkspaceAuthorizerTests
{
    private static readonly string Root = Path.GetPathRoot(AppContext.BaseDirectory)!;
    private static readonly string Home = Path.Combine(Root, "home", "dev");

    private sealed class FakeFileSystem : IWorkspaceFileSystem
    {
        public HashSet<string> Existing { get; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> Unreadable { get; } = new(StringComparer.OrdinalIgnoreCase);
        public string HomeDirectory => Home;
        public string? ResolveDirectory(string path) => Existing.Contains(path) ? path : null;
        public bool CanEnumerate(string directory) => !Unreadable.Contains(directory);
    }

    private static (WorkspaceAuthorizer Auth, FakeFileSystem Fs) Create()
    {
        var fs = new FakeFileSystem();
        return (new WorkspaceAuthorizer(fs), fs);
    }

    [Fact]
    public void Pasta_inexistente_ou_vazia_e_recusada()
    {
        var (auth, _) = Create();

        Assert.Equal(WorkspaceError.NotFound, auth.Authorize(Path.Combine(Root, "nao-existe")).Error);
        Assert.Equal(WorkspaceError.NotFound, auth.Authorize("").Error);
        Assert.Equal(WorkspaceError.NotFound, auth.Authorize("   ").Error);
    }

    [Fact]
    public void Raiz_de_unidade_nao_e_um_projeto()
    {
        var (auth, fs) = Create();
        fs.Existing.Add(Root);

        Assert.Equal(WorkspaceError.NotAllowed, auth.Authorize(Root).Error);
    }

    [Fact]
    public void Pasta_sem_permissao_de_leitura_e_recusada()
    {
        var (auth, fs) = Create();
        var dir = Path.Combine(Home, "privado");
        fs.Existing.Add(dir);
        fs.Unreadable.Add(dir);

        Assert.Equal(WorkspaceError.NotReadable, auth.Authorize(dir).Error);
    }

    [Fact]
    public void Pasta_valida_e_autorizada_com_id_estavel_e_caminho_abreviado()
    {
        var (auth, fs) = Create();
        var dir = Path.Combine(Home, "Desktop", "MyApps", "backend", "EmpregaNetAPI");
        fs.Existing.Add(dir);

        var first = auth.Authorize(dir).Project!;
        var second = auth.Authorize(dir).Project!;

        Assert.Equal(first.ProjectId, second.ProjectId);
        Assert.Matches("^p-[0-9a-f]{12}$", first.ProjectId);
        Assert.Equal("EmpregaNetAPI", first.DisplayName);
        var sep = Path.DirectorySeparatorChar;
        Assert.Equal($"~{sep}…{sep}backend{sep}EmpregaNetAPI", first.DisplayPath);
        Assert.True(auth.IsAuthorized(first.ProjectId));
        Assert.False(auth.IsAuthorized("p-000000000000"));
    }

    [Fact]
    public void Caminho_curto_nao_e_abreviado_alem_do_til()
    {
        var (auth, fs) = Create();
        var dir = Path.Combine(Home, "projeto");
        fs.Existing.Add(dir);

        var sep = Path.DirectorySeparatorChar;
        Assert.Equal($"~{sep}projeto", auth.Authorize(dir).Project!.DisplayPath);
    }

    [Fact]
    public void Barra_final_nao_muda_o_projeto()
    {
        var (auth, fs) = Create();
        var dir = Path.Combine(Home, "app");
        fs.Existing.Add(dir + Path.DirectorySeparatorChar);
        fs.Existing.Add(dir);

        Assert.Equal(auth.Authorize(dir).Project!.ProjectId, auth.Authorize(dir + Path.DirectorySeparatorChar).Project!.ProjectId);
    }

    [Fact]
    public void Sessao_responde_workspace_authorize_respeitando_os_schemas()
    {
        var (auth, fs) = Create();
        var dir = Path.Combine(Home, "app");
        fs.Existing.Add(dir);
        var session = new EngineSession("0.1.0", workspace: auth);
        var request = new JsonObject
        {
            ["v"] = 1, ["kind"] = "request", ["id"] = "w1", ["method"] = "workspace.authorize",
            ["params"] = new JsonObject { ["path"] = dir }
        };
        Assert.True(ContractFiles.IsValid(
            ContractFiles.Schema("protocol", "methods", "workspace.authorize.params.schema.json"), request["params"]));

        var (response, _) = session.Handle(request.ToJsonString());

        Assert.True(ContractFiles.IsValid(ContractFiles.Schema("protocol", "envelope.v1.schema.json"), response));
        Assert.True(ContractFiles.IsValid(
            ContractFiles.Schema("protocol", "methods", "workspace.authorize.result.schema.json"), response["result"]));
    }

    [Fact]
    public void Fixtures_de_workspace_authorize_respeitam_os_schemas_de_metodo()
    {
        var req = ContractFiles.ReadFixture("valid", "request.workspace.authorize.json")!;
        var res = ContractFiles.ReadFixture("valid", "response.workspace.authorize.json")!;

        Assert.True(ContractFiles.IsValid(ContractFiles.Schema("protocol", "methods", "workspace.authorize.params.schema.json"), req["params"]));
        Assert.True(ContractFiles.IsValid(ContractFiles.Schema("protocol", "methods", "workspace.authorize.result.schema.json"), res["result"]));
    }

    [Theory]
    [InlineData("nao-existe", ProtocolErrorCodes.PathNotFound)]
    public void Erro_de_pasta_vira_codigo_estavel_no_protocolo(string folder, string code)
    {
        var (auth, _) = Create();
        var session = new EngineSession("0.1.0", workspace: auth);
        var line = new JsonObject
        {
            ["v"] = 1, ["kind"] = "request", ["id"] = "w2", ["method"] = "workspace.authorize",
            ["params"] = new JsonObject { ["path"] = Path.Combine(Root, folder) }
        }.ToJsonString();

        var (response, _) = session.Handle(line);

        Assert.Equal(code, response["error"]!["code"]!.GetValue<string>());
    }

    [Fact]
    public void Params_invalidos_geram_INVALID_PARAMS_e_sem_workspace_o_metodo_nao_existe()
    {
        var (auth, _) = Create();
        const string bad = "{\"v\":1,\"kind\":\"request\",\"id\":\"w3\",\"method\":\"workspace.authorize\",\"params\":{\"path\":5}}";

        Assert.Equal(ProtocolErrorCodes.InvalidParams, new EngineSession("0.1.0", workspace: auth).Handle(bad).Response["error"]!["code"]!.GetValue<string>());
        Assert.Equal(ProtocolErrorCodes.MethodNotFound, new EngineSession("0.1.0").Handle(bad).Response["error"]!["code"]!.GetValue<string>());
    }

    [Fact]
    public void Hello_anuncia_a_capacidade_somente_quando_o_workspace_esta_disponivel()
    {
        var (auth, _) = Create();
        const string hello = "{\"v\":1,\"kind\":\"request\",\"id\":\"h\",\"method\":\"engine.hello\",\"params\":{\"protocol\":[1],\"hostVersion\":\"x\"}}";

        var withWs = new EngineSession("0.1.0", workspace: auth).Handle(hello).Response["result"]!["capabilities"]!.AsArray();
        var without = new EngineSession("0.1.0").Handle(hello).Response["result"]!["capabilities"]!.AsArray();

        Assert.Contains("workspace.authorize", withWs.Select(n => n!.GetValue<string>()));
        Assert.Empty(without);
    }
}
