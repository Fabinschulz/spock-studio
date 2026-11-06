using System.Diagnostics;
using Mariliz.Application.Tests.Support;
using Mariliz.Infrastructure;

namespace Mariliz.Application.Tests;

/// <summary>Um Python vivo mas travado não pode segurar quem pergunta pela saúde: o cancelamento volta logo e a próxima execução troca o processo.</summary>
public class AiRuntimeHealthTests
{
    // Servidor de mentira: responde /health até o arquivo-sinal existir; a partir daí cada processo trava para sempre.
    private const string HangingServer = """
        import sys, os, time
        from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

        flag = sys.argv[sys.argv.index("--flag") + 1] if "--flag" in sys.argv else os.environ["HUNG_FLAG"]
        port = int(sys.argv[sys.argv.index("--port") + 1])
        state = {"hung": False}

        class Handler(BaseHTTPRequestHandler):
            def do_GET(self):
                if os.path.exists(flag):
                    state["hung"] = True
                if state["hung"]:
                    time.sleep(600)
                self.send_response(200)
                self.send_header("Content-Length", "2")
                self.end_headers()
                self.wfile.write(b"ok")

            def do_POST(self):
                self.send_response(404)
                self.send_header("Content-Length", "0")
                self.end_headers()

            def log_message(self, *args):
                pass

        ThreadingHTTPServer(("127.0.0.1", port), Handler).serve_forever()
        """;

    [Fact]
    public async Task Python_travado_nao_segura_o_cancelamento_e_a_proxima_execucao_troca_o_processo()
    {
        using var project = new TempProject();
        var flag = Path.Combine(project.DataDir, "hang.flag");
        var script = Path.Combine(project.DataDir, "server.py");
        File.WriteAllText(script, HangingServer);
        var python = PythonRuntimeLayout.Candidates(AppContext.BaseDirectory, null, null, Environment.GetEnvironmentVariable("PATH")).First();
        Environment.SetEnvironmentVariable("HUNG_FLAG", flag);
        var runtime = new LoopbackAiRuntime(scriptPath: script, pythonPath: python);
        try
        {
            await runtime.EnsureReadyAsync(CancellationToken.None);
            var first = runtime.ProcessId;
            Assert.NotNull(first);

            File.WriteAllText(flag, "");
            var watch = Stopwatch.StartNew();
            await runtime.CancelAsync("run-aaaaaaaaaaaa");
            watch.Stop();
            Assert.True(watch.Elapsed < TimeSpan.FromSeconds(12), $"o cancelamento esperou {watch.Elapsed.TotalSeconds:F1}s por um processo travado");

            File.Delete(flag);
            await runtime.EnsureReadyAsync(CancellationToken.None);
            Assert.NotNull(runtime.ProcessId);
            Assert.NotEqual(first, runtime.ProcessId);
        }
        finally
        {
            Environment.SetEnvironmentVariable("HUNG_FLAG", null);
            await runtime.ShutdownAsync();
        }
    }
}
