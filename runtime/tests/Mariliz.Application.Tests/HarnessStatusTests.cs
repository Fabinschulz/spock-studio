using System.Text.Json.Nodes;
using Mariliz.Application.Engineering;
using Mariliz.Application.Harness;
using Mariliz.Application.Protocol;
using Mariliz.Application.Tests.Support;
using Mariliz.Application.Workspace;
using Mariliz.Infrastructure;

namespace Mariliz.Application.Tests;

public class HarnessStatusTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 11, 12, 0, 0, TimeSpan.Zero);

    private static (EngineSession Session, string ProjectId) Session(TempProject project)
    {
        var workspace = new WorkspaceAuthorizer(new LocalWorkspaceFileSystem());
        var projectId = workspace.Authorize(project.Root).Project!.ProjectId;
        var engine = LocalEngine.Create(project.DataDir, (_, _, _, _, _) => { }, rememberWorkspaces: false);
        var harness = new HarnessStatusService(workspace, new TestFilesFactory(), new FakeClock { UtcNow = Now });
        var profiles = new TechnologyProfileService(engine.Configure, null!, workspace, null!, new FakeClock());
        return (new EngineSession("test", profiles: profiles, harness: harness), projectId);
    }

    private static JsonNode Inspect(EngineSession session, string projectId)
    {
        var response = session.Handle(new JsonObject
        {
            ["v"] = 1, ["kind"] = "request", ["id"] = "t", ["method"] = "engineering.harness",
            ["params"] = new JsonObject { ["projectId"] = projectId }
        }.ToJsonString()).Response;
        return response["result"] ?? throw new InvalidOperationException(response.ToJsonString());
    }

    private static async Task Apply(TempProject project)
    {
        SpecificationFixture.Install(project);
        var journey = new Journey(project);
        var plan = await journey.PlanAsync();
        await journey.ApplyAsync(plan);
    }

    [Fact]
    public void Sem_lock_o_projeto_ainda_nao_tem_harness_aplicado()
    {
        using var project = TempProject.Dotnet();
        var (session, projectId) = Session(project);

        var status = Inspect(session, projectId);

        Assert.False(status["applied"]!.GetValue<bool>());
        Assert.True(status["lockReadable"]!.GetValue<bool>());
        Assert.Empty(status["components"]!.AsArray());
    }

    [Fact]
    public async Task Harness_recem_aplicado_esta_integro_e_cada_mudanca_a_mao_aparece_no_componente_certo()
    {
        using var project = TempProject.Dotnet();
        await Apply(project);
        var (session, projectId) = Session(project);
        var before = project.Fingerprint();

        var fresh = Inspect(session, projectId);
        Assert.True(fresh["applied"]!.GetValue<bool>());
        Assert.Equal("claude-code", fresh["aiEnvironment"]!.GetValue<string>());
        Assert.Equal(0, fresh["modified"]!.GetValue<int>() + fresh["missing"]!.GetValue<int>());
        Assert.All(fresh["components"]!.AsArray(), c => Assert.Equal("intact", c!["state"]!.GetValue<string>()));
        Assert.Contains(fresh["components"]!.AsArray(), c => c!["label"]!.GetValue<string>() == "Agente revisor de código");
        Assert.Equal(before, project.Fingerprint());

        project.Write(Catalog.PathCodeReviewer, project.Read(Catalog.PathCodeReviewer) + "\nregra minha\n");
        File.Delete(Path.Combine(project.Root, Catalog.PathRuleGeneral.Replace('/', Path.DirectorySeparatorChar)));
        project.Write(Catalog.PathSettings, "{}");

        var changed = Inspect(session, projectId);
        var byId = changed["components"]!.AsArray().ToDictionary(c => c!["id"]!.GetValue<string>(), c => c!["state"]!.GetValue<string>());
        Assert.Equal("modified", byId[Catalog.AgentCodeReviewer]);
        Assert.Equal("missing", byId[Catalog.RuleGeneral]);
        Assert.Equal("modified", byId[Catalog.HookPolicy]);
        Assert.Equal("missing", changed["components"]![0]!["state"]!.GetValue<string>());
        Assert.Equal((2, 1), (changed["modified"]!.GetValue<int>(), changed["missing"]!.GetValue<int>()));
    }

    [Fact]
    public async Task Arquivo_mantido_por_decisao_da_pessoa_aparece_como_sua_versao_e_editado_depois_volta_a_modificado()
    {
        using var project = TempProject.Dotnet();
        SpecificationFixture.Install(project);
        project.Write(Catalog.PathCodeReviewer, "---\nname: code-reviewer\ndescription: o meu\ntools: Read\n---\n\n# meu revisor\n");
        var journey = new Journey(project);
        await journey.ApplyAsync(await journey.PlanAsync());
        var (session, projectId) = Session(project);

        var kept = Inspect(session, projectId);
        var reviewer = kept["components"]!.AsArray().Single(c => c!["id"]!.GetValue<string>() == Catalog.AgentCodeReviewer)!;
        Assert.Equal("kept", reviewer["state"]!.GetValue<string>());
        Assert.Equal((1, 0), (kept["kept"]!.GetValue<int>(), kept["modified"]!.GetValue<int>()));
        Assert.Equal(kept["components"]!.AsArray().Count,
            kept["intact"]!.GetValue<int>() + kept["kept"]!.GetValue<int>() + kept["modified"]!.GetValue<int>() + kept["missing"]!.GetValue<int>());

        project.Write(Catalog.PathCodeReviewer, project.Read(Catalog.PathCodeReviewer) + "\nmais uma\n");
        var edited = Inspect(session, projectId);
        Assert.Equal((0, 1), (edited["kept"]!.GetValue<int>(), edited["modified"]!.GetValue<int>()));
    }

    [Fact]
    public async Task Lock_ilegivel_e_componente_excluido_aparecem_como_tais()
    {
        using var project = TempProject.Dotnet();
        await Apply(project);
        var (session, projectId) = Session(project);
        project.Write("mariliz.yaml", project.Read("mariliz.yaml")!.Replace("exclude: []", "exclude:\n  - mariliz.agent.verifier"));

        var withExclusion = Inspect(session, projectId);
        project.Write(ManagedLockFile.FileName, "isto: [não é yaml válido");
        var broken = Inspect(session, projectId);

        var excluded = Assert.Single(withExclusion["excluded"]!.AsArray())!;
        Assert.Equal(("mariliz.agent.verifier", "Agente verificador", true),
            (excluded["id"]!.GetValue<string>(), excluded["label"]!.GetValue<string>(), excluded["manageable"]!.GetValue<bool>()));
        Assert.True(broken["applied"]!.GetValue<bool>());
        Assert.False(broken["lockReadable"]!.GetValue<bool>());
        Assert.Empty(broken["components"]!.AsArray());
    }

    [Fact]
    public async Task Resposta_do_estado_do_harness_confere_com_a_fixture_do_contrato()
    {
        using var project = TempProject.Dotnet();
        await Apply(project);
        project.Write(Catalog.PathCodeReviewer, project.Read(Catalog.PathCodeReviewer) + "\nregra minha\n");
        var (session, projectId) = Session(project);

        var text = Inspect(session, projectId).ToJsonString(new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        }).Replace(projectId, "p-000000000001").ReplaceLineEndings("\n");
        text = System.Text.RegularExpressions.Regex.Replace(text, "\"engine\": \"[^\"]*\"", "\"engine\": \"0.0.0\"");

        var path = ContractFiles.Path_("fixtures", "engineering", "harness.json");
        if (Environment.GetEnvironmentVariable("MARILIZ_UPDATE_GOLDEN") == "1" || !File.Exists(path))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, text + "\n");
            return;
        }
        Assert.True(text == File.ReadAllText(path).ReplaceLineEndings("\n").TrimEnd(),
            "harness: a resposta mudou. Se foi deliberado, regenere com MARILIZ_UPDATE_GOLDEN=1 e ajuste o Zod da interface.");
    }
}
