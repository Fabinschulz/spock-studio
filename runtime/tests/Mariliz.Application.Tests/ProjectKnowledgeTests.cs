using System.Text.Json.Nodes;
using Mariliz.Application.Engineering;
using Mariliz.Application.Ports;
using Mariliz.Application.Protocol;
using Mariliz.Application.Tests.Support;
using Mariliz.Core.Text;
using Mariliz.Infrastructure;

namespace Mariliz.Application.Tests;

// O runtime de IA é um dublê que diz se está ligado e devolve o manifesto do índice.
public class ProjectKnowledgeTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 11, 12, 0, 0, TimeSpan.Zero);
    private const string Secret = "AKIAABCDEFGHIJKLMNOP";

    private sealed class FakeRuntime(string probe, IReadOnlyDictionary<string, string>? manifest = null) : IAiRuntime
    {
        public int ManifestCalls { get; private set; }
        public string Probe() => probe;
        public Task EnsureReadyAsync(CancellationToken ct) => throw new InvalidOperationException("A visão não pode subir o runtime.");
        public Task<IReadOnlyDictionary<string, string>> IndexedHashesAsync(string projectId, string indexRoot, CancellationToken ct)
        {
            ManifestCalls++;
            return Task.FromResult(manifest ?? new Dictionary<string, string>());
        }
        public Task<WorkflowAck> StartAsync(JsonObject request, Action<JsonObject> onEvent, CancellationToken ct) => throw new NotSupportedException();
        public Task<WorkflowAck> ResumeAsync(JsonObject request, Action<JsonObject> onEvent, CancellationToken ct) => throw new NotSupportedException();
        public Task CancelAsync(string runId, string? projectId = null, string? executionRoot = null) => Task.CompletedTask;
        public Task ShutdownAsync() => Task.CompletedTask;
    }

    private sealed class Setup : IDisposable
    {
        public TempProject Project { get; } = new();
        public string ProjectId { get; }
        public EngineSession Session { get; }

        public Setup(FakeRuntime runtime)
        {
            Project.Write("README.md", "Guia do projeto, longo o bastante para entrar no índice.");
            Project.Write("src/App.cs", "public class App { public static void Main() { } }");
            Project.Write("src/Config.cs", $"class Config {{ const string Key = \"{Secret}\"; }}");
            Project.Write("docs/rascunho/ideia.md", "Rascunho longo o bastante para entrar no índice.");
            Project.Write("app.log", "Linha de log longa o bastante para entrar no índice.");
            Project.Write(".gitignore", "*.log\n");
            Project.Write(".env", "TOKEN=abc");
            Project.Write("logo.png", "binário");
            Project.Write("node_modules/pkg/index.js", "module.exports = 1;");
            Project.Write("vazio.md", "   ");
            Project.Write("mariliz.yaml", "schema: 1\nknowledge:\n  exclude:\n    - docs/rascunho/\n");

            var engine = LocalEngine.Create(Project.DataDir, (_, _, _, _, _) => { }, rememberWorkspaces: false);
            ProjectId = engine.Workspace.Authorize(Project.Root).Project!.ProjectId;
            var knowledge = new ProjectKnowledgeService(engine.Workspace, engine.Files, runtime, Project.DataDir, new FakeClock { UtcNow = Now });
            var profiles = new TechnologyProfileService(engine.Configure, null!, engine.Workspace, null!, new FakeClock());
            Session = new EngineSession("test", profiles: profiles, knowledge: knowledge);
        }

        public JsonNode Inspect()
        {
            var response = Session.Handle(new JsonObject
            {
                ["v"] = 1, ["kind"] = "request", ["id"] = "t", ["method"] = "engineering.knowledge",
                ["params"] = new JsonObject { ["projectId"] = ProjectId }
            }.ToJsonString()).Response;
            return response["result"] ?? throw new InvalidOperationException(response.ToJsonString());
        }

        public void Dispose() => Project.Dispose();
    }

    private static string Hash(string text) => Hashing.Sha256(text);

    [Fact]
    public void Mostra_o_que_entra_o_que_fica_de_fora_com_o_motivo_e_o_estado_de_cada_arquivo_no_indice()
    {
        var manifest = new Dictionary<string, string>
        {
            ["README.md"] = Hash("Guia do projeto, longo o bastante para entrar no índice."),
            ["src/App.cs"] = "hash-antigo",
            ["docs/apagado.md"] = "qualquer"
        };
        var runtime = new FakeRuntime("ready", manifest);
        using var s = new Setup(runtime);

        var knowledge = s.Inspect();

        Assert.Equal("read", knowledge["indexState"]!.GetValue<string>());
        var files = knowledge["files"]!.AsArray().ToDictionary(f => f!["path"]!.GetValue<string>(), f => f!["state"]!.GetValue<string>());
        Assert.Equal("indexed", files["README.md"]);
        Assert.Equal("outdated", files["src/App.cs"]);
        Assert.Equal("pending", files["mariliz.yaml"]);
        Assert.Equal("README.md", knowledge["files"]![0]!["path"]!.GetValue<string>());
        Assert.Equal((1, 1, 1), (knowledge["indexed"]!.GetValue<int>(), knowledge["outdated"]!.GetValue<int>(), knowledge["orphaned"]!.GetValue<int>()));
        Assert.Equal(["docs/rascunho/"], knowledge["exclude"]!.AsArray().Select(x => x!.GetValue<string>()));

        var excluded = knowledge["excluded"]!.AsArray().ToDictionary(e => e!["reason"]!.GetValue<string>(), e => e!["examples"]!.AsArray().Select(x => x!.GetValue<string>()).ToList());
        Assert.Equal(["src/Config.cs"], excluded["secret"]);
        Assert.Equal(["docs/rascunho/ideia.md"], excluded["excluded"]);
        Assert.Equal(["app.log"], excluded["gitignored"]);
        Assert.Equal(["vazio.md"], excluded["empty"]);
        Assert.Contains(".env", excluded["blocked"]);
        Assert.Contains("logo.png", excluded["blocked"]);
        // Pastas de dependência e build nem são percorridas pela leitura do motor: não aparecem nem como excluídas.
        Assert.DoesNotContain("node_modules", knowledge.ToJsonString());
        Assert.Equal("secret", knowledge["excluded"]![0]!["reason"]!.GetValue<string>());

        var json = knowledge.ToJsonString();
        Assert.DoesNotContain(Secret, json);
        Assert.DoesNotContain("public static void Main", json);
        Assert.Equal(1, runtime.ManifestCalls);
    }

    [Theory]
    [InlineData("stopped", "runtimeStopped")]
    [InlineData("unavailable", "runtimeUnavailable")]
    public void Com_o_runtime_desligado_nao_sobe_o_processo_e_diz_que_o_indice_nao_foi_lido(string probe, string state)
    {
        var runtime = new FakeRuntime(probe);
        using var s = new Setup(runtime);

        var knowledge = s.Inspect();

        Assert.Equal(state, knowledge["indexState"]!.GetValue<string>());
        Assert.All(knowledge["files"]!.AsArray(), f => Assert.Equal("unknown", f!["state"]!.GetValue<string>()));
        Assert.Equal(0, runtime.ManifestCalls);
        Assert.Equal(3, knowledge["allowed"]!.GetValue<int>());
    }

    [Fact]
    public void Resposta_do_conhecimento_confere_com_a_fixture_do_contrato()
    {
        var runtime = new FakeRuntime("ready", new Dictionary<string, string>
        {
            ["README.md"] = Hash("Guia do projeto, longo o bastante para entrar no índice."),
            ["src/App.cs"] = "hash-antigo"
        });
        using var s = new Setup(runtime);

        var text = s.Inspect().ToJsonString(new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        }).Replace(s.ProjectId, "p-000000000001").ReplaceLineEndings("\n");

        var path = ContractFiles.Path_("fixtures", "engineering", "knowledge.json");
        if (Environment.GetEnvironmentVariable("MARILIZ_UPDATE_GOLDEN") == "1" || !File.Exists(path))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, text + "\n");
            return;
        }
        Assert.True(text == File.ReadAllText(path).ReplaceLineEndings("\n").TrimEnd(),
            "knowledge: a resposta mudou. Se foi deliberado, regenere com MARILIZ_UPDATE_GOLDEN=1 e ajuste o Zod da interface.");
    }
}
