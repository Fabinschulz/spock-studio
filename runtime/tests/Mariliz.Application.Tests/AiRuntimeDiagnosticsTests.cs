using System.Diagnostics;
using Mariliz.Application.Tests.Support;
using Mariliz.Infrastructure;

namespace Mariliz.Application.Tests;

// Validação real A1: a falha do Python não aparecia em lugar nenhum, porque o motor lia o stderr dele e jogava fora.
public class AiRuntimeDiagnosticsTests
{
    // Escreve no stderr antes de responder /health: uma linha curta, uma longa e mais linhas do que o teto.
    private const string NoisyServer = """
        import sys
        from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

        port = int(sys.argv[sys.argv.index("--port") + 1])
        print("[ai] falha em teste: ValueError", file=sys.stderr, flush=True)
        print("x" * 2000, file=sys.stderr, flush=True)
        for n in range(1000):
            print(f"linha {n}", file=sys.stderr, flush=True)
        print("[ai] fim", file=sys.stderr, flush=True)

        class Handler(BaseHTTPRequestHandler):
            def do_GET(self):
                self.send_response(200)
                self.send_header("Content-Length", "2")
                self.end_headers()
                self.wfile.write(b"ok")

            def log_message(self, *args):
                pass

        ThreadingHTTPServer(("127.0.0.1", port), Handler).serve_forever()
        """;

    [Fact]
    public async Task O_stderr_do_python_vai_para_o_diagnostico_com_prefixo_e_teto()
    {
        using var project = new TempProject();
        var script = Path.Combine(project.DataDir, "server.py");
        File.WriteAllText(script, NoisyServer);
        var python = PythonRuntimeLayout.Candidates(AppContext.BaseDirectory, null, null, Environment.GetEnvironmentVariable("PATH")).First();
        var log = new StringWriter();
        var runtime = new LoopbackAiRuntime(scriptPath: script, pythonPath: python, diagnostics: log);
        try
        {
            await runtime.EnsureReadyAsync(CancellationToken.None);

            string[] lines = [];
            var watch = Stopwatch.StartNew();
            while (watch.Elapsed < TimeSpan.FromSeconds(10))
            {
                lock (log) lines = log.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
                if (lines.Length >= LoopbackAiRuntime.MaxDiagnosticLines) break;
                await Task.Delay(50);
            }

            Assert.Equal("[ia] [ai] falha em teste: ValueError", lines[0]);
            Assert.Equal(LoopbackAiRuntime.DiagnosticPrefix.Length + LoopbackAiRuntime.MaxDiagnosticLength + 1, lines[1].Length);
            Assert.All(lines, line => Assert.StartsWith(LoopbackAiRuntime.DiagnosticPrefix, line));
            await Task.Delay(300);
            lock (log) lines = log.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
            Assert.Equal(LoopbackAiRuntime.MaxDiagnosticLines, lines.Length);
            Assert.DoesNotContain("[ia] [ai] fim", lines);
        }
        finally
        {
            await runtime.ShutdownAsync();
        }
    }
}
