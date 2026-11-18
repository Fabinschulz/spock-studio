using Mariliz.Core.Policy;
using System.Text.Json;
using System.Text.Json.Nodes;
using Mariliz.Application.Engineering;
using Mariliz.Application.Harness;
using Mariliz.Application.Protocol;
using Mariliz.Application.Tests.Support;
using Mariliz.Application.Workspace;
using Mariliz.Infrastructure;

namespace Mariliz.Application.Tests;

// As fichas são texto escrito à mão, então os testes as prendem ao que o motor de fato grava e ao que cada ativo declara.
public class HarnessCatalogTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 11, 12, 0, 0, TimeSpan.Zero);

    private static readonly Dictionary<string, string> AssetOf = new()
    {
        [Catalog.AgentHarnessEngineer] = "agents/harness-engineer.md",
        [Catalog.AgentCodeReviewer] = "agents/code-reviewer.md",
        [Catalog.AgentVerifier] = "agents/verifier.md",
        [Catalog.SkillHarnessContract] = "skills/harness-contract/SKILL.md"
    };

    [Fact]
    public void Todo_componente_do_catalogo_tem_ficha_e_toda_ficha_e_de_um_componente()
    {
        using var document = JsonDocument.Parse(AssetLoader.Read(HarnessCatalogService.SheetsAsset));
        var written = document.RootElement.GetProperty("components").EnumerateArray().Select(item => item.GetProperty("id").GetString()!).ToList();

        Assert.Equal(Catalog.Components.Select(component => component.Id).Order(StringComparer.Ordinal), written.Order(StringComparer.Ordinal));
        Assert.Equal(written.Count, written.Distinct().Count());
        Assert.Equal(Catalog.Components.Select(component => component.Id), HarnessCatalogService.AllSheets.Select(sheet => sheet.Id));
    }

    [Fact]
    public void Nenhuma_ficha_tem_campo_vazio_nem_travessao()
    {
        foreach (var sheet in HarnessCatalogService.AllSheets)
        {
            var texts = new[] { sheet.Title, sheet.WhatItDoes, sheet.WhenItActs, sheet.Origin }.Concat(sheet.Reads).Concat(sheet.CanChange).ToList();
            Assert.All(texts, text => Assert.False(string.IsNullOrWhiteSpace(text), $"{sheet.Id} tem um campo vazio."));
            Assert.All(texts, text => Assert.DoesNotContain("\u2014", text));
            Assert.NotEmpty(sheet.Reads);
            Assert.NotEmpty(sheet.CanChange);
            Assert.NotEmpty(sheet.Files);
        }
    }

    [Fact]
    public void As_ferramentas_do_agente_vem_do_proprio_arquivo_do_agente_com_o_que_cada_uma_quer_dizer()
    {
        var reviewer = HarnessCatalogService.AllSheets.Single(sheet => sheet.Id == Catalog.AgentCodeReviewer);

        Assert.Equal(["Read", "Grep", "Glob", "Bash"], reviewer.Tools.Select(tool => tool.Name));
        Assert.Equal("ler arquivos", reviewer.Tools[0].Plain);
        Assert.Equal("rodar comandos no terminal", reviewer.Tools[3].Plain);
        Assert.All(HarnessCatalogService.AllSheets.Where(sheet => sheet.Kind != "agent"), sheet => Assert.Empty(sheet.Tools));
    }

    [Fact]
    public void O_que_a_ficha_diz_que_o_agente_altera_confere_com_as_ferramentas_que_ele_declara()
    {
        foreach (var sheet in HarnessCatalogService.AllSheets.Where(sheet => sheet.Kind == "agent"))
        {
            var writes = sheet.Tools.Any(tool => tool.Name is "Write" or "Edit" or "MultiEdit");
            var says = sheet.CanChange[0];
            if (writes) Assert.DoesNotMatch("^Nada", says);
            else Assert.StartsWith("Nada", says);
            // Um agente com Write só pode ser descrito como escrevendo o que a ficha nomeia: um arquivo, não "o projeto".
            if (writes) Assert.Contains("Escreve um único arquivo", says);
        }
    }

    [Fact]
    public void A_origem_so_diz_adaptado_quando_o_proprio_ativo_diz_que_e_adaptado()
    {
        foreach (var (id, asset) in AssetOf)
        {
            var sheet = HarnessCatalogService.AllSheets.Single(item => item.Id == id);
            var assetSaysAdapted = AssetLoader.Read(asset).Contains("Adapted from", StringComparison.Ordinal);

            Assert.Equal(assetSaysAdapted, sheet.Origin.StartsWith("Adaptado", StringComparison.Ordinal));
        }
    }

    [Fact]
    public void So_o_que_o_usuario_pode_dispensar_e_marcado_como_gerenciavel()
    {
        var byId = HarnessCatalogService.AllSheets.ToDictionary(sheet => sheet.Id);

        Assert.True(byId[Catalog.AgentVerifier].Manageable);
        Assert.True(byId[Catalog.HookPolicy].Manageable);
        Assert.False(byId[Catalog.SpecificationStructure].Manageable);
        Assert.False(byId[Catalog.ConfigDeclaration].Manageable);
    }

    private static async Task<TempProject> Applied()
    {
        var project = TempProject.Dotnet();
        SpecificationFixture.Install(project);
        var journey = new Journey(project);
        await journey.ApplyAsync(await journey.PlanAsync());
        return project;
    }

    private static HarnessCatalog ReadCatalog(TempProject project, out string projectId)
    {
        var workspace = new WorkspaceAuthorizer(new LocalWorkspaceFileSystem());
        projectId = workspace.Authorize(project.Root).Project!.ProjectId;
        var status = new HarnessStatusService(workspace, new TestFilesFactory(), new FakeClock { UtcNow = Now });
        return new HarnessCatalogService(status).Read(projectId);
    }

    [Fact]
    public async Task Os_arquivos_que_cada_ficha_cita_sao_os_que_o_harness_aplicado_registrou_no_lock()
    {
        using var project = await Applied();
        var workspace = new WorkspaceAuthorizer(new LocalWorkspaceFileSystem());
        var projectId = workspace.Authorize(project.Root).Project!.ProjectId;
        var status = new HarnessStatusService(workspace, new TestFilesFactory(), new FakeClock { UtcNow = Now }).Inspect(projectId);

        foreach (var component in status.Components.Where(component => Catalog.Find(component.Id) is not null && component.Id != Catalog.SpecificationStructure))
        {
            var sheet = HarnessCatalogService.AllSheets.Single(item => item.Id == component.Id);
            Assert.All(component.Files, file => Assert.Contains(file.Path, sheet.Files));
        }
    }

    [Fact]
    public async Task As_fichas_nao_vao_para_o_projeto_nem_mudam_o_que_e_gravado_nele()
    {
        using var project = await Applied();
        var texts = HarnessCatalogService.AllSheets.SelectMany(sheet => new[] { sheet.WhatItDoes, sheet.WhenItActs, sheet.Origin }).ToList();

        var everything = string.Join("\n", Directory.EnumerateFiles(project.Root, "*", SearchOption.AllDirectories)
            .Where(file => !file.Contains(Path.DirectorySeparatorChar + ".git" + Path.DirectorySeparatorChar)).Select(File.ReadAllText));

        Assert.All(texts, text => Assert.DoesNotContain(text, everything));
        Assert.False(File.Exists(Path.Combine(project.Root, ".claude", "catalog")));
        Assert.DoesNotContain("components.json", everything);
    }

    [Fact]
    public void Sem_projeto_o_catalogo_traz_so_as_fichas_sem_estado()
    {
        var catalog = new HarnessCatalogService(null).Read(null);

        Assert.Null(catalog.ProjectId);
        Assert.Null(catalog.Applied);
        Assert.Equal(Catalog.Components.Count, catalog.Entries.Count);
        Assert.All(catalog.Entries, entry => Assert.Equal((null, null), (entry.Presence, entry.State)));
    }

    [Fact]
    public void Projeto_sem_harness_aplicado_traz_tudo_como_nao_aplicado()
    {
        using var project = TempProject.Dotnet();

        var catalog = ReadCatalog(project, out var projectId);

        Assert.Equal(projectId, catalog.ProjectId);
        Assert.False(catalog.Applied);
        Assert.All(catalog.Entries, entry => Assert.Equal(("not_applied", null), (entry.Presence, entry.State)));
    }

    [Fact]
    public async Task Harness_aplicado_traz_cada_componente_com_o_estado_dos_arquivos_e_o_dispensado_como_excluido()
    {
        using var project = await Applied();
        project.Write(HarnessNaming.Spock.CodeReviewerAgent, project.Read(HarnessNaming.Spock.CodeReviewerAgent) + "\nregra minha\n");
        project.Write("spock.yaml", project.Read("spock.yaml")!.Replace("exclude: []", "exclude:\n  - mariliz.agent.verifier"));

        var catalog = ReadCatalog(project, out _);
        var byId = catalog.Entries.ToDictionary(entry => entry.Sheet.Id);

        Assert.True(catalog.Applied);
        Assert.Equal(("applied", "intact"), (byId[Catalog.AgentHarnessEngineer].Presence, byId[Catalog.AgentHarnessEngineer].State));
        Assert.Equal(("applied", "modified"), (byId[Catalog.AgentCodeReviewer].Presence, byId[Catalog.AgentCodeReviewer].State));
        // O verificador foi dispensado em spock.yaml, mas os arquivos continuam até a próxima aplicação.
        Assert.Equal(("excluded", "intact"), (byId[Catalog.AgentVerifier].Presence, byId[Catalog.AgentVerifier].State));
    }

    [Fact]
    public async Task Componente_que_nao_se_aplica_ao_projeto_aparece_como_nao_aplicado()
    {
        using var project = await Applied();

        var catalog = ReadCatalog(project, out _);

        // Projeto .NET puro: a regra de JavaScript não foi aplicada.
        Assert.Equal("not_applied", catalog.Entries.Single(entry => entry.Sheet.Id == Catalog.RuleStackJavascript).Presence);
        Assert.Null(catalog.Entries.Single(entry => entry.Sheet.Id == Catalog.RuleStackJavascript).State);
    }

    [Fact]
    public async Task Ler_o_catalogo_nao_escreve_nada_no_projeto()
    {
        using var project = await Applied();
        var before = project.Fingerprint();

        ReadCatalog(project, out _);

        Assert.Equal(before, project.Fingerprint());
    }

    [Fact]
    public void Projeto_nao_autorizado_e_recusado_e_o_metodo_traz_as_duas_formas()
    {
        using var project = TempProject.Dotnet();
        var workspace = new WorkspaceAuthorizer(new LocalWorkspaceFileSystem());
        var projectId = workspace.Authorize(project.Root).Project!.ProjectId;
        var engine = LocalEngine.Create(project.DataDir, (_, _, _, _, _) => { }, rememberWorkspaces: false);
        var harness = new HarnessStatusService(workspace, new TestFilesFactory(), new FakeClock { UtcNow = Now });
        var profiles = new TechnologyProfileService(engine.Configure, null!, workspace, null!, new FakeClock());
        var session = new EngineSession("test", profiles: profiles, harness: harness);

        JsonObject Call(JsonObject parameters) => session.Handle(new JsonObject
            { ["v"] = 1, ["kind"] = "request", ["id"] = "t", ["method"] = "engineering.catalog", ["params"] = parameters }.ToJsonString()).Response;

        Assert.Equal(Catalog.Components.Count, Call(new JsonObject())["result"]!["entries"]!.AsArray().Count);
        Assert.Equal(projectId, Call(new JsonObject { ["projectId"] = projectId })["result"]!["projectId"]!.GetValue<string>());
        Assert.Equal("PROJECT_NOT_AUTHORIZED", Call(new JsonObject { ["projectId"] = "p-desconhecido01" })["error"]!["code"]!.GetValue<string>());
    }

    [Fact]
    public async Task Fixture_de_contrato_do_catalogo()
    {
        using var project = await Applied();
        project.Write("spock.yaml", project.Read("spock.yaml")!.Replace("exclude: []", "exclude:\n  - mariliz.agent.verifier"));
        var catalog = ReadCatalog(project, out var projectId);

        var text = JsonSerializer.Serialize(catalog, Mariliz.Application.Integrations.IntegrationJson.Options);
        var node = JsonNode.Parse(text)!;
        var pretty = node.ToJsonString(new JsonSerializerOptions
        {
            WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        }).Replace(projectId, "p-000000000001").ReplaceLineEndings("\n");

        var path = ContractFiles.Path_("fixtures", "engineering", "catalog.json");
        if (Environment.GetEnvironmentVariable("MARILIZ_UPDATE_GOLDEN") == "1" || !File.Exists(path))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, pretty + "\n");
            return;
        }

        Assert.True(pretty == File.ReadAllText(path).ReplaceLineEndings("\n").TrimEnd(),
            "catalog: a resposta mudou. Se foi deliberado, regenere com MARILIZ_UPDATE_GOLDEN=1 e ajuste o Zod da interface.");
    }
}
