using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Mariliz.Runtime.IntegrationTests;

// Cliente MCP mínimo do processo real (`dotnet Mariliz.Runtime.dll mcp ...`), como uma IDE: JSON-RPC uma mensagem por linha no stdin e no
// stdout, pasta de dados própria e nunca uma espera sem prazo. Quem falha mata a árvore de processos em DisposeAsync.
internal sealed class McpClient : IAsyncDisposable
{
    public static readonly TimeSpan Step = TimeSpan.FromSeconds(60);

    private readonly Process _process;
    private readonly object _gate = new();
    private readonly List<string> _stdout = [];
    private readonly List<string> _stderr = [];
    private readonly Dictionary<int, TaskCompletionSource<JsonObject>> _pending = [];
    private readonly Task _stdoutPump;
    private readonly Task _stderrPump;
    private int _next;

    private McpClient(Process process)
    {
        _process = process;
        _stdoutPump = Task.Run(PumpStdoutAsync);
        _stderrPump = Task.Run(PumpStderrAsync);
    }

    public string Stderr
    {
        get { lock (_gate) return string.Join("\n", _stderr); }
    }

    public IReadOnlyList<string> StdoutLines
    {
        get { lock (_gate) return [.. _stdout]; }
    }

    public static McpClient Start(string dataDir, params string[] arguments)
    {
        var dll = Path.Combine(AppContext.BaseDirectory, "Mariliz.Runtime.dll");
        var psi = new ProcessStartInfo("dotnet")
        {
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
            StandardInputEncoding = new UTF8Encoding(false), StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8,
            UseShellExecute = false,
            // O teste nunca lê nem grava os dados de verdade da pessoa.
            Environment = { ["MARILIZ_DATA_DIR"] = dataDir },
            WorkingDirectory = Path.GetTempPath()
        };
        psi.ArgumentList.Add(dll);
        psi.ArgumentList.Add("mcp");
        foreach (var argument in arguments) psi.ArgumentList.Add(argument);
        return new McpClient(Process.Start(psi)!);
    }

    public async Task<JsonObject> CallAsync(string method, JsonObject? parameters = null)
    {
        var id = Interlocked.Increment(ref _next);
        var completion = new TaskCompletionSource<JsonObject>(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_gate) _pending[id] = completion;
        await SendAsync(new JsonObject { ["jsonrpc"] = "2.0", ["id"] = id, ["method"] = method, ["params"] = parameters ?? new JsonObject() });
        try
        {
            return await completion.Task.WaitAsync(Step);
        }
        catch (TimeoutException)
        {
            throw new TimeoutException($"O servidor MCP não respondeu a {method} em {Step.TotalSeconds:0} s.{Environment.NewLine}stderr: {Stderr}");
        }
    }

    public async Task<JsonObject> InitializeAsync(string client)
    {
        var reply = await CallAsync("initialize", new JsonObject
        {
            ["protocolVersion"] = "2025-06-18", ["capabilities"] = new JsonObject(),
            ["clientInfo"] = new JsonObject { ["name"] = client, ["version"] = "1.0" }
        });
        await SendAsync(new JsonObject { ["jsonrpc"] = "2.0", ["method"] = "notifications/initialized" });
        return reply;
    }

    public async Task<JsonObject> ToolAsync(string name, JsonObject arguments) =>
        (await CallAsync("tools/call", new JsonObject { ["name"] = name, ["arguments"] = arguments }))["result"]!.AsObject();

    // O fim limpo: a IDE fecha a entrada e o servidor sai com código 0.
    public async Task<int> CloseAsync()
    {
        _process.StandardInput.Close();
        using var limit = new CancellationTokenSource(Step);
        await _process.WaitForExitAsync(limit.Token);
        await Task.WhenAll(_stdoutPump, _stderrPump).WaitAsync(Step);
        return _process.ExitCode;
    }

    private async Task SendAsync(JsonObject message)
    {
        await _process.StandardInput.WriteLineAsync(message.ToJsonString().AsMemory()).WaitAsync(Step);
        await _process.StandardInput.FlushAsync().WaitAsync(Step);
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

    private async Task PumpStdoutAsync()
    {
        while (await _process.StandardOutput.ReadLineAsync() is { } line)
        {
            lock (_gate) _stdout.Add(line);
            if (Parse(line) is not { } message || message["id"] is not JsonValue idNode || !idNode.TryGetValue<int>(out var id)) continue;
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
        foreach (var orphan in orphans) orphan.TrySetException(new InvalidOperationException($"O servidor MCP encerrou antes de responder.{Environment.NewLine}stderr: {Stderr}"));
    }

    private async Task PumpStderrAsync()
    {
        while (await _process.StandardError.ReadLineAsync() is { } line)
        {
            lock (_gate) _stderr.Add(line);
        }
    }

    private static JsonObject? Parse(string line)
    {
        try { return JsonNode.Parse(line) as JsonObject; }
        catch (JsonException) { return null; }
    }
}
