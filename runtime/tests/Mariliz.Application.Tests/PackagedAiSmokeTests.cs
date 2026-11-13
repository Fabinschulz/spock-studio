using System.Text.Json.Nodes;
using Mariliz.Application.Ports;
using Mariliz.Application.Tests.Support;
using Mariliz.Application.Workflows;
using Mariliz.Infrastructure;

namespace Mariliz.Application.Tests;

// Só roda no CI do instalador, com MARILIZ_PACKAGED_RESOURCES apontando para desktop/host/src-tauri/resources depois de build-runtime.ps1 -FetchPythonEmbed (Windows) ou de scripts/package-intelligence.py bundle --python <triple> (macOS e Linux). Com MARILIZ_REQUIRE_PACKAGED=1 a falta do pacote é falha.
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
