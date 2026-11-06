using System.Diagnostics;
using System.Text;
using System.Text.Json.Nodes;
using Mariliz.Application.Tests.Support;

namespace Mariliz.Application.Tests;

/// <summary>
/// O motor .NET que o instalador leva, falando o protocolo como o host do Tauri fala, com o Python que o instalador leva:
/// a execução inteira (autorizar, iniciar, pausar, aprovar, gravar o registro, concluir) e o fim ordenado. É o mais perto do app
/// instalado que dá para chegar sem a janela. Só roda na CI do instalador, com <c>MARILIZ_PACKAGED_ENGINE</c> e
/// <c>MARILIZ_PACKAGED_RESOURCES</c> apontando para o instalador aberto por <c>scripts/smoke-installer.py</c>.
/// </summary>
public class PackagedEngineE2ETests
{
    private sealed class Engine : IDisposable
    {
        private readonly Process _process;
        private readonly List<JsonObject> _messages = [];
        private int _id;

        public Engine(string engine, string resources, string dataDir)
        {
            var python = OperatingSystem.IsWindows() ? Path.Combine(resources, "python", "python.exe") : Path.Combine(resources, "python", "bin", "python3");
            PythonPath = python;
            var psi = new ProcessStartInfo(engine)
            {
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8
            };
            // O que o host faz em engine.rs: aponta o motor para o interpretador e a inteligência empacotados.
            psi.Environment["MARILIZ_AI"] = Path.Combine(resources, "intelligence", "server.py");
            psi.Environment["MARILIZ_PYTHON"] = python;
            psi.Environment["MARILIZ_DATA_DIR"] = dataDir;
            _process = Process.Start(psi) ?? throw new InvalidOperationException("O motor não iniciou.");
            _process.ErrorDataReceived += (_, _) => { };
            _process.BeginErrorReadLine();
            new Thread(ReadLines) { IsBackground = true }.Start();
        }

        public string PythonPath { get; }
        public Process Process => _process;

        private void ReadLines()
        {
            while (_process.StandardOutput.ReadLine() is { } line)
            {
                try { if (System.Text.Json.Nodes.JsonNode.Parse(line) is JsonObject item) lock (_messages) _messages.Add(item); }
                catch (System.Text.Json.JsonException) { }
            }
        }

        public JsonObject Wait(Func<JsonObject, bool> predicate, int seconds = 90)
        {
            var deadline = DateTime.UtcNow.AddSeconds(seconds);
            while (DateTime.UtcNow < deadline)
            {
                lock (_messages)
                    if (_messages.FirstOrDefault(predicate) is { } match) return match;
                if (_process.HasExited) break;
                Thread.Sleep(50);
            }
            throw new TimeoutException("A mensagem esperada não chegou do motor instalado.");
        }

        public JsonObject Request(string method, JsonObject? parameters = null)
        {
            var id = "e" + Interlocked.Increment(ref _id);
            var line = new JsonObject { ["v"] = 1, ["kind"] = "request", ["id"] = id, ["method"] = method, ["params"] = parameters ?? new JsonObject() };
            _process.StandardInput.WriteLine(line.ToJsonString());
            _process.StandardInput.Flush();
            return Wait(m => m["kind"]?.GetValue<string>() == "response" && m["id"]?.GetValue<string>() == id);
        }

        public JsonObject Event(string type, string runId) =>
            Wait(m => m["kind"]?.GetValue<string>() == "event" && m["type"]?.GetValue<string>() == type && m["runId"]?.GetValue<string>() == runId);

        public void Dispose()
        {
            try { if (!_process.HasExited) _process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
            _process.Dispose();
        }
    }

    /// <summary>Processos que estão rodando o interpretador empacotado. Pelo caminho do executável, não pelo nome: só o do instalador conta.</summary>
    private static List<int> PythonProcesses(string pythonPath) =>
        Process.GetProcesses().Where(p =>
        {
            try { return string.Equals(p.MainModule?.FileName, pythonPath, StringComparison.OrdinalIgnoreCase); }
            catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException) { return false; }
        }).Select(p => p.Id).ToList();

    private static (string Engine, string Resources) Paths()
    {
        var engine = Environment.GetEnvironmentVariable(PackagedEngineFactAttribute.EngineVariable);
        var resources = Environment.GetEnvironmentVariable(PackagedFactAttribute.ResourcesVariable);
        if (string.IsNullOrWhiteSpace(engine) || string.IsNullOrWhiteSpace(resources))
            Assert.Fail($"{PackagedEngineFactAttribute.EngineVariable} e {PackagedFactAttribute.ResourcesVariable} são obrigatórias nesta execução.");
        Assert.True(File.Exists(engine), "O motor instalado não existe: " + engine);
        return (engine, resources);
    }

    private static async Task<bool> GoneAsync(string pythonPath, int seconds)
    {
        for (var i = 0; i < seconds * 10; i++)
        {
            if (PythonProcesses(pythonPath).Count == 0) return true;
            await Task.Delay(100);
        }
        return false;
    }

    private static JsonObject Handshake(Engine engine)
    {
        var hello = engine.Request("engine.hello", new JsonObject { ["protocol"] = new JsonArray(1), ["hostVersion"] = "e2e" });
        Assert.Equal(1, hello["result"]!["protocol"]!.GetValue<int>());
        return hello;
    }

    [PackagedEngineFact]
    public async Task O_motor_instalado_conduz_uma_execucao_inteira_e_encerra_sem_deixar_o_python()
    {
        var (enginePath, resources) = Paths();
        using var project = TempProject.Dotnet();
        project.Write("docs/cobranca.md", "# Cobrança\n\nO módulo de cobrança recorrente fatura assinaturas todos os meses.\n");
        using var engine = new Engine(enginePath, resources, project.DataDir);
        Handshake(engine);

        var projectId = engine.Request("workspace.authorize", new JsonObject { ["path"] = project.Root })["result"]!["projectId"]!.GetValue<string>();
        var health = engine.Request("runtime.health")["result"]!;
        Assert.Equal("ready", health["engine"]!.GetValue<string>());
        Assert.NotEqual("unavailable", health["aiRuntime"]!.GetValue<string>());

        var runId = engine.Request("workflow.start", new JsonObject
        {
            ["projectId"] = projectId,
            ["input"] = new JsonObject { ["goal"] = "avaliar o módulo de cobrança recorrente", ["impact"] = true, ["requiresApproval"] = true }
        })["result"]!["runId"]!.GetValue<string>();

        var approval = engine.Event("approval.required", runId)["data"]!;
        var action = approval["actions"]!.AsArray().Single()!;
        Assert.Equal("file.write", action["kind"]!.GetValue<string>());
        // Antes da aprovação, o Python instalado já está rodando o grafo, e o projeto está intacto.
        Assert.NotEmpty(PythonProcesses(engine.PythonPath));
        Assert.False(Directory.Exists(Path.Combine(project.Root, ".mariliz", "decisions")));

        engine.Request("workflow.resume", new JsonObject
        {
            ["projectId"] = projectId,
            ["runId"] = runId,
            ["approvalId"] = approval["approvalId"]!.GetValue<string>(),
            ["actionIds"] = new JsonArray(action["id"]!.GetValue<string>())
        });
        engine.Event("workflow.completed", runId);

        var snapshot = engine.Request("run.snapshot", new JsonObject { ["runId"] = runId })["result"]!;
        Assert.Equal("completed_with_limitations", snapshot["state"]!.GetValue<string>());
        Assert.Contains("docs/cobranca.md", snapshot["result"]!["evidence"]!.ToJsonString());
        var record = Directory.GetFiles(Path.Combine(project.Root, ".mariliz", "decisions"), "*-impacto.md").Single();
        Assert.Contains("# Decisão de arquitetura", File.ReadAllText(record));

        // Fim ordenado, como o host o pede: o motor sai e leva o Python junto.
        engine.Process.StandardInput.WriteLine("{\"v\":1,\"kind\":\"request\",\"id\":\"e-shutdown\",\"method\":\"engine.shutdown\"}");
        engine.Process.StandardInput.Flush();
        Assert.True(engine.Process.WaitForExit(20_000), "O motor não encerrou depois de engine.shutdown.");
        Assert.True(await GoneAsync(engine.PythonPath, 15), "O Python do instalador continuou vivo depois do motor.");
    }

    [PackagedEngineFact]
    public async Task Motor_instalado_finalizado_a_forca_nao_deixa_o_python_orfao()
    {
        var (enginePath, resources) = Paths();
        using var project = TempProject.Dotnet();
        project.Write("docs/cobranca.md", "# Cobrança\n\nO módulo de cobrança recorrente fatura assinaturas todos os meses.\n");
        using var engine = new Engine(enginePath, resources, project.DataDir);
        Handshake(engine);
        var projectId = engine.Request("workspace.authorize", new JsonObject { ["path"] = project.Root })["result"]!["projectId"]!.GetValue<string>();
        var runId = engine.Request("workflow.start", new JsonObject
        {
            ["projectId"] = projectId,
            ["input"] = new JsonObject { ["goal"] = "avaliar o módulo de cobrança recorrente", ["impact"] = true, ["requiresApproval"] = true }
        })["result"]!["runId"]!.GetValue<string>();
        engine.Event("approval.required", runId);
        Assert.NotEmpty(PythonProcesses(engine.PythonPath));

        // O host espera o prazo do desligamento e mata o motor. Só o motor: o Python não recebe sinal nenhum.
        engine.Process.Kill(entireProcessTree: false);
        // Com prazo: o WaitForExit sem argumento espera o fim dos canais de saída, que um Python órfão manteria abertos para sempre.
        Assert.True(engine.Process.WaitForExit(10_000), "O motor não terminou depois de finalizado.");

        Assert.True(await GoneAsync(engine.PythonPath, 20), "O Python do instalador ficou órfão depois que o motor foi finalizado.");
    }
}
