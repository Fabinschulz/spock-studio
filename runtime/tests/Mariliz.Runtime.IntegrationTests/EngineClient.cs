using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Schema;

namespace Mariliz.Runtime.IntegrationTests;

// Cliente NDJSON mínimo do motor real: sobe `dotnet Mariliz.Runtime.dll` com uma pasta de dados própria, correlaciona resposta e
// pedido por id e nunca espera sem prazo. Quem falha (ou estoura o prazo) mata a árvore de processos em DisposeAsync, para não
// deixar motor órfão numa máquina compartilhada.
internal sealed class EngineClient : IAsyncDisposable
{
    public static readonly TimeSpan Step = TimeSpan.FromSeconds(30);

    private readonly Process _process;
    private readonly object _gate = new();
    private readonly List<string> _stdout = [];
    private readonly List<string> _stderr = [];
    private readonly Dictionary<string, TaskCompletionSource<JsonObject>> _pending = [];
    private readonly Task _stdoutPump;
    private readonly Task _stderrPump;
    private int _next;

    private EngineClient(Process process)
    {
        _process = process;
        _stdoutPump = Task.Run(PumpStdoutAsync);
        _stderrPump = Task.Run(PumpStderrAsync);
    }

    public int ProcessId => _process.Id;

    public static EngineClient Start(string dataDir)
    {
        // O motor chega à saída dos testes pela ProjectReference; é o mesmo build que está sendo testado.
        var dll = Path.Combine(AppContext.BaseDirectory, "Mariliz.Runtime.dll");
        var psi = new ProcessStartInfo("dotnet")
        {
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
            StandardInputEncoding = new UTF8Encoding(false), StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8,
            UseShellExecute = false,
            // O teste nunca lê nem grava os dados de verdade da pessoa; MARILIZ_PYTHON segue herdado do ambiente.
            Environment = { ["MARILIZ_DATA_DIR"] = dataDir },
            WorkingDirectory = Path.GetTempPath()
        };
        psi.ArgumentList.Add(dll);
        return new EngineClient(Process.Start(psi)!);
    }

    public IReadOnlyList<string> StdoutLines
    {
        get { lock (_gate) return [.. _stdout]; }
    }

    public string Stderr
    {
        get { lock (_gate) return string.Join("\n", _stderr); }
    }

    // Tudo o que o motor publicou fora de uma resposta (progresso de execução, por exemplo).
    public IReadOnlyList<JsonObject> Events => [.. StdoutLines.Select(Parse).OfType<JsonObject>().Where(message => message["kind"]?.GetValue<string>() == "event")];

    public async Task<JsonObject> CallAsync(string method, JsonObject? parameters = null, TimeSpan? timeout = null)
    {
        var id = $"t{Interlocked.Increment(ref _next)}";
        var completion = new TaskCompletionSource<JsonObject>(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_gate) _pending[id] = completion;
        var request = new JsonObject { ["v"] = 1, ["kind"] = "request", ["id"] = id, ["method"] = method };
        if (parameters is not null) request["params"] = parameters;
        var limit = timeout ?? Step;
        try
        {
            await _process.StandardInput.WriteLineAsync(request.ToJsonString().AsMemory()).WaitAsync(limit);
            await _process.StandardInput.FlushAsync().WaitAsync(limit);
            return await completion.Task.WaitAsync(limit);
        }
        catch (TimeoutException)
        {
            throw new TimeoutException($"O motor não respondeu a {method} em {limit.TotalSeconds:0} s.{Environment.NewLine}stderr: {Stderr}{Environment.NewLine}stdout: {string.Join(" | ", StdoutLines.TakeLast(5))}");
        }
    }

    // O resultado de uma chamada que precisa dar certo; o erro entra na mensagem da falha.
    public async Task<JsonObject> ResultAsync(string method, JsonObject? parameters = null, TimeSpan? timeout = null)
    {
        var response = await CallAsync(method, parameters, timeout);
        return response["result"] as JsonObject ?? throw new InvalidOperationException($"{method} falhou: {response.ToJsonString()}");
    }

    public async Task<string> ErrorCodeAsync(string method, JsonObject? parameters = null)
    {
        var response = await CallAsync(method, parameters);
        return response["error"]?["code"]?.GetValue<string>() ?? throw new InvalidOperationException($"{method} devia falhar, mas respondeu: {response.ToJsonString()}");
    }

    public Task<JsonObject> HelloAsync() => ResultAsync("engine.hello", new JsonObject { ["protocol"] = new JsonArray(1), ["hostVersion"] = "teste-processo" });

    public async Task<string> AuthorizeAsync(string path) =>
        (await ResultAsync("workspace.authorize", new JsonObject { ["path"] = path }))["projectId"]!.GetValue<string>();

    // O fim limpo: engine.shutdown, saída com código 0 e nada no stdout que não seja uma mensagem do protocolo.
    public async Task ShutdownAsync()
    {
        await ResultAsync("engine.shutdown");
        using var limit = new CancellationTokenSource(Step);
        try
        {
            await _process.WaitForExitAsync(limit.Token);
            await Task.WhenAll(_stdoutPump, _stderrPump).WaitAsync(Step);
        }
        catch (Exception ex) when (ex is OperationCanceledException or TimeoutException)
        {
            throw new TimeoutException($"O motor não encerrou em {Step.TotalSeconds:0} s depois do engine.shutdown.{Environment.NewLine}stderr: {Stderr}");
        }
        Assert.Equal(0, _process.ExitCode);
        AssertStdoutIsProtocolOnly();
    }

    public void AssertStdoutIsProtocolOnly()
    {
        // Registro próprio: o global não aceita o mesmo $id duas vezes, e o EngineProcessTests já carrega este esquema nele.
        var schema = JsonSchema.FromText(File.ReadAllText(Path.Combine(RepositoryRoot(), "contracts", "schemas", "protocol", "envelope.v1.schema.json")),
            new BuildOptions { SchemaRegistry = new SchemaRegistry() });
        foreach (var line in StdoutLines)
        {
            var message = Parse(line);
            Assert.True(message is not null, $"Linha no stdout que não é JSON: {line}");
            Assert.True(schema.Evaluate(JsonSerializer.SerializeToElement(message)).IsValid, $"Linha fora do envelope do protocolo: {line}");
        }
    }

    public bool HasExited => _process.HasExited;

    // O motor escreve o aviso no stderr antes de abrir o protocolo, mas a leitura do pipe é assíncrona: espera aparecer, sem passar do prazo.
    public async Task WaitForStderrAsync(string text)
    {
        var deadline = Stopwatch.StartNew();
        while (!Stderr.Contains(text, StringComparison.Ordinal))
        {
            if (deadline.Elapsed > Step) throw new TimeoutException($"O stderr do motor não trouxe \"{text}\" em {Step.TotalSeconds:0} s.{Environment.NewLine}stderr: {Stderr}");
            await Task.Delay(50);
        }
    }

    // Filhos vivos do motor. No Windows um filho órfão continua apontando para o pai que morreu, então a consulta vale também depois da saída.
    public static IReadOnlyList<int> ChildProcessIds(int parentId)
    {
        var psi = OperatingSystem.IsWindows()
            ? new ProcessStartInfo("powershell", $"-NoProfile -NonInteractive -Command \"(Get-CimInstance Win32_Process -Filter 'ParentProcessId={parentId}' | ForEach-Object {{ $_.ProcessId }}) -join ','\"")
            : new ProcessStartInfo("pgrep", $"-P {parentId}");
        psi.RedirectStandardOutput = true;
        psi.UseShellExecute = false;
        using var query = Process.Start(psi)!;
        var output = query.StandardOutput.ReadToEnd();
        if (!query.WaitForExit(30_000)) throw new TimeoutException("A consulta dos processos filhos não terminou em 30 s.");
        return [.. output.Split([',', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries).Select(text => int.Parse(text.Trim()))];
    }

    public static bool IsAlive(int processId)
    {
        try
        {
            using var other = Process.GetProcessById(processId);
            return !other.HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (!_process.HasExited) _process.Kill(entireProcessTree: true);
            await _process.WaitForExitAsync().WaitAsync(Step);
        }
        catch (Exception ex) when (ex is InvalidOperationException or TimeoutException or System.ComponentModel.Win32Exception)
        {
            // Já tinha saído, ou o sistema não deixou matar: não há mais o que o teste possa fazer por ele.
        }
        _process.Dispose();
    }

    public static string RepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "contracts", "schemas", "protocol", "envelope.v1.schema.json")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Raiz do repositório não encontrada.");
    }

    private static JsonObject? Parse(string line)
    {
        try { return JsonNode.Parse(line) as JsonObject; }
        catch (JsonException) { return null; }
    }

    private async Task PumpStdoutAsync()
    {
        while (await _process.StandardOutput.ReadLineAsync() is { } line)
        {
            lock (_gate) _stdout.Add(line);
            if (Parse(line) is not { } message || message["kind"]?.GetValue<string>() != "response" || message["id"]?.GetValue<string>() is not { } id) continue;
            TaskCompletionSource<JsonObject>? waiting;
            lock (_gate) _pending.Remove(id, out waiting);
            waiting?.TrySetResult(message);
        }
        List<TaskCompletionSource<JsonObject>> orphans;
        lock (_gate)
        {
            orphans = [.. _pending.Values];
            _pending.Clear();
        }
        foreach (var orphan in orphans) orphan.TrySetException(new InvalidOperationException($"O motor encerrou antes de responder.{Environment.NewLine}stderr: {Stderr}"));
    }

    private async Task PumpStderrAsync()
    {
        while (await _process.StandardError.ReadLineAsync() is { } line)
        {
            lock (_gate) _stderr.Add(line);
        }
    }
}
