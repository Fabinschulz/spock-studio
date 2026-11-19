using Mariliz.Application.Compatibility;
using Mariliz.Application.Context;
using Mariliz.Application.Engineering;
using Mariliz.Application.Tests.Support;
using Mariliz.Application.Workflows;
using Mariliz.Core.Discovery;
using Mariliz.Infrastructure;
using Mariliz.Infrastructure.Engineering;

namespace Mariliz.Application.Tests;

// MV-1: o projeto existente é lido como está (versão por pasta) e cada versão ganha a situação de suporte pelo catálogo.
// O catálogo destes testes é fixo; o embutido muda a cada atualização e só é conferido no formato.
public sealed class CompatibilityTests : IDisposable
{
    private static readonly DateOnly Today = new(2026, 11, 18);

    private static readonly ReleaseCatalog Catalog = ReleaseCatalog.Parse("""
        {
          "schemaVersion": 1,
          "generatedAt": "2026-11-18T00:00:00Z",
          "technologies": [
            { "id": "dotnet", "name": ".NET", "policy": "lifecycle", "cycle": "major.minor", "source": "x", "checkedAt": "2026-11-18T00:00:00Z", "stale": false,
              "lines": [
                { "cycle": "11.0", "channel": "sts", "status": "active", "eol": "2028-11-14", "latest": "11.0.0", "language": "C# 15" },
                { "cycle": "10.0", "channel": "lts", "status": "active", "eol": "2028-11-14", "latest": "10.0.15", "language": "C# 14" },
                { "cycle": "9.0", "channel": "sts", "status": "active", "eol": "2027-05-12", "latest": "9.0.23", "language": "C# 13" },
                { "cycle": "8.0", "channel": "lts", "status": "active", "eol": "2026-11-10", "latest": "8.0.34", "language": "C# 12" },
                { "cycle": "6.0", "channel": "lts", "status": "eol", "eol": "2024-11-12", "latest": "6.0.36", "language": "C# 10" } ] },
            { "id": "node", "name": "Node.js", "policy": "lifecycle", "cycle": "major", "source": "x", "checkedAt": "2026-11-18T00:00:00Z", "stale": false,
              "lines": [
                { "cycle": "27", "channel": "preview", "status": "preview", "eol": "2030-04-30" },
                { "cycle": "26", "channel": "lts", "status": "active", "eol": "2029-04-30", "latest": "26.13.1" },
                { "cycle": "24", "channel": "lts", "status": "maintenance", "eol": "2028-04-30", "latest": "24.22.3" },
                { "cycle": "18", "channel": "lts", "status": "eol", "eol": "2025-04-30", "latest": "18.20.8" } ] },
            { "id": "python", "name": "Python", "policy": "lifecycle", "cycle": "major.minor", "source": "x", "checkedAt": "2026-11-18T00:00:00Z", "stale": false,
              "lines": [
                { "cycle": "3.15", "channel": "stable", "status": "active", "eol": "2031-10" },
                { "cycle": "3.14", "channel": "stable", "status": "active", "eol": "2030-10" },
                { "cycle": "3.10", "channel": "stable", "status": "maintenance", "eol": "2026-10" } ] },
            { "id": "go", "name": "Go", "policy": "supported-window", "cycle": "major.minor", "source": "x", "checkedAt": "2026-11-18T00:00:00Z", "stale": false,
              "lines": [
                { "cycle": "1.27", "channel": "stable", "status": "active", "latest": "1.27.4" },
                { "cycle": "1.26", "channel": "stable", "status": "active", "latest": "1.26.11" } ] },
            { "id": "java", "name": "Java", "policy": "vendor", "cycle": "major", "source": "x", "checkedAt": "2026-11-18T00:00:00Z", "stale": false,
              "lines": [
                { "cycle": "25", "channel": "lts", "status": "active" },
                { "cycle": "21", "channel": "lts", "status": "vendor" } ] },
            { "id": "react", "name": "React", "policy": "latest-major", "cycle": "major", "source": "x", "checkedAt": "2026-11-18T00:00:00Z", "stale": false,
              "lines": [
                { "cycle": "19", "channel": "stable", "status": "active", "latest": "19.3.1" },
                { "cycle": "17", "channel": "stable", "status": "unknown", "latest": "17.0.2" } ] },
            { "id": "vite", "name": "Vite", "policy": "latest-major", "cycle": "major", "source": "x", "checkedAt": "2026-11-18T00:00:00Z", "stale": false,
              "lines": [ { "cycle": "8", "channel": "stable", "status": "active", "latest": "8.3.8" } ] }
          ]
        }
        """);

    private readonly IntegrationHarness _h = new();

    public void Dispose() => _h.Dispose();

    private static ModuleCompatibility Assess(string technology, string? version, string origin = ProjectModules.Stack, Confidence confidence = Confidence.High) =>
        CompatibilityAssessor.Assess(new ProjectModule(".", technology, technology, version, origin, confidence, []), Catalog, Today);

    [Theory]
    [InlineData("dotnet", "net6.0", "6.0")]
    [InlineData("dotnet", "net8.0-windows", "8.0")]
    [InlineData("dotnet", "netcoreapp3.1", "3.1")]
    [InlineData("dotnet", "8.0.400", "8.0")]
    [InlineData("dotnet", "net48", null)]
    [InlineData("dotnet", "netstandard2.0", null)]
    [InlineData("node", "v18.20.4", "18")]
    [InlineData("react", "^17.0.2", "17")]
    [InlineData("python", ">=3.10", "3.10")]
    [InlineData("python", "3", null)]
    [InlineData("java", "1.8", "8")]
    [InlineData("go", "1.21.5", "1.21")]
    public void Versao_escrita_pelo_projeto_vira_a_linha_do_catalogo(string technology, string version, string? cycle) =>
        Assert.Equal(cycle, VersionCycles.Of(Catalog.Find(technology)!, version));

    [Theory]
    [InlineData("dotnet", "net11.0", SupportStatuses.Current)]
    [InlineData("dotnet", "net10.0", SupportStatuses.Current)]
    [InlineData("dotnet", "net9.0", SupportStatuses.Supported)]
    [InlineData("dotnet", "net8.0", SupportStatuses.EndOfLife)]
    [InlineData("dotnet", "net6.0", SupportStatuses.EndOfLife)]
    [InlineData("dotnet", "net12.0", SupportStatuses.Unknown)]
    [InlineData("dotnet", "net48", SupportStatuses.Unknown)]
    [InlineData("node", "26.1.0", SupportStatuses.Current)]
    [InlineData("node", "24", SupportStatuses.Maintenance)]
    [InlineData("node", "18", SupportStatuses.EndOfLife)]
    [InlineData("node", "27.0.0", SupportStatuses.Preview)]
    [InlineData("python", "3.15", SupportStatuses.Current)]
    [InlineData("python", "3.14", SupportStatuses.Supported)]
    [InlineData("python", "3.10", SupportStatuses.EndOfLife)]
    [InlineData("go", "1.26", SupportStatuses.Supported)]
    [InlineData("go", "1.21", SupportStatuses.EndOfLife)]
    [InlineData("java", "25", SupportStatuses.Current)]
    [InlineData("java", "21", SupportStatuses.Partial)]
    [InlineData("java", "1.8", SupportStatuses.Unknown)]
    [InlineData("react", "19.3.1", SupportStatuses.Current)]
    [InlineData("react", "17.0.2", SupportStatuses.Partial)]
    [InlineData("rust", "1.90", SupportStatuses.Unknown)]
    public void Cada_versao_ganha_a_situacao_de_suporte(string technology, string version, string status) =>
        Assert.Equal(status, Assess(technology, version).Status);

    [Fact]
    public void Sem_versao_declarada_a_situacao_e_desconhecida_e_o_agente_e_mandado_ler_os_manifestos()
    {
        var module = Assess("node", null);

        Assert.Equal(SupportStatuses.Unknown, module.Status);
        Assert.Contains("leia os manifestos", module.Guidance);
    }

    [Fact]
    public void Fim_de_suporte_com_so_o_mes_vale_ate_o_fim_dele_mesmo_que_a_fonte_ainda_diga_manutencao()
    {
        // Python 3.10: a fonte publica "2026-10"; em 18/11 o suporte já acabou.
        var module = Assess("python", "3.10");

        Assert.Equal((SupportStatuses.EndOfLife, "Legado sem suporte"), (module.Status, module.StatusLabel));
    }

    [Fact]
    public void A_orientacao_do_legado_fixa_a_versao_do_projeto_e_separa_a_modernizacao()
    {
        var module = Assess("dotnet", "net6.0");

        Assert.Contains("2024-11-12", module.Reason);
        Assert.Contains("net6.0 (C# 10)", module.Guidance);
        Assert.Contains("não use API, sintaxe, pacote nem opção de build de versão mais nova", module.Guidance);
        Assert.Contains("modernização à parte", module.Guidance);
        Assert.DoesNotContain("11.0", module.Guidance);
        Assert.Equal("11.0.0", module.NewestStable);
    }

    [Fact]
    public void Versao_de_faixa_declarada_pede_confirmacao_no_lockfile()
    {
        var module = Assess("react", "17.0.2", confidence: Confidence.Low);

        Assert.False(module.Confirmed);
        Assert.Contains("confirme no lockfile", module.Guidance);
    }

    [Fact]
    public void Framework_fora_do_catalogo_nao_entra_na_avaliacao_mas_stack_desconhecida_entra()
    {
        var report = CompatibilityAssessor.Assess(
        [
            new ProjectModule("web", "zod", "Zod", "4.6.5", ProjectModules.Framework, Confidence.Medium, []),
            new ProjectModule("web", "vite", "Vite", "8.3.2", ProjectModules.Framework, Confidence.Medium, []),
            new ProjectModule("svc", "rust", "Rust", "1.90", ProjectModules.Stack, Confidence.Medium, [])
        ], Catalog, new DateTimeOffset(2026, 11, 18, 0, 0, 0, TimeSpan.Zero));

        Assert.Equal(["vite", "rust"], report.Modules.Select(module => module.TechnologyId));
        Assert.False(report.CatalogStale);
    }

    [Fact]
    public void Catalogo_com_mais_de_90_dias_e_marcado_como_desatualizado()
    {
        var report = CompatibilityAssessor.Assess([], Catalog, new DateTimeOffset(2027, 3, 1, 0, 0, 0, TimeSpan.Zero));

        Assert.True(report.CatalogStale);
    }

    [Fact]
    public void Catalogo_embutido_e_legivel_e_nenhuma_linha_de_pre_lancamento_esta_ativa()
    {
        var catalog = ReleaseCatalog.Embedded;

        Assert.All(["dotnet", "node", "python", "go", "rust", "java", "react", "angular", "nextjs", "vite", "typescript"],
            id => Assert.NotNull(catalog.Find(id)));
        Assert.All(catalog.Technologies.SelectMany(technology => technology.Lines),
            line => Assert.False(line.Channel == "preview" && line.Status == "active"));
        Assert.All(catalog.Technologies, technology => Assert.StartsWith("https://", technology.Source));
    }

    [Fact]
    public void Formato_desconhecido_do_catalogo_e_recusado()
    {
        Assert.Throws<InvalidDataException>(() => ReleaseCatalog.Parse("""{ "schemaVersion": 2, "generatedAt": "2026-11-18T00:00:00Z", "technologies": [] }"""));
    }

    // ---------- descoberta: projetos de referência ----------

    private static async Task<IReadOnlyList<ProjectModule>> Modules(TempProject project)
    {
        var journey = new Journey(project);
        return ProjectModules.From(await journey.Service.DiscoverAsync(journey.ProjectId, CancellationToken.None));
    }

    private static string Csproj(string targets) =>
        $"<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework{(targets.Contains(';') ? "s" : "")}>{targets}</TargetFramework{(targets.Contains(';') ? "s" : "")}></PropertyGroup></Project>";

    [Fact]
    public async Task Projeto_legado_tem_cada_versao_lida_dos_proprios_arquivos()
    {
        using var project = new TempProject();
        project.Write("global.json", """{ "sdk": { "version": "6.0.428", "rollForward": "latestFeature" } }""");
        project.Write("api/Api.sln", "Microsoft Visual Studio Solution File, Format Version 12.00\n");
        project.Write("api/src/Api/Api.csproj", Csproj("net6.0"));
        project.Write("web/package.json", """{ "name": "web", "dependencies": { "react": "^17.0.2" }, "devDependencies": { "typescript": "~4.9.5" } }""");
        project.Write("web/.nvmrc", "v18.20.4\n");
        project.Write("worker/pyproject.toml", "[project]\nname = \"worker\"\nrequires-python = \">=3.10\"\n");

        var modules = await Modules(project);

        Assert.Contains(modules, module => (module.Root, module.TechnologyId, module.Version, module.Origin) == ("api", "dotnet", "net6.0", ProjectModules.Stack));
        Assert.Contains(modules, module => (module.Root, module.Name, module.Version, module.Origin) == ("api", ".NET SDK", "6.0.428", ProjectModules.Runtime));
        Assert.Contains(modules, module => (module.Root, module.TechnologyId, module.Version) == ("web", "react", "17.0.2"));
        Assert.Contains(modules, module => (module.Root, module.TechnologyId, module.Version, module.Origin) == ("web", "node", "18.20.4", ProjectModules.Runtime));
        Assert.Contains(modules, module => (module.Root, module.TechnologyId, module.Version) == ("web", "typescript", "4.9.5"));
        Assert.Contains(modules, module => (module.Root, module.TechnologyId, module.Version) == ("worker", "python", "3.10"));
        var nvmrc = Assert.Single(modules, module => module.TechnologyId == "node");
        Assert.Equal(["web/.nvmrc: v18.20.4"], nvmrc.Evidence);

        var report = CompatibilityAssessor.Assess(modules, Catalog, new DateTimeOffset(2026, 11, 18, 0, 0, 0, TimeSpan.Zero));
        Assert.All(report.Modules.Where(module => module.TechnologyId is "dotnet" or "node" or "python"),
            module => Assert.Equal(SupportStatuses.EndOfLife, module.Status));
    }

    [Fact]
    public async Task Monorepo_com_alvos_dotnet_diferentes_tem_uma_restricao_por_projeto()
    {
        using var project = new TempProject();
        project.Write("Plataforma.sln", "Microsoft Visual Studio Solution File, Format Version 12.00\n");
        project.Write("legado/Legado.csproj", Csproj("net6.0"));
        project.Write("novo/Novo.csproj", Csproj("net10.0"));
        project.Write("comum/Comum.csproj", Csproj("netstandard2.0;net8.0"));
        project.Write("painel/package.json", """{ "name": "painel", "dependencies": { "react": "^19.3.0" }, "engines": { "node": ">=26" } }""");

        var modules = await Modules(project);
        var projects = modules.Where(module => module.Name == ".NET (projeto)").ToDictionary(module => module.Root, module => module.Version);

        Assert.Equal(new Dictionary<string, string?> { ["comum"] = "netstandard2.0", ["legado"] = "net6.0", ["novo"] = "net10.0" }, projects);
        Assert.Equal("net10.0", Assert.Single(modules, module => module.TechnologyId == "dotnet" && module.Origin == ProjectModules.Stack).Version);
        var node = Assert.Single(modules, module => module.TechnologyId == "node");
        Assert.Equal(("painel", "26"), (node.Root, node.Version));
        Assert.Contains("mínimo aceito", node.Evidence[0]);

        var assessed = CompatibilityAssessor.Assess(modules, Catalog, new DateTimeOffset(2026, 11, 18, 0, 0, 0, TimeSpan.Zero)).Modules;
        Assert.Contains("compila para netstandard2.0", Assert.Single(assessed, module => module.Root == "comum").Guidance);
        Assert.Equal(SupportStatuses.EndOfLife, Assert.Single(assessed, module => module.Root == "legado").Status);
        Assert.Equal(SupportStatuses.Current, Assert.Single(assessed, module => module.Root == "novo").Status);
    }

    [Fact]
    public async Task Solucao_com_um_alvo_so_nao_repete_uma_linha_por_projeto()
    {
        using var project = new TempProject();
        project.Write("App.sln", "Microsoft Visual Studio Solution File, Format Version 12.00\n");
        project.Write("a/A.csproj", Csproj("net10.0"));
        project.Write("b/B.csproj", Csproj("net10.0"));

        var modules = await Modules(project);

        Assert.Equal("net10.0", Assert.Single(modules, module => module.TechnologyId == "dotnet").Version);
    }

    [Fact]
    public async Task Nvmrc_sem_numero_nao_inventa_versao()
    {
        using var project = new TempProject();
        project.Write("package.json", """{ "name": "app", "dependencies": { "react": "^19.3.0" } }""");
        project.Write(".nvmrc", "lts/*\n");

        var modules = await Modules(project);

        Assert.DoesNotContain(modules, module => module.TechnologyId == "node");
    }

    // ---------- o que o agente recebe ----------

    private ContextEngine Engine() => new(_h.Workspace, new HarnessStatusService(_h.Workspace, new TestFilesFactory(), _h.Clock),
        new FileTechnologyProfileStore(_h.DataDir), new TiktokenTokenizers(), _h.Clock, releases: Catalog);

    private void Profile(params ProjectModule[] modules)
    {
        _h.Clock.UtcNow = new DateTimeOffset(2026, 11, 18, 12, 0, 0, TimeSpan.Zero);
        new FileTechnologyProfileStore(_h.DataDir).Write(new TechnologyProfile(_h.ProjectId, _h.Clock.UtcNow,
            [new ProfileTechnology("dotnet", ".NET", "Plataforma e runtime", ["net6.0"], ["api"], Confidence.High, [])], [], [], [], [], [])
        {
            Modules = modules
        });
    }

    [Theory]
    [InlineData(ContextIntents.Bootstrap)]
    [InlineData(ContextIntents.Architecture)]
    public void Pacote_de_contexto_do_projeto_legado_traz_a_restricao_de_versao_e_nao_sugere_a_versao_nova(string intent)
    {
        Profile(
            new ProjectModule("api", "dotnet", ".NET", "net6.0", ProjectModules.Stack, Confidence.High, ["api/Api.csproj"]),
            new ProjectModule("web", "node", "Node.js", "18.20.4", ProjectModules.Runtime, Confidence.High, ["web/.nvmrc"]),
            new ProjectModule("web", "vite", "Vite", "4.5.0", ProjectModules.Framework, Confidence.Medium, ["web/package.json"]));

        var package = Engine().Compose(_h.ProjectId, intent);

        Assert.Contains("## Restrições de versão", package.Text);
        Assert.Contains("Em api: escreva código e configuração para .NET net6.0 (C# 10)", package.Text);
        Assert.Contains("Em web: escreva código e configuração para Node.js 18.20.4", package.Text);
        Assert.Contains("Vite 4.5.0 em web", package.Text);
        Assert.Contains("legado sem suporte", package.Text);
        Assert.DoesNotContain("11.0", package.Text);
        Assert.DoesNotContain("26.13", package.Text);
        Assert.All(package.Items.Where(item => item.Section == "versions"), item => Assert.Equal("spock", item.Source));
    }

    [Fact]
    public void Restricoes_de_versao_entram_na_versao_do_contexto()
    {
        Profile(new ProjectModule("api", "dotnet", ".NET", "net6.0", ProjectModules.Stack, Confidence.High, []));
        var before = Engine().Compose(_h.ProjectId, ContextIntents.Bootstrap).ContextVersion;

        Profile(new ProjectModule("api", "dotnet", ".NET", "net10.0", ProjectModules.Stack, Confidence.High, []));
        var after = Engine().Compose(_h.ProjectId, ContextIntents.Bootstrap).ContextVersion;

        Assert.NotEqual(before, after);
    }

    [Fact]
    public void Perfil_gravado_antes_das_restricoes_pede_nova_analise()
    {
        Profile();

        var package = Engine().Compose(_h.ProjectId, ContextIntents.Bootstrap);

        Assert.Contains("reanalise o projeto no Spock", package.Text);
    }
}
