using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Mariliz.Application.Execution;
using Mariliz.Application.Ports;

namespace Mariliz.Infrastructure;

/// <summary>
/// Sobe <c>intelligence/server.py</c> em <c>127.0.0.1</c> com porta efêmera e token de sessão.
/// A entrada padrão do filho fica fechada, para não herdar o canal do protocolo.
/// </summary>
public sealed class LoopbackAiRuntime : IAiRuntime
{
    private readonly string? _scriptPath;
    private readonly string? _pythonOverride;
    private readonly object _gate = new();
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

    public async Task EnsureReadyAsync(CancellationToken ct)
    {
        if (await HealthyAsync(ct)) return;

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
            var path = Text(item, "path");
            var hash = Text(item, "hash");
            if (path.Length > 0 && hash.Length > 0) hashes[path] = hash;
        }
        return hashes;
    }

    public Task<WorkflowAck> StartAsync(string runId, string projectId, string executionId, JsonObject input, JsonObject context, CancellationToken ct) =>
        PostWorkflowAsync("v1/workflows", runId, projectId, executionId, input, context, ct);

    public Task<WorkflowAck> ResumeAsync(string runId, string projectId, string executionId, CancellationToken ct) =>
        PostWorkflowAsync($"v1/workflows/{Uri.EscapeDataString(runId)}/resume", runId, projectId, executionId, new JsonObject { ["approved"] = true }, new JsonObject(), ct);

    public async Task CancelAsync(string runId)
    {
        try
        {
            if (!await HealthyAsync(CancellationToken.None)) return;
            await PostAsync($"v1/workflows/{Uri.EscapeDataString(runId)}/cancel", new JsonObject { ["runId"] = runId }, CancellationToken.None);
        }
        catch (Exception ex) when (ex is EngineException or HttpRequestException or IOException or OperationCanceledException)
        {
        }
    }

    private async Task<WorkflowAck> PostWorkflowAsync(
        string path, string runId, string projectId, string executionId, JsonObject input, JsonObject context, CancellationToken ct)
    {
        await EnsureReadyAsync(ct);
        var body = new JsonObject
        {
            ["schema"] = "workflow.start.v1",
            ["runId"] = runId,
            ["projectId"] = projectId,
            ["executionId"] = executionId,
            ["input"] = input.DeepClone(),
            ["context"] = context.DeepClone()
        };
        var ack = await PostAsync(path, body, ct);
        return new WorkflowAck(
            Text(ack, "workflowId"),
            Text(ack, "runId"),
            Text(ack, "projectId"),
            Text(ack, "executionId"),
            Text(ack, "status"),
            ack["output"] as JsonObject ?? new JsonObject(),
            ReadLimitations(ack));
    }

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

        for (var attempt = 0; attempt < 50; attempt++)
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

    private static string Text(JsonObject ack, string name) =>
        ack[name] is JsonValue value && value.TryGetValue<string>(out var text) ? text : "";

    private static IReadOnlyList<WorkflowLimitation> ReadLimitations(JsonObject ack)
    {
        if (ack["limitations"] is not JsonArray array) return [];
        var items = new List<WorkflowLimitation>();
        foreach (var node in array)
        {
            if (node is not JsonObject item) continue;
            var code = Text(item, "code");
            if (code.Length > 0) items.Add(new WorkflowLimitation(code, Text(item, "message")));
        }
        return items;
    }

    private IEnumerable<string> PythonCandidates()
    {
        if (!string.IsNullOrWhiteSpace(_pythonOverride))
        {
            yield return _pythonOverride;
            yield break;
        }

        var names = OperatingSystem.IsWindows() ? new[] { "py", "python3", "python" } : new[] { "python3", "python" };
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in names)
        {
            var found = FindOnPath(name);
            if (found is not null && seen.Add(found)) yield return found;
        }
    }

    private static string? FindOnPath(string name)
    {
        var extensions = OperatingSystem.IsWindows() ? new[] { ".exe", ".cmd", "" } : new[] { "" };
        foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            foreach (var ext in extensions)
            {
                try
                {
                    var candidate = Path.Combine(dir.Trim().Trim('"'), name + ext);
                    if (File.Exists(candidate)) return candidate;
                }
                catch (ArgumentException) { }
            }
        }

        return null;
    }

    private static int FreeLoopbackPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
