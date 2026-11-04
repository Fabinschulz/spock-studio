using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Mariliz.Application.Protocol;
using Mariliz.Application.Execution;
using Mariliz.Application.Ports;

namespace Mariliz.Infrastructure;

/// <summary>
/// Sobe <c>intelligence/server.py</c> em <c>127.0.0.1</c> com porta efêmera e token de sessão.
/// A entrada padrão do filho fica fechada, para não herdar o canal do protocolo.
/// </summary>
public sealed class LoopbackAiRuntime : IAiRuntime
{
    private const int MaxLine = 1_000_000;
    private static readonly TimeSpan StartupBudget = TimeSpan.FromSeconds(20);

    private readonly string? _scriptPath;
    private readonly string? _pythonOverride;
    private readonly object _gate = new();
    private readonly SemaphoreSlim _startup = new(1, 1);
    private Process? _process;
    private HttpClient? _http;

    public LoopbackAiRuntime(string? scriptPath = null, string? pythonPath = null)
    {
        _scriptPath = scriptPath ?? LocateScript();
        _pythonOverride = pythonPath;
    }

    public bool IsRunning
    {
        get { lock (_gate) return _process is { HasExited: false }; }
    }

    public string Probe()
    {
        if (IsRunning) return "ready";
        var script = _scriptPath;
        if (string.IsNullOrWhiteSpace(script) || !File.Exists(script)) return "unavailable";
        return PythonCandidates().Any() ? "stopped" : "unavailable";
    }

    /// <summary>
    /// Sobe o processo se ele não responde. A subida é serializada: duas execuções que encontrassem o runtime parado
    /// ao mesmo tempo derrubariam o processo uma da outra.
    /// </summary>
    public async Task EnsureReadyAsync(CancellationToken ct)
    {
        if (await HealthyAsync(ct)) return;

        await _startup.WaitAsync(ct);
        try
        {
            if (await HealthyAsync(ct)) return;
            await LaunchAsync(ct);
        }
        finally
        {
            _startup.Release();
        }
    }

    private async Task LaunchAsync(CancellationToken ct)
    {
        var script = _scriptPath;
        if (string.IsNullOrWhiteSpace(script) || !File.Exists(script))
            throw new EngineException("AI_RUNTIME_UNAVAILABLE", "O runtime de IA não está disponível nesta instalação.");

        var candidates = PythonCandidates().ToList();
        if (candidates.Count == 0)
            throw new EngineException("AI_RUNTIME_UNAVAILABLE", "O runtime de IA não está disponível nesta máquina.");

        foreach (var python in candidates)
        {
            ct.ThrowIfCancellationRequested();
            await ShutdownAsync();
            if (await TryStartAsync(python, script, ct)) return;
        }

        throw new EngineException("AI_RUNTIME_UNAVAILABLE", "O runtime de IA não respondeu.");
    }

    public async Task<IReadOnlyDictionary<string, string>> IndexedHashesAsync(string projectId, string indexRoot, CancellationToken ct)
    {
        await EnsureReadyAsync(ct);
        var ack = await PostAsync("v1/knowledge/manifest", new JsonObject { ["projectId"] = projectId, ["indexRoot"] = indexRoot }, ct);
        var hashes = new Dictionary<string, string>(StringComparer.Ordinal);
        if (ack["files"] is not JsonArray files) return hashes;
        foreach (var node in files)
        {
            if (node is not JsonObject item) continue;
            var path = item.Text("path");
            var hash = item.Text("hash");
            if (path.Length > 0 && hash.Length > 0) hashes[path] = hash;
        }
        return hashes;
    }

    public Task<WorkflowAck> StartAsync(JsonObject request, Action<JsonObject> onEvent, CancellationToken ct) =>
        StreamWorkflowAsync("v1/workflows", request, onEvent, ct);

    public Task<WorkflowAck> ResumeAsync(JsonObject request, Action<JsonObject> onEvent, CancellationToken ct) =>
        StreamWorkflowAsync($"v1/workflows/{Uri.EscapeDataString(request.Text("runId"))}/resume", request, onEvent, ct);

    public async Task CancelAsync(string runId, string? projectId = null, string? executionRoot = null)
    {
        try
        {
            if (!await HealthyAsync(CancellationToken.None)) return;
            var body = new JsonObject { ["runId"] = runId };
            if (!string.IsNullOrEmpty(projectId)) body["projectId"] = projectId;
            if (!string.IsNullOrEmpty(executionRoot)) body["executionRoot"] = executionRoot;
            await PostAsync($"v1/workflows/{Uri.EscapeDataString(runId)}/cancel", body, CancellationToken.None);
        }
        catch (Exception ex) when (ex is EngineException or HttpRequestException or IOException or OperationCanceledException)
        {
        }
    }

    /// <summary>
    /// Lê o NDJSON à medida que chega: cada <c>ai.event.v1</c> vai para <paramref name="onEvent"/> na hora, e a última linha
    /// precisa ser o <c>ai.ack.v2</c>. Se o fluxo terminar sem ele, a resposta está fora do contrato.
    /// </summary>
    private async Task<WorkflowAck> StreamWorkflowAsync(string path, JsonObject body, Action<JsonObject> onEvent, CancellationToken ct)
    {
        await EnsureReadyAsync(ct);
        HttpClient? http;
        lock (_gate) http = _http;
        if (http is null) throw new EngineException("AI_RUNTIME_UNAVAILABLE", "O runtime de IA não está disponível nesta máquina.");

        using var message = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json")
        };
        using var response = await http.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!response.IsSuccessStatusCode || response.Content.Headers.ContentType?.MediaType != "application/x-ndjson")
            throw new EngineException("AI_RUNTIME_INVALID_RESPONSE", "O runtime de IA respondeu fora do contrato.");

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        while (await reader.ReadLineAsync(ct) is { } line)
        {
            if (line.Length == 0) continue;
            if (line.Length > MaxLine || JsonNode.Parse(line) is not JsonObject item)
                throw new EngineException("AI_RUNTIME_INVALID_RESPONSE", "O runtime de IA respondeu fora do contrato.");
            switch (item.Text("schema"))
            {
                case "ai.event.v1":
                    onEvent(item);
                    break;
                case "ai.ack.v2":
                    return ReadAck(item);
                default:
                    throw new EngineException("AI_RUNTIME_INVALID_RESPONSE", "O runtime de IA respondeu fora do contrato.");
            }
        }

        throw new EngineException("AI_RUNTIME_INVALID_RESPONSE", "O runtime de IA respondeu fora do contrato.");
    }

    private static WorkflowAck ReadAck(JsonObject ack)
    {
        var warnings = ReadNotices(ack, "warnings");
        var errors = ReadNotices(ack, "errors");
        var interrupt = ack["approval"] is JsonObject approval ? approval.Text("interruptId") : "";
        return new WorkflowAck(
            $"{ack.Text("workflowId")}.v{ack.Text("workflowVersion")}",
            ack.Text("runId"),
            ack.Text("projectId"),
            ack.Text("executionId"),
            ack.Text("status"),
            ack["output"] as JsonObject ?? new JsonObject(),
            [.. warnings, .. errors],
            errors,
            IsInterruptId(interrupt) ? interrupt : null);
    }

    private static bool IsInterruptId(string value) =>
        value.Length == 16 && value.StartsWith("int-", StringComparison.Ordinal) && value[4..].All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');

    public async Task ShutdownAsync()
    {
        Process? process;
        HttpClient? http;
        lock (_gate)
        {
            process = _process;
            http = _http;
            _process = null;
            _http = null;
        }

        http?.Dispose();
        if (process is null) return;
        try
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException) { }

        try { await process.WaitForExitAsync(CancellationToken.None); }
        catch (InvalidOperationException) { }
        process.Dispose();
    }

    internal static string? LocateScript()
    {
        var env = Environment.GetEnvironmentVariable("MARILIZ_AI");
        if (!string.IsNullOrWhiteSpace(env)) return env;

        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (var i = 0; i < 8 && dir is not null; i++, dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "intelligence", "server.py");
            if (File.Exists(candidate)) return candidate;
        }

        return null;
    }

    private async Task<bool> TryStartAsync(string python, string script, CancellationToken ct)
    {
        var port = FreeLoopbackPort();
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var psi = new ProcessStartInfo(python)
        {
            WorkingDirectory = Path.GetDirectoryName(script)!,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        psi.ArgumentList.Add(script);
        psi.ArgumentList.Add("--host");
        psi.ArgumentList.Add("127.0.0.1");
        psi.ArgumentList.Add("--port");
        psi.ArgumentList.Add(port.ToString());
        psi.Environment["MARILIZ_AI_TOKEN"] = token;
        psi.Environment["PYTHONUNBUFFERED"] = "1";
        psi.Environment["PYTHONIOENCODING"] = "utf-8";
        // A inteligência não fala com serviços de telemetria; o servidor também desliga, isto vale antes do primeiro import.
        psi.Environment["LANGSMITH_TRACING"] = "false";
        psi.Environment["LANGCHAIN_TRACING_V2"] = "false";
        psi.Environment["LANGSMITH_OTEL_ENABLED"] = "false";

        Process process;
        try { process = Process.Start(psi) ?? throw new InvalidOperationException(); }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or FileNotFoundException)
        {
            return false;
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        try { process.StandardInput.Close(); }
        catch (IOException) { }

        var http = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}/"), Timeout = TimeSpan.FromSeconds(10) };
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        lock (_gate)
        {
            _process = process;
            _http = http;
        }

        if (await HealthyAsync(ct)) return true;
        await ShutdownAsync();
        return false;
    }

    private async Task<bool> HealthyAsync(CancellationToken ct)
    {
        HttpClient? http;
        Process? process;
        lock (_gate)
        {
            http = _http;
            process = _process;
        }
        if (http is null || process is null) return false;

        // A primeira importação do LangGraph sem bytecode pronto leva alguns segundos (medido: 2,5 s).
        var deadline = DateTime.UtcNow + StartupBudget;
        while (DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();
            if (process.HasExited) return false;
            try
            {
                using var response = await http.GetAsync("health", ct);
                if (response.IsSuccessStatusCode) return true;
            }
            catch (HttpRequestException) { }
            catch (TaskCanceledException) when (!ct.IsCancellationRequested) { }
            await Task.Delay(100, ct);
        }

        return false;
    }

    private async Task<JsonObject> PostAsync(string path, JsonObject body, CancellationToken ct)
    {
        HttpClient? http;
        lock (_gate) http = _http;
        if (http is null) throw new EngineException("AI_RUNTIME_UNAVAILABLE", "O runtime de IA não está disponível nesta máquina.");

        using var content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
        using var response = await http.PostAsync(path, content, ct);
        var text = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode || JsonNode.Parse(text) is not JsonObject ack)
            throw new EngineException("AI_RUNTIME_INVALID_RESPONSE", "O runtime de IA respondeu fora do contrato.");
        return ack;
    }

    private static IReadOnlyList<WorkflowLimitation> ReadNotices(JsonObject ack, string name)
    {
        if (ack[name] is not JsonArray array) return [];
        var items = new List<WorkflowLimitation>();
        foreach (var node in array)
        {
            if (node is not JsonObject item) continue;
            var code = item.Text("code");
            if (code.Length > 0) items.Add(new WorkflowLimitation(code, item.Text("message")));
        }
        return items;
    }

    private IEnumerable<string> PythonCandidates() =>
        PythonRuntimeLayout.Candidates(
            AppContext.BaseDirectory,
            _pythonOverride,
            Environment.GetEnvironmentVariable("MARILIZ_PYTHON"),
            Environment.GetEnvironmentVariable("PATH"));

    private static int FreeLoopbackPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
