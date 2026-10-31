using Mariliz.Infrastructure;

namespace Mariliz.Application.Tests;

/// <summary>
/// Só roda no CI do instalador Windows, com <c>MARILIZ_PACKAGED_RESOURCES</c> apontando para
/// <c>desktop/host/src-tauri/resources</c> depois de <c>build-runtime.ps1 -FetchPythonEmbed</c>.
/// </summary>
public class PackagedAiSmokeTests
{
    [Fact]
    public async Task Pacote_embarcado_sobe_o_runtime_de_ia()
    {
        var root = Environment.GetEnvironmentVariable("MARILIZ_PACKAGED_RESOURCES");
        if (string.IsNullOrWhiteSpace(root))
            return;

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
        var python = Path.Combine(root, "python", "python.exe");
        Assert.True(File.Exists(script), "server.py embarcado ausente.");
        Assert.True(File.Exists(python), "python.exe embarcado ausente.");

        var runtime = new LoopbackAiRuntime(scriptPath: script, pythonPath: python);
        try
        {
            await runtime.EnsureReadyAsync(CancellationToken.None);
            Assert.Equal("ready", runtime.Probe());
        }
        finally
        {
            await runtime.ShutdownAsync();
        }
    }
}
