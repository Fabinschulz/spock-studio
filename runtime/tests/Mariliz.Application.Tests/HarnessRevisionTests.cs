using Mariliz.Core.Policy;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Mariliz.Application.Engineering;
using Mariliz.Application.Harness;
using Mariliz.Application.Protocol;
using Mariliz.Application.Tests.Support;
using Mariliz.Application.Workspace;
using Mariliz.Infrastructure;

namespace Mariliz.Application.Tests;

public class HarnessRevisionTests
{
    private static (EngineSession Session, string ProjectId) Session(Journey journey)
    {
        var workspace = new WorkspaceAuthorizer(new LocalWorkspaceFileSystem());
        var projectId = workspace.Authorize(journey.Project.Root).Project!.ProjectId;
        Assert.Equal(journey.ProjectId, projectId);
        var engine = LocalEngine.Create(journey.Project.DataDir, (_, _, _, _, _) => { }, rememberWorkspaces: false);
        var revisions = new HarnessRevisionService(workspace, new TestFilesFactory(), journey.Store);
        var profiles = new TechnologyProfileService(engine.Configure, null!, workspace, null!, new FakeClock());
        return (new EngineSession("test", profiles: profiles, revisions: revisions), projectId);
    }

    private static JsonNode Read(EngineSession session, string projectId)
    {
        var response = session.Handle(new JsonObject
        {
            ["v"] = 1, ["kind"] = "request", ["id"] = "t", ["method"] = "engineering.revisions",
            ["params"] = new JsonObject { ["projectId"] = projectId }
        }.ToJsonString()).Response;
        return response["result"] ?? throw new InvalidOperationException(response.ToJsonString());
    }

    private static async Task<Journey> AppliedThenDispensedThenReverted()
    {
        var project = TempProject.Dotnet();
        SpecificationFixture.Install(project);
        var journey = new Journey(project);
        await journey.ApplyAsync(await journey.PlanAsync());
        var dispense = await journey.Service.PlanAsync(journey.ProjectId,
            new PlanOptions("claude-code", ExcludeComponents: [Catalog.AgentCodeReviewer]), CancellationToken.None);
        var applied = await journey.ApplyAsync(dispense);
        await journey.RevertAsync(applied["runId"]!.GetValue<string>());
        return journey;
    }

    [Fact]
    public void Sem_execucoes_nao_ha_revisoes()
    {
        using var project = TempProject.Dotnet();
        var (session, projectId) = Session(new Journey(project));

        var result = Read(session, projectId);

        Assert.Empty(result["revisions"]!.AsArray());
        Assert.Null(result["appliedByEngine"]);
    }

    [Fact]
    public async Task Aplicar_dispensar_e_reverter_viram_tres_revisoes_com_o_que_cada_uma_mudou()
    {
        var journey = await AppliedThenDispensedThenReverted();
        using var _ = journey.Project;
        var (session, projectId) = Session(journey);

        var revisions = Read(session, projectId)["revisions"]!.AsArray();

        Assert.Equal([3, 2, 1], revisions.Select(r => r!["number"]!.GetValue<int>()));
        Assert.Equal(["revert", "apply", "apply"], revisions.Select(r => r!["kind"]!.GetValue<string>()));

        var first = revisions[2]!;
        Assert.Contains(first["components"]!.AsArray(), c => c!["id"]!.GetValue<string>() == Catalog.AgentCodeReviewer
            && c["created"]!.AsArray().Any(p => p!.GetValue<string>() == HarnessNaming.Spock.CodeReviewerAgent));
        Assert.DoesNotContain(first["components"]!.AsArray().SelectMany(c => c!["created"]!.AsArray()), p => p!.GetValue<string>() == HarnessNaming.Spock.Lock);

        var dispense = revisions[1]!;
        var byId = dispense["components"]!.AsArray().ToDictionary(c => c!["id"]!.GetValue<string>(), c => c!);
        Assert.Equal([HarnessNaming.Spock.CodeReviewerAgent], byId[Catalog.AgentCodeReviewer]["removed"]!.AsArray().Select(p => p!.GetValue<string>()));
        Assert.Equal([HarnessNaming.Spock.Declaration], byId[Catalog.ConfigDeclaration]["updated"]!.AsArray().Select(p => p!.GetValue<string>()));
        Assert.Equal(revisions[0]!["runId"]!.GetValue<string>(), dispense["revertedByRunId"]!.GetValue<string>());

        var revert = revisions[0]!;
        Assert.Equal(dispense["runId"]!.GetValue<string>(), revert["revertsRunId"]!.GetValue<string>());
        var restored = revert["components"]!.AsArray().Single(c => c!["id"]!.GetValue<string>() == Catalog.AgentCodeReviewer)!;
        Assert.Equal([HarnessNaming.Spock.CodeReviewerAgent], restored["created"]!.AsArray().Select(p => p!.GetValue<string>()));
    }

    [Fact]
    public async Task Resposta_das_revisoes_confere_com_a_fixture_do_contrato()
    {
        var journey = await AppliedThenDispensedThenReverted();
        using var _ = journey.Project;
        var (session, projectId) = Session(journey);

        var text = Read(session, projectId).ToJsonString(new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        }).Replace(projectId, "p-000000000001").ReplaceLineEndings("\n");
        text = Regex.Replace(text, "\"(appliedByEngine|currentEngine)\": \"[^\"]*\"", "\"$1\": \"0.0.0\"");
        text = Regex.Replace(text, "\"at\": \"[^\"]*\"", "\"at\": \"2026-11-11T12:00:00+00:00\"");
        var runIds = Regex.Matches(text, "\"runId\": \"([^\"]+)\"").Select(m => m.Groups[1].Value).ToList();
        for (var i = 0; i < runIds.Count; i++) text = text.Replace(runIds[i], $"run-{runIds.Count - i}");

        var path = ContractFiles.Path_("fixtures", "engineering", "revisions.json");
        if (Environment.GetEnvironmentVariable("MARILIZ_UPDATE_GOLDEN") == "1" || !File.Exists(path))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, text + "\n");
            return;
        }
        Assert.True(text == File.ReadAllText(path).ReplaceLineEndings("\n").TrimEnd(),
            "revisions: a resposta mudou. Se foi deliberado, regenere com MARILIZ_UPDATE_GOLDEN=1 e ajuste o Zod da interface.");
    }
}
