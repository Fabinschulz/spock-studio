using System.Text.Json.Nodes;
using Mariliz.Application.Ports;
using Mariliz.Application.Tests.Support;
using Mariliz.Application.Workflows;
using Mariliz.Infrastructure;

namespace Mariliz.Application.Tests;

// Só roda no CI do instalador, com MARILIZ_PACKAGED_RESOURCES apontando para desktop/host/src-tauri/resources depois de build-runtime.ps1 -FetchPython (Windows) ou de scripts/package-intelligence.py bundle --python <triple> (macOS e Linux). Com MARILIZ_REQUIRE_PACKAGED=1 a falta do pacote é falha.
public class PackagedAiSmokeTests
{
    [PackagedFact]
    public async Task Pacote_embarcado_sobe_o_runtime_de_ia()
    {
        var root = Environment.GetEnvironmentVariable(PackagedFactAttribute.ResourcesVariable);
        if (string.IsNullOrWhiteSpace(root))
            Assert.Fail($"{PackagedFactAttribute.ResourcesVariable} não está definida, mas o pacote é obrigatório nesta execução.");

        if (!Path.IsPathRooted(root))
        {
            var workspace = Environment.GetEnvironmentVariable("GITHUB_WORKSPACE");
            if (string.IsNullOrWhiteSpace(workspace))
            {
                workspace = Path.GetFullPath(
                    Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
            }
            root = Path.GetFullPath(Path.Combine(workspace, root));
        }

        var script = Path.Combine(root, "intelligence", "server.py");
        var python = OperatingSystem.IsWindows() ? Path.Combine(root, "python", "python.exe") : Path.Combine(root, "python", "bin", "python3");
        Assert.True(File.Exists(script), "server.py embarcado ausente.");
        Assert.True(File.Exists(python), "Interpretador embarcado ausente.");
        Assert.Empty(Directory.GetFiles(Path.Combine(root, "intelligence", "packages"), "*.py", SearchOption.AllDirectories));
        var pythonLicense = OperatingSystem.IsWindows() ? Path.Combine(root, "python", "LICENSE.txt") : Path.Combine(root, "python", "lib", "python3.14", "LICENSE.txt");
        Assert.True(File.Exists(pythonLicense), "Licença do CPython ausente do pacote.");
        // D-07: os avisos de terceiros vão no instalador e precisam estar completos.
        var notices = Path.Combine(root, "notices");
        Assert.True(File.Exists(Path.Combine(notices, "THIRD_PARTY_NOTICES.txt")), "Avisos de terceiros ausentes do pacote.");
        var manifest = JsonNode.Parse(File.ReadAllText(Path.Combine(notices, "notices-manifest.json")))!;
        Assert.True(manifest["complete"]!.GetValue<bool>(), "O manifesto dos avisos registra lacunas.");
        Assert.True(manifest["components"]!.GetValue<int>() > 0, "O manifesto dos avisos não lista componentes.");
        if (OperatingSystem.IsWindows())
        {
            // O PBS para Windows não traz no LICENSE.txt os textos de OpenSSL, SQLite, libffi e afins; eles vão à parte.
            foreach (var notice in new[] { "README.md", "LICENSE.openssl-3.txt", "LICENSE.sqlite.txt", "LICENSE.libffi.txt", "LICENSE.zlib-ng.txt", "LICENSE.zstd.txt" })
                Assert.True(File.Exists(Path.Combine(root, "python", "licenses", "python-build-standalone", notice)), $"{notice} do python-build-standalone ausente do pacote.");
            Assert.True(File.Exists(Path.Combine(root, "python", "python314._pth")), "python314._pth ausente: o interpretador leria PYTHONPATH e o registro.");
        }
        if (OperatingSystem.IsLinux())
        {
            // O Berkeley DB (Sleepycat) só existe no módulo _dbm, que a poda tira; se ele voltar, a licença e as fontes têm de ir junto.
            Assert.Empty(Directory.GetFiles(Path.Combine(root, "python", "lib", "python3.14", "lib-dynload"), "_dbm*"));
            // As bibliotecas nativas que o executável do PBS leva dentro (OpenSSL, SQLite, ncurses, zstd...).
            foreach (var notice in new[] { "README.md", "LICENSE.openssl-3.txt", "LICENSE.ncurses.txt", "LICENSE.zstd.txt" })
                Assert.True(File.Exists(Path.Combine(root, "python", "licenses", "python-build-standalone", notice)), $"{notice} do python-build-standalone ausente do pacote.");
        }

        var runtime = new LoopbackAiRuntime(scriptPath: script, pythonPath: python);
        try
        {
            await runtime.EnsureReadyAsync(CancellationToken.None);
            Assert.Equal("ready", runtime.Probe());

            // O grafo em LangGraph carrega do bytecode embarcado e conclui sem modelo.
            var events = new List<JsonObject>();
            var ack = await runtime.StartAsync(new JsonObject
            {
                ["schema"] = "ai.workflow.start.v2",
                ["workflowId"] = "engineering.request",
                ["workflowVersion"] = "1",
                ["runId"] = "run-0123456789ab",
                ["executionId"] = "ex-0123456789ab",
                ["projectId"] = "p-0123456789ab",
                ["workspaceId"] = null,
                ["input"] = new JsonObject { ["goal"] = "descrever", ["impact"] = true },
                ["context"] = new JsonObject { ["available"] = true, ["stacks"] = new JsonArray("dotnet") },
                ["limits"] = new JsonObject { ["maxIterations"] = 8, ["maxMilliseconds"] = 10_000, ["maxTokens"] = 0, ["maxCostUsd"] = 0 },
                ["policy"] = new JsonObject { ["allowedActions"] = new JsonArray(), ["modelAllowed"] = false }
            }, events.Add, CancellationToken.None);
            Assert.Equal(AckStatus.Completed, ack.Status);
            Assert.Equal(WorkflowService.WorkflowId, ack.WorkflowId);
            Assert.Contains(events, e => e["type"]?.GetValue<string>() == "agent.completed");
        }
        finally
        {
            await runtime.ShutdownAsync();
        }
    }
}
