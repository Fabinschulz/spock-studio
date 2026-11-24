using System.Text.Json;
using System.Text.Json.Nodes;
using Mariliz.Application.Analysis;
using Mariliz.Application.Compatibility;
using Mariliz.Application.Management;
using Mariliz.Application.Management.Assistant;
using Mariliz.Application.Mcp;
using Mariliz.Application.Protocol;
using Mariliz.Application.Tests.Support;
using Mariliz.Application.Workspace;
using Mariliz.Infrastructure;
using Mariliz.Infrastructure.Integrations;

namespace Mariliz.Application.Tests;

// MV-7: o plano de modernização com saltos de versão. O caminho sai do catálogo (fixo aqui), de LTS em LTS; o plano só
// aceita os trechos desse caminho, na ordem, cada um com a página oficial das mudanças incompatíveis. Um plano só de saltos
// não precisa da avaliação de arquitetura; o critério do roadmap é o .NET 6 → 8 → 10.
public sealed class ModernizationUpgradeTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 11, 24, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Checked = new(2026, 11, 19, 16, 0, 0, TimeSpan.Zero);
    private readonly TempProject _project = new();
    private readonly WorkspaceAuthorizer _workspace;
    private readonly string _projectId;
    private readonly FileDecisionStore _decisions;
    private readonly FileModernizationApprovalStore _approvals;
    private readonly EngineSession _session;

    private static readonly ReleaseCatalog Catalog = new(1, Checked,
    [
        new("dotnet", ".NET", "lifecycle", "major.minor", "https://builds.dotnet.microsoft.com/dotnet/release-metadata/releases-index.json", Checked, false, null, null,
        [
            new("12.0", "lts", "preview", null, null, "12.0.0-preview.1", null, "C# 16"),
            new("11.0", "sts", "active", null, "2028-11-14", "11.0.0", null, "C# 15"),
            new("10.0", "lts", "active", null, "2028-11-14", "10.0.15", null, "C# 14"),
            new("9.0", "sts", "maintenance", null, "2027-05-12", "9.0.11", null, "C# 13"),
            new("8.0", "lts", "eol", null, "2026-11-10", "8.0.22", null, "C# 12"),
            new("7.0", "sts", "eol", null, "2024-05-14", "7.0.20", null, "C# 11"),
            new("6.0", "lts", "eol", null, "2024-11-12", "6.0.36", null, "C# 10")
        ]),
        new("node", "Node.js", "lifecycle", "major", "https://github.com/nodejs/Release/blob/main/schedule.json", Checked, false, null, null,
        [
            new("26", "lts", "active", null, "2029-04-30", "26.13.1", null, null),
            new("25", "current", "eol", null, "2026-06-01", "25.9.0", null, null),
            new("24", "lts", "maintenance", null, "2028-04-30", "24.20.0", null, null),
            new("22", "lts", "maintenance", null, "2027-04-30", "22.21.0", null, null),
            new("20", "lts", "eol", null, "2026-04-30", "20.19.5", null, null),
            new("18", "lts", "eol", null, "2025-04-30", "18.20.8", null, null)
        ]),
        new("react", "React", "latest-major", "major", "https://registry.npmjs.org/react", Checked, false, null, null,
        [
            new("19", "stable", "active", null, null, "19.3.1", null, null),
            new("18", "stable", "eol", null, null, "18.3.1", null, null),
            new("17", "stable", "eol", null, null, "17.0.2", null, null)
        ])
    ]);

    public ModernizationUpgradeTests()
    {
        _project.Write("api/Api.sln", "Microsoft Visual Studio Solution File, Format Version 12.00\n");
        _project.Write("api/src/Api/Api.csproj", "<Project Sdk=\"Microsoft.NET.Sdk.Web\"><PropertyGroup><TargetFramework>net6.0</TargetFramework></PropertyGroup></Project>");
        _project.Write("api/src/Api/Program.cs", "var app = WebApplication.Create();\napp.Run();\n");
        _project.Write("web/package.json", """{ "name": "web", "dependencies": { "react": "17.0.2", "react-dom": "17.0.2" } }""");
        _project.Write("web/.nvmrc", "v18.20.4\n");
        _workspace = new WorkspaceAuthorizer(new LocalWorkspaceFileSystem(), new FileWorkspaceRegistry(_project.DataDir));
        _projectId = _workspace.Authorize(_project.Root).Project!.ProjectId;
        _decisions = new FileDecisionStore(_project.DataDir);
        _approvals = new FileModernizationApprovalStore(_project.DataDir);
        var clock = new FakeClock { UtcNow = Now };
        var decisions = new DecisionService(_workspace, _decisions, new FileRunStore(_project.DataDir),
            projectId => new EvidencePack(projectId, "Svc", Now, [], []), new MemoryAudit(), clock);
        _session = new EngineSession("test", workspace: _workspace,
            modernization: new ModernizationService(_workspace, new TestFilesFactory(), clock, decisions, _approvals, Catalog));
    }

    public void Dispose() => _project.Dispose();

    private ProjectModernization Read() =>
        ModernizationService.Read(_projectId, new TestFilesFactory().Open(_project.Root), Now, _approvals.List(_projectId), Catalog);

    private IReadOnlyList<VersionFinding> Versions() => Read().Versions;

    private static JsonObject Upgrade(string id, string from, string to, string guide, params string[] dependsOn) => new()
    {
        ["id"] = id,
        ["title"] = $"Subir o .NET de {from} para {to}",
        ["kind"] = ModernizationStepKinds.Upgrade,
        ["upgrade"] = new JsonObject { ["finding"] = "V1", ["from"] = from, ["to"] = to, ["breakingChanges"] = guide },
        ["change"] = $"Trocar o TargetFramework para net{to} e tratar o que a página de mudanças incompatíveis aponta para ASP.NET Core.",
        ["files"] = new JsonArray("api/src/Api/Api.csproj"),
        ["dependsOn"] = new JsonArray(dependsOn.Select(item => (JsonNode)JsonValue.Create(item)!).ToArray()),
        ["undo"] = $"Voltar o TargetFramework para net{from}.",
        ["verification"] = "dotnet build e dotnet test passam; a API sobe e responde no endpoint de saúde.",
        ["risk"] = "APIs obsoletas removidas na linha nova."
    };

    private static JsonObject Plan(params JsonObject[] steps) => new()
    {
        ["goal"] = "Sair do .NET 6, sem suporte, para o .NET 10 LTS.",
        ["characterization"] = new JsonArray(new JsonObject
        {
            ["behavior"] = "A API sobe e responde.", ["files"] = new JsonArray("api/src/Api/Program.cs"), ["test"] = "Teste de fumaça que sobe a API."
        }),
        ["steps"] = new JsonArray(steps.Select(step => (JsonNode)step).ToArray())
    };

    private static JsonObject Valid() => Plan(
        Upgrade("U1", "6.0", "8.0", "https://learn.microsoft.com/dotnet/core/compatibility/8.0"),
        Upgrade("U2", "8.0", "10.0", "https://learn.microsoft.com/dotnet/core/compatibility/10.0", "U1"));

    private void Write(JsonObject plan) => _project.Write(".spock/modernization-plan.json", plan.ToJsonString());

    private IReadOnlyList<string> Failed() => Read().Plan!.Checks.Where(check => !check.Passed).Select(check => check.Id).ToList();

    [Fact]
    public void Achados_de_versao_saem_do_catalogo_com_o_caminho_de_LTS_em_LTS()
    {
        var versions = Versions();

        Assert.Equal([("V1", "api", "dotnet", "6.0", "10.0"), ("V2", "web", "node", "18", "26"), ("V3", "web", "react", "17", "19")],
            versions.Select(finding => (finding.Id, finding.Root, finding.TechnologyId, finding.Cycle, finding.Target)));
        Assert.Equal([("6.0", "8.0", "https://learn.microsoft.com/dotnet/core/compatibility/8.0"), ("8.0", "10.0", "https://learn.microsoft.com/dotnet/core/compatibility/10.0")],
            versions[0].Hops.Select(hop => (hop.From, hop.To, hop.Guide)));
        Assert.Equal(["20", "22", "24", "26"], versions[1].Hops.Select(hop => hop.To));
        Assert.Equal("https://github.com/nodejs/node/blob/main/doc/changelogs/CHANGELOG_V20.md", versions[1].Hops[0].Guide);
        // Sem LTS, de linha em linha.
        Assert.Equal([("17", "18", "https://react.dev/blog/2022/03/08/react-18-upgrade-guide"), ("18", "19", "https://react.dev/blog/2024/04/25/react-19-upgrade-guide")],
            versions[2].Hops.Select(hop => (hop.From, hop.To, hop.Guide)));
        Assert.Equal(SupportStatuses.EndOfLife, versions[0].Status);
    }

    [Fact]
    public void Plano_so_de_saltos_dotnet_6_8_10_passa_sem_avaliacao_de_arquitetura()
    {
        Write(Valid());
        var plan = Read().Plan!;

        Assert.All(plan.Checks, check => Assert.True(check.Passed, $"{check.Id}: {check.Detail}"));
        Assert.Contains(plan.Checks, check => check.Id == "upgrade-path");
        Assert.Equal(21, plan.Checks.Count);
        Assert.Equal(new ModernizationUpgrade("V1", "8.0", "10.0", "https://learn.microsoft.com/dotnet/core/compatibility/10.0"), plan.Steps[1].Upgrade);
    }

    public static TheoryData<string, string, Func<JsonObject>> WrongPlans => new()
    {
        { "pula a LTS do meio", "upgrade-path", () => Plan(Upgrade("U1", "6.0", "10.0", "https://learn.microsoft.com/dotnet/core/compatibility/10.0")) },
        { "para numa STS", "upgrade-path", () => Plan(Upgrade("U1", "6.0", "7.0", "https://learn.microsoft.com/dotnet/core/compatibility/7.0")) },
        { "vai para o pré-lançamento", "upgrade-path", () => Plan(
            Upgrade("U1", "6.0", "8.0", "https://learn.microsoft.com/dotnet/core/compatibility/8.0"),
            Upgrade("U2", "8.0", "10.0", "https://learn.microsoft.com/dotnet/core/compatibility/10.0", "U1"),
            Upgrade("U3", "10.0", "12.0", "https://learn.microsoft.com/dotnet/core/compatibility/12.0", "U2")) },
        { "começa do meio do caminho", "upgrade-chain", () => Plan(Upgrade("U1", "8.0", "10.0", "https://learn.microsoft.com/dotnet/core/compatibility/10.0")) },
        { "segundo salto sem depender do primeiro", "upgrade-chain", () => Plan(
            Upgrade("U1", "6.0", "8.0", "https://learn.microsoft.com/dotnet/core/compatibility/8.0"),
            Upgrade("U2", "8.0", "10.0", "https://learn.microsoft.com/dotnet/core/compatibility/10.0")) },
        { "saltos fora de ordem", "upgrade-chain", () => Plan(
            Upgrade("U2", "8.0", "10.0", "https://learn.microsoft.com/dotnet/core/compatibility/10.0"),
            Upgrade("U1", "6.0", "8.0", "https://learn.microsoft.com/dotnet/core/compatibility/8.0")) },
        { "página que não é a oficial", "upgrade-source", () => Plan(
            Upgrade("U1", "6.0", "8.0", "https://blog.exemplo.dev/dotnet-8-o-que-muda"),
            Upgrade("U2", "8.0", "10.0", "https://learn.microsoft.com/dotnet/core/compatibility/10.0", "U1")) },
        { "achado que não existe", "upgrade-finding", () =>
            {
                var step = Upgrade("U1", "6.0", "8.0", "https://learn.microsoft.com/dotnet/core/compatibility/8.0");
                step["upgrade"]!["finding"] = "V9";
                return Plan(step);
            } },
        { "passo estrutural sem avaliação", "assessment", () =>
            {
                var plan = Valid();
                plan["steps"]!.AsArray().Add(new JsonObject
                {
                    ["id"] = "S1", ["title"] = "Separar a camada de aplicação", ["kind"] = "incremental", ["divergences"] = new JsonArray("D1"),
                    ["change"] = "Extrair os casos de uso.", ["files"] = new JsonArray("api/src/Api/Program.cs"), ["dependsOn"] = new JsonArray("U2"),
                    ["undo"] = "Reverter.", ["verification"] = "Testes passam.", ["risk"] = "Baixo."
                });
                return plan;
            } },
        { "salto sem critério de aceite", "verification", () =>
            {
                var plan = Valid();
                plan["steps"]![0]!["verification"] = "";
                return plan;
            } }
    };

    [Theory]
    [MemberData(nameof(WrongPlans))]
    public void Cada_plano_de_saltos_errado_cai_na_verificacao_certa(string caso, string expected, Func<JsonObject> plan)
    {
        Write(plan());

        Assert.True(Failed().Contains(expected), $"{caso}: falhou {string.Join(", ", Failed())}");
    }

    [Fact]
    public void Guias_oficiais_por_tecnologia_e_linha()
    {
        var vite = new CatalogTechnology("vite", "Vite", "latest-major", "major", "https://registry.npmjs.org/vite", Checked, false, null, null,
            [new("8", "stable", "active", null, null, "8.3.8", null, null), new("7", "stable", "eol", null, null, "7.3.0", null, null)]);
        var python = new CatalogTechnology("python", "Python", "lifecycle", "major.minor", "https://peps.python.org/api/release-cycle.json", Checked, false, null, null, []);

        Assert.Equal("https://vite.dev/guide/migration", UpgradeGuides.For(vite, "8"));
        Assert.Equal("https://v7.vite.dev/guide/migration", UpgradeGuides.For(vite, "7"));
        Assert.Equal("https://docs.python.org/3/whatsnew/3.14.html", UpgradeGuides.For(python, "3.14"));
        Assert.Null(UpgradeGuides.For(Catalog.Find("react")!, "20"));
    }

    [Fact]
    public void Salto_aprovado_so_depois_do_anterior_e_cada_decisao_vai_para_a_memoria()
    {
        Write(Valid());
        var second = Decide("U2");
        Decide("U1");
        Decide("U2");

        Assert.Equal("MODERNIZATION_DEPENDENCY_PENDING", second["error"]!["code"]!.GetValue<string>());
        Assert.All(Read().Steps, state => Assert.Equal(ModernizationStepStatuses.Approved, state.Status));
        Assert.Equal(2, _decisions.List(_projectId).Count);
    }

    private JsonObject Decide(string stepId) => _session.Handle(new JsonObject
    {
        ["v"] = 1, ["kind"] = "request", ["id"] = "t", ["method"] = ProtocolMethods.ModernizationDecide,
        ["params"] = new JsonObject { ["projectId"] = _projectId, ["stepId"] = stepId, ["decision"] = "approved" }
    }.ToJsonString()).Response;

    private ModernizationPlanTools Tools() =>
        new(() => _workspace, new TestFilesFactory(), _project.Root, McpScope.Of(_projectId), _approvals, clock: new FakeClock { UtcNow = Now }, catalog: Catalog);

    [Fact]
    public async Task Preparar_sem_avaliacao_entrega_os_saltos_com_as_paginas_oficiais()
    {
        var text = await Tools().Tools().Single(tool => tool.Info.Name == ModernizationPlanTools.PrepareTool)
            .Call(new McpCall([], "teste", CancellationToken.None));

        Assert.Contains("Sem avaliação de arquitetura válida: o plano pode ter só saltos de versão.", text);
        Assert.Contains("- V1 api: .NET net6.0 (Legado sem suporte, fim do suporte 2024-11-12). Recomendada: 10.0. Caminho: 6.0 → 8.0 → 10.0", text);
        Assert.Contains("  6.0 → 8.0: https://learn.microsoft.com/dotnet/core/compatibility/8.0", text);
        Assert.Contains("\"upgrade\": {\"finding\": \"V1\"", text);
        Assert.DoesNotContain("## Divergências", text);
    }

    // A interface lê a mesma fixture no Zod: os achados de versão e um plano com os dois saltos, o primeiro aprovado.
    [Fact]
    public void Contrato_do_plano_com_saltos_tem_fixture_gerada_pelo_motor()
    {
        Write(Valid());
        Decide("U1");
        var result = _session.Handle(new JsonObject
        {
            ["v"] = 1, ["kind"] = "request", ["id"] = "t", ["method"] = ProtocolMethods.ModernizationPlan,
            ["params"] = new JsonObject { ["projectId"] = _projectId }
        }.ToJsonString()).Response["result"]!.AsObject();
        foreach (var state in result["modernization"]!["steps"]!.AsArray())
        {
            if (state!["decisionId"] is not null) state["decisionId"] = "dec-000000000001";
        }

        var pretty = result.ToJsonString(new JsonSerializerOptions
        {
            WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        }).ReplaceLineEndings("\n");
        var path = ContractFiles.Path_("fixtures", "engineering", "modernization-upgrade.json");
        if (Environment.GetEnvironmentVariable("MARILIZ_UPDATE_GOLDEN") == "1" || !File.Exists(path))
        {
            File.WriteAllText(path, pretty + "\n");
            return;
        }

        Assert.True(pretty == File.ReadAllText(path).ReplaceLineEndings("\n").TrimEnd(),
            "modernization.plan com saltos: o resultado mudou. Se foi deliberado, regenere com MARILIZ_UPDATE_GOLDEN=1 e ajuste o Zod da interface.");
    }
}