using System.Text.Json;
using System.Text.Json.Nodes;
using Mariliz.Application.Compatibility;
using Mariliz.Application.Mcp;
using Mariliz.Application.Protocol;
using Mariliz.Application.Scaffold;
using Mariliz.Application.Tests.Support;
using Mariliz.Application.Workspace;
using Mariliz.Infrastructure;

namespace Mariliz.Application.Tests;

// MV-6: da arquitetura aprovada ao esqueleto do projeto. O catálogo é fixo aqui, para o plano não mudar a cada geração do
// catálogo embutido; o que se prova é que a mesma stack e o mesmo catálogo dão os mesmos comandos, e que a conferência lê
// os manifestos que o agente criou.
public sealed class ScaffoldTests : IDisposable
{
    private const string Folder = "loja-nova";
    private static readonly DateOnly Today = new(2026, 11, 24);
    private static readonly DateTimeOffset Now = new(2026, 11, 24, 12, 0, 0, TimeSpan.Zero);
    private readonly TempProject _project = new();
    private readonly WorkspaceAuthorizer _workspace;
    private readonly string _projectId;
    private readonly string _root;

    public ScaffoldTests()
    {
        _root = Path.Combine(_project.Root, Folder);
        Directory.CreateDirectory(_root);
        _workspace = new WorkspaceAuthorizer(new LocalWorkspaceFileSystem(), new FileWorkspaceRegistry(_project.DataDir));
        _projectId = _workspace.Authorize(_root).Project!.ProjectId;
    }

    public void Dispose() => _project.Dispose();

    private static readonly DateTimeOffset Checked = new(2026, 11, 19, 16, 0, 0, TimeSpan.Zero);

    private static readonly ReleaseCatalog Catalog = new(1, Checked,
    [
        new("dotnet", ".NET", "lifecycle", "major.minor", "https://builds.dotnet.microsoft.com/dotnet/release-metadata/releases-index.json", Checked, false, null, null,
        [
            new("11.0", "sts", "active", null, "2028-11-14", "11.0.0", "2026-11-10", "C# 15"),
            new("10.0", "lts", "active", null, "2028-11-14", "10.0.15", "2026-11-10", "C# 14")
        ]),
        new("node", "Node.js", "lifecycle", "major", "https://github.com/nodejs/Release/blob/main/schedule.json", Checked, false, null, null,
        [
            new("26", "lts", "active", null, "2029-04-30", "26.13.1", null, null),
            new("24", "lts", "maintenance", null, "2028-04-30", "24.20.0", null, null)
        ]),
        new("react", "React", "latest-major", "major", "https://registry.npmjs.org/react", Checked, false, null, null,
            [new("19", "stable", "active", null, null, "19.3.1", null, null)]),
        new("vite", "Vite", "latest-major", "major", "https://registry.npmjs.org/vite", Checked, false, null, null,
            [new("8", "stable", "active", null, null, "8.3.8", null, null, new Dictionary<string, string> { ["node"] = "^20.19.0 || >=22.12.0" })])
    ]);

    private void Architecture(params string[] stack) =>
        Write(".spock/arquitetura/arquitetura.md", "# Arquitetura\n\n**Estilo:** Monólito modular\n\nResumo.\n\n## Stack\n\n"
            + string.Concat(stack.Select(line => $"- {line}\n")) + "\n## Riscos\n\n- Risco. Mitigação: algo.\n");

    private void Write(string path, string content) => _project.Write($"{Folder}/{path}", content);

    private ProjectScaffold Read() => ScaffoldService.Read(new TestFilesFactory().Open(_root), Today, Catalog);

    private void FullStack() => Architecture(
        "**backend**: ASP.NET Core. Equipe .NET. Critério: pergunta 10.4 do catálogo, opção .NET, fatores F1.",
        "**frontend**: React com Vite e TypeScript. Painel interno.",
        "**dados**: PostgreSQL. Relacional.");

    [Fact]
    public void Sem_a_arquitetura_aprovada_nao_ha_plano()
    {
        var scaffold = Read();

        Assert.False(scaffold.Plan.Supported);
        Assert.Contains("Grave antes a especificação, a arquitetura e o backlog", Assert.Single(scaffold.Plan.Problems));
        Assert.Empty(scaffold.Plan.Steps);
        Assert.Empty(scaffold.Checks);
        Assert.False(scaffold.Done);
    }

    [Fact]
    public void Stack_dotnet_e_react_vira_comandos_oficiais_com_as_versoes_da_politica()
    {
        FullStack();
        var plan = Read().Plan;

        Assert.True(plan.Supported);
        Assert.Equal("LojaNova", plan.ProjectName);
        Assert.Equal([("dotnet", "10.0.15"), ("node", "26.13.1"), ("react", "19.3.1"), ("vite", "8.3.8")],
            plan.Technologies.Select(technology => (technology.Id, technology.Version)));
        Assert.Contains("a LTS ganha", plan.Technologies[0].Reason);
        Assert.Equal(["B1", "B2", "B3", "B4", "B5", "B6", "F1", "F2", "F3", "F4", "F5", "R1"], plan.Steps.Select(step => step.Id));
        var commands = plan.Steps.Where(step => step.Command is not null).ToDictionary(step => step.Id, step => (step.Directory, step.Command));
        Assert.Equal((".", "dotnet new sln -n LojaNova -o backend"), commands["B1"]);
        Assert.Equal(("backend", "dotnet new globaljson --sdk-version 10.0.100 --roll-forward latestFeature"), commands["B2"]);
        Assert.Equal(("backend", "dotnet new webapi -n LojaNova.Api -o src/LojaNova.Api -f net10.0"), commands["B3"]);
        Assert.Equal(("backend", "dotnet new xunit -n LojaNova.Api.Tests -o tests/LojaNova.Api.Tests -f net10.0"), commands["B4"]);
        Assert.Equal((".", "npm create vite@8 frontend -- --template react-ts"), commands["F1"]);
        Assert.Equal(("frontend", "npm install --save-exact react@19.3.1 react-dom@19.3.1"), commands["F4"]);
        Assert.Equal(("frontend", "npm install --save-exact --save-dev vite@8.3.8"), commands["F5"]);
        var nvmrc = plan.Steps.Single(step => step.Id == "F2");
        Assert.Equal((".nvmrc", "26.13.1\n"), (nvmrc.File, nvmrc.Content));
        Assert.Equal(["dotnet build", "dotnet test", "npm run build", "npm run lint"], plan.Verification.Select(step => step.Command));
        Assert.Contains(plan.Notes, note => note.StartsWith("dados: PostgreSQL não é criado aqui", StringComparison.Ordinal));
        Assert.Contains(plan.Notes, note => note.StartsWith("TypeScript fica na versão que o template do Vite traz", StringComparison.Ordinal));
    }

    [Fact]
    public void O_registro_diz_versao_motivo_fonte_e_data_de_cada_tecnologia()
    {
        FullStack();
        var record = JsonNode.Parse(Read().Plan.Record!)!;

        Assert.Equal("2026-11-19", record["catalogGeneratedAt"]!.GetValue<string>());
        var dotnet = record["technologies"]![0]!;
        Assert.Equal("10.0.15", dotnet["version"]!.GetValue<string>());
        Assert.Equal("lts", dotnet["channel"]!.GetValue<string>());
        Assert.Equal("https://builds.dotnet.microsoft.com/dotnet/release-metadata/releases-index.json", dotnet["source"]!.GetValue<string>());
        Assert.Equal("2026-11-19", dotnet["checkedAt"]!.GetValue<string>());
        Assert.Contains("LTS ativa mais nova", dotnet["reason"]!.GetValue<string>());
    }

    [Fact]
    public void Mesma_stack_e_mesmo_catalogo_dao_o_mesmo_plano()
    {
        FullStack();
        var first = Read().Plan;
        var second = Read().Plan;

        Assert.Equal(first.Record, second.Record);
        Assert.Equal(first.Steps, second.Steps);
    }

    [Fact]
    public void So_frontend_cria_so_o_frontend()
    {
        Architecture("**frontend**: React com Vite e TypeScript. Site de uma página.", "**infra**: Hospedagem estática com CDN. Custo mínimo.");
        var plan = Read().Plan;

        Assert.True(plan.Supported);
        Assert.False(plan.Backend);
        Assert.Equal(["node", "react", "vite"], plan.Technologies.Select(technology => technology.Id));
        Assert.DoesNotContain(plan.Steps, step => step.Part == ScaffoldPlans.Backend);
    }

    [Theory]
    [InlineData("**frontend**: Angular. Formulários pesados.", "Angular")]
    [InlineData("**backend**: Node com NestJS. Mesma linguagem.", "Node com NestJS")]
    [InlineData("**backend**: Python com FastAPI. ML.", "Python com FastAPI")]
    public void Stack_que_o_spock_ainda_nao_cria_e_recusada_com_o_motivo(string line, string choice)
    {
        Architecture("**frontend**: React com Vite. Painel.", line);
        var plan = Read().Plan;

        Assert.False(plan.Supported);
        Assert.Contains(plan.Problems, problem => problem.StartsWith($"{choice} (", StringComparison.Ordinal) && problem.Contains("ainda não é criado pelo Spock"));
        Assert.Empty(plan.Steps);
    }

    [Fact]
    public void TypeScript_como_linguagem_do_frontend_nao_e_outra_parte()
    {
        Architecture("**frontend**: React com Vite. Painel.", "**linguagem**: TypeScript. Tipagem no frontend.");

        Assert.True(Read().Plan.Supported);
    }

    [Theory]
    [InlineData("loja-nova", "LojaNova")]
    [InlineData("Barbearia São João", "BarbeariaSaoJoao")]
    [InlineData("2fa app", "App2faApp")]
    [InlineData("---", "App")]
    public void Nome_do_projeto_dotnet_vem_da_pasta(string folder, string expected) =>
        Assert.Equal(expected, ScaffoldPlans.ProjectName(folder));

    // O que o agente teria criado seguindo o plano; a conferência só olha estes arquivos.
    private void Scaffolded(ScaffoldPlan plan)
    {
        Write("backend/global.json", """{ "sdk": { "version": "10.0.100", "rollForward": "latestFeature" } }""");
        Write("backend/LojaNova.slnx", "<Solution>\n  <Project Path=\"src/LojaNova.Api/LojaNova.Api.csproj\" />\n  <Project Path=\"tests/LojaNova.Api.Tests/LojaNova.Api.Tests.csproj\" />\n</Solution>\n");
        Write("backend/src/LojaNova.Api/LojaNova.Api.csproj", "<Project Sdk=\"Microsoft.NET.Sdk.Web\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");
        Write("backend/tests/LojaNova.Api.Tests/LojaNova.Api.Tests.csproj",
            "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>" +
            "<ItemGroup><ProjectReference Include=\"..\\..\\src\\LojaNova.Api\\LojaNova.Api.csproj\" /></ItemGroup></Project>");
        Write("frontend/.nvmrc", "26.13.1\n");
        Write("frontend/package.json", """
            {"name": "frontend", "scripts": {"dev": "vite", "build": "tsc -b && vite build", "lint": "eslint ."},
             "dependencies": {"react": "19.3.1", "react-dom": "19.3.1"},
             "devDependencies": {"vite": "8.3.8", "typescript": "~5.9.3"}}
            """);
        Write("frontend/package-lock.json", "{}");
        // Outra indentação e outra quebra de linha: o registro é comparado pelo conteúdo.
        Write("spock-versions.json", JsonNode.Parse(plan.Record!)!.ToJsonString().Replace("\n", "\r\n"));
    }

    [Fact]
    public void A_conferencia_compara_os_manifestos_com_o_plano()
    {
        FullStack();
        var empty = Read();
        Scaffolded(empty.Plan);
        var done = Read();
        Write("frontend/package.json", """{"scripts": {"build": "x", "lint": "y"}, "dependencies": {"react": "^19.3.1", "react-dom": "19.3.1"}, "devDependencies": {"vite": "8.3.8"}}""");
        Write("backend/src/LojaNova.Api/LojaNova.Api.csproj", "<Project Sdk=\"Microsoft.NET.Sdk.Web\"><PropertyGroup><TargetFramework>net11.0</TargetFramework></PropertyGroup></Project>");
        Write("spock-versions.json", empty.Plan.Record!.Replace("10.0.15", "11.0.0"));
        var drifted = Read();

        Assert.Equal(["sdk", "api", "tests", "solution", "node", "react", "vite", "scripts", "lockfile", "record"], empty.Checks.Select(check => check.Id));
        Assert.All(empty.Checks, check => Assert.False(check.Passed, check.Id));
        Assert.All(done.Checks, check => Assert.True(check.Passed, $"{check.Id}: {check.Detail}"));
        Assert.True(done.Done);
        Assert.Equal(["api", "react", "record"], drifted.Checks.Where(check => !check.Passed).Select(check => check.Id));
        Assert.Equal("react ^19.3.1, react-dom 19.3.1", drifted.Checks.Single(check => check.Id == "react").Detail);
        Assert.Equal("Microsoft.NET.Sdk.Web net11.0", drifted.Checks.Single(check => check.Id == "api").Detail);
    }

    private ProjectScaffoldTools Tools() =>
        new(() => _workspace, new TestFilesFactory(), _root, McpScope.Of(_projectId), new FakeClock { UtcNow = Now }, catalog: Catalog);

    private McpTool Tool(string name) => Tools().Tools().Single(tool => tool.Info.Name == name);

    private static Task<string> Call(McpTool tool) => tool.Call(new McpCall([], "teste", CancellationToken.None));

    [Fact]
    public async Task Preparar_entrega_versoes_comandos_e_registro_sem_rodar_nada()
    {
        var unsupported = await Call(Tool(ProjectScaffoldTools.PrepareTool));
        FullStack();
        var text = await Call(Tool(ProjectScaffoldTools.PrepareTool));

        Assert.Contains("Grave antes a especificação", unsupported);
        Assert.Contains($"Raiz do projeto: {_root}", text);
        Assert.Contains("- .NET 10.0.15 (linha 10.0, lts, suporte até 2028-11-14):", text);
        Assert.Contains("B3. Criar a API (pasta backend)\n   dotnet new webapi -n LojaNova.Api -o src/LojaNova.Api -f net10.0", text);
        Assert.Contains("F2. Fixar o Node (pasta frontend)\n   Grave .nvmrc com exatamente:\n```\n26.13.1\n```", text);
        Assert.Contains("\"version\": \"19.3.1\"", text);
        Assert.Contains("Não implemente funcionalidade", text);
        Assert.DoesNotContain(Directory.EnumerateFileSystemEntries(_root), path => !path.EndsWith(".spock", StringComparison.Ordinal));
        Assert.All(Tools().Tools(), tool => Assert.False(tool.Info.OpenWorld));
    }

    [Fact]
    public async Task Conferir_diz_o_que_falta_e_pede_a_verificacao_quando_os_arquivos_conferem()
    {
        FullStack();
        var check = Tool(ProjectScaffoldTools.CheckTool);
        var nothing = await Call(check);
        Scaffolded(Read().Plan);
        var done = await Call(check);

        Assert.Contains("0 de 10 verificações passando", nothing);
        Assert.Contains("- frontend/.nvmrc fixa o Node 26.13.1 (arquivo ausente)", nothing);
        Assert.Contains("passa nas 10 verificações", done);
        Assert.Contains("Rode a verificação (backend: dotnet build; backend: dotnet test; frontend: npm run build; frontend: npm run lint)", done);
    }

    // A interface lê a mesma fixture no Zod.
    [Fact]
    public void Contrato_da_criacao_do_projeto_tem_fixture_gerada_pelo_motor()
    {
        FullStack();
        Write("frontend/.nvmrc", "26.13.1\n");
        var session = new EngineSession("test", scaffold: new ScaffoldService(_workspace, new TestFilesFactory(), new FakeClock { UtcNow = Now }, Catalog));
        var response = session.Handle(new JsonObject
        {
            ["v"] = 1, ["kind"] = "request", ["id"] = "t", ["method"] = ProtocolMethods.ProjectScaffold,
            ["params"] = new JsonObject { ["projectId"] = _projectId }
        }.ToJsonString()).Response;
        var result = response["result"]?.AsObject() ?? throw new InvalidOperationException(response.ToJsonString());

        var pretty = result.ToJsonString(new JsonSerializerOptions
        {
            WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        }).ReplaceLineEndings("\n");
        var path = ContractFiles.Path_("fixtures", "engineering", "scaffold.json");
        if (Environment.GetEnvironmentVariable("MARILIZ_UPDATE_GOLDEN") == "1" || !File.Exists(path))
        {
            File.WriteAllText(path, pretty + "\n");
            return;
        }

        Assert.True(pretty == File.ReadAllText(path).ReplaceLineEndings("\n").TrimEnd(),
            "project.scaffold: o resultado mudou. Se foi deliberado, regenere com MARILIZ_UPDATE_GOLDEN=1 e ajuste o Zod da interface.");
    }
}
