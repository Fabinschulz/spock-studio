using System.Text.Json.Nodes;
using Mariliz.Application.Engineering;
using Mariliz.Application.Harness;
using Mariliz.Application.Integrations;
using Mariliz.Application.Protocol;
using Mariliz.Application.Tests.Support;
using Mariliz.Core.Discovery;
using Mariliz.Infrastructure;
using Mariliz.Infrastructure.Engineering;
using Mariliz.Infrastructure.Integrations;

namespace Mariliz.Application.Tests;

/// <summary>Perfil tecnológico: montagem a partir da descoberta, histórico entre análises e o fluxo pelo protocolo.</summary>
public class TechnologyProfileTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 11, 12, 0, 0, TimeSpan.Zero);

    private static StackFinding Stack(string id, string name, string root, string? version, Confidence confidence = Confidence.High) =>
        new(id, name, root, version, confidence, [new Evidence($"{(root.Length == 0 ? "" : root + "/")}{id}.manifest")], []);

    private static DiscoveryReport Report(IReadOnlyList<StackFinding> stacks, IReadOnlyList<ToolingFinding>? tooling = null) =>
        new("p-000000000001", "loja", stacks, tooling ?? [], [new AiEnvironmentFinding("claude-code", "Claude Code", true, Confidence.High, [])],
            new HarnessInventory([], [], [], [], [], []), new SpecificationStatus(SpecificationState.NotInstalled, null, null, null, false, []),
            new GitState(true, false, []), new ToolAvailability(null, null, null, null), false, []);

    private static ProjectIntegration Integration(string provider, string resource) =>
        new("pint-000000000001", "p-000000000001", "conn-000000000001", provider, new DataScope(resource, resource, [DataKinds.Issues]),
            ContextualizationPolicy.Closed, Now);

    [Fact]
    public void Perfil_junta_pastas_da_mesma_tecnologia_e_diz_como_o_harness_atende_cada_uma()
    {
        var report = Report([
            Stack("dotnet", ".NET", "backend", "net10.0"),
            Stack("dotnet", ".NET", "tools", "net8.0", Confidence.Medium),
            Stack("react", "React", "web", "^19.0.0"),
            Stack("flutter", "Flutter", "app", null, Confidence.Low)
        ], [new ToolingFinding("test", "dotnet test", "dotnet test", []), new ToolingFinding("ci", "GitHub Actions", null, [])]);

        var profile = ProfileBuilder.Build(report, [Integration("jira", "APP"), Integration("github", "acme/loja")], null, Now);

        var dotnet = profile.Technologies.Single(t => t.Id == "dotnet");
        // A tecnologia existe com a melhor evidência entre as pastas; a evidência fraca de uma pasta não a torna duvidosa.
        Assert.Equal(["net10.0", "net8.0"], dotnet.Versions);
        Assert.Equal(["backend", "tools"], dotnet.Roots);
        Assert.Equal((Confidence.High, "Plataforma e runtime"), (dotnet.Confidence, dotnet.Category));
        Assert.Empty(profile.Technologies.Single(t => t.Id == "flutter").Versions);
        Assert.Equal(Catalog.RuleStackDotnet, profile.Harness.Single(h => h.Technology == ".NET").RuleId);
        Assert.Equal(Catalog.RuleStackJavascript, profile.Harness.Single(h => h.Technology == "React").RuleId);
        Assert.False(profile.Harness.Single(h => h.Technology == "Flutter").Covered);
        Assert.Equal([("Gestão de trabalho", "APP"), ("Repositório", "acme/loja")], profile.Connections.Select(c => (c.Role, c.Resource)));
        Assert.Equal(["Claude Code"], profile.AiEnvironments);
        var created = Assert.Single(profile.History);
        Assert.Equal(("created", "3 tecnologias"), (created.Kind, created.To));
    }

    [Fact]
    public void Nova_analise_registra_o_que_entrou_saiu_e_mudou_de_versao_antes_do_historico_antigo()
    {
        var first = ProfileBuilder.Build(Report([Stack("dotnet", ".NET", "", "net8.0"), Stack("react", "React", "web", "^18.0.0")],
            [new ToolingFinding("ci", "GitHub Actions", null, [])]), [Integration("github", "acme/loja")], null, Now.AddDays(-7));

        var second = ProfileBuilder.Build(Report([Stack("dotnet", ".NET", "", "net10.0"), Stack("nextjs", "Next.js", "web", "^16.0.1")],
            [new ToolingFinding("ci", "GitHub Actions", null, []), new ToolingFinding("test", "vitest", "pnpm run test", [])]), [], first, Now);

        Assert.Equal(
        [
            ("added", "Next.js", null, "^16.0.1"),
            ("removed", "React", "^18.0.0", null),
            ("changed", ".NET", "net8.0", "net10.0"),
            ("added", "Ferramenta", null, "vitest (pnpm run test)"),
            ("removed", "Conexão", "Repositório: acme/loja", null),
            ("created", "Perfil", null, "2 tecnologias")
        ], second.History.Select(c => (c.Kind, c.Subject, c.From, c.To)));
        Assert.All(second.History.Take(5), change => Assert.Equal(Now, change.At));
    }

    [Fact]
    public void Analise_sem_mudanca_nao_cria_historico()
    {
        var report = Report([Stack("dotnet", ".NET", "", "net10.0")]);
        var first = ProfileBuilder.Build(report, [], null, Now.AddDays(-1));

        var again = ProfileBuilder.Build(report, [], first, Now);

        Assert.Single(again.History);
    }

    // ------------------------------------------------------------------ Pelo protocolo, com descoberta real

    private sealed class Setup : IDisposable
    {
        public TempProject Project { get; } = TempProject.Next();
        public FakeClock Clock { get; } = new() { UtcNow = Now };
        public EngineSession Session { get; }
        public string ProjectId { get; }

        public Setup()
        {
            var engine = LocalEngine.Create(Project.DataDir, (_, _, _, _, _) => { }, rememberWorkspaces: false);
            ProjectId = engine.Workspace.Authorize(Project.Root).Project!.ProjectId;
            var hub = new IntegrationHub(new FileIntegrationStore(Project.DataDir), new FileSyncedDataStore(Project.DataDir), new MemoryVault(),
                new ConnectorRegistry([]), engine.Workspace, Clock, new MemoryAudit());
            var profiles = new TechnologyProfileService(engine.Configure, hub, engine.Workspace, new FileTechnologyProfileStore(Project.DataDir), Clock);
            Session = new EngineSession("test", profiles: profiles);
        }

        public JsonObject Request(string method, JsonObject parameters) =>
            Session.Handle(new JsonObject { ["v"] = 1, ["kind"] = "request", ["id"] = "t", ["method"] = method, ["params"] = parameters }.ToJsonString()).Response;

        public void Dispose() => Project.Dispose();
    }

    [Fact]
    public void Perfil_nasce_da_descoberta_fica_fora_do_projeto_e_acompanha_mudancas()
    {
        using var s = new Setup();
        var before = s.Project.Fingerprint();

        var empty = s.Request("engineering.profile", new JsonObject { ["projectId"] = s.ProjectId })["result"]!;
        var first = s.Request("engineering.analyze", new JsonObject { ["projectId"] = s.ProjectId })["result"]!["profile"]!;
        s.Project.Write("api/Api.csproj", "<Project Sdk=\"Microsoft.NET.Sdk.Web\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");
        s.Clock.UtcNow = Now.AddDays(1);
        var second = s.Request("engineering.analyze", new JsonObject { ["projectId"] = s.ProjectId })["result"]!["profile"]!;
        var stored = s.Request("engineering.profile", new JsonObject { ["projectId"] = s.ProjectId })["result"]!["profile"]!;

        Assert.Null(empty["profile"]);
        Assert.Contains(first["technologies"]!.AsArray(), t => t!["id"]!.GetValue<string>() == "nextjs");
        Assert.Contains(second["history"]!.AsArray(), c => c!["kind"]!.GetValue<string>() == "added" && c["subject"]!.GetValue<string>() == ".NET");
        Assert.Equal(second.ToJsonString(), stored.ToJsonString());
        Assert.True(File.Exists(Path.Combine(s.Project.DataDir, "profiles", s.ProjectId + ".json")));
        s.Project.Write("api/Api.csproj", "");
        File.Delete(Path.Combine(s.Project.Root, "api", "Api.csproj"));
        Directory.Delete(Path.Combine(s.Project.Root, "api"));
        Assert.Equal(before, s.Project.Fingerprint());

        var denied = s.Request("engineering.analyze", new JsonObject { ["projectId"] = "p-000000000000" });
        Assert.Equal("PROJECT_NOT_AUTHORIZED", denied["error"]!["code"]!.GetValue<string>());
    }

    [Fact]
    public void Resposta_do_perfil_confere_com_a_fixture_do_contrato()
    {
        using var s = new Setup();
        var result = s.Request("engineering.analyze", new JsonObject { ["projectId"] = s.ProjectId })["result"]!;

        var text = result.ToJsonString(new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        }).Replace(s.ProjectId, "p-000000000001").ReplaceLineEndings("\n");
        // Ambientes de IA dependem da máquina que roda o teste: a fixture guarda só o formato da lista.
        var node = System.Text.Json.Nodes.JsonNode.Parse(text)!;
        node["profile"]!["aiEnvironments"] = new JsonArray();
        text = node.ToJsonString(new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        }).ReplaceLineEndings("\n");

        var path = ContractFiles.Path_("fixtures", "engineering", "profile.json");
        if (Environment.GetEnvironmentVariable("MARILIZ_UPDATE_GOLDEN") == "1" || !File.Exists(path))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, text + "\n");
            return;
        }
        Assert.True(text == File.ReadAllText(path).ReplaceLineEndings("\n").TrimEnd(),
            "profile: a resposta mudou. Se foi deliberado, regenere com MARILIZ_UPDATE_GOLDEN=1 e ajuste o Zod da interface.");
    }
}
