using System.Text.Json;
using System.Text.Json.Nodes;

using Mariliz.Application.Discovery;
using Mariliz.Application.Execution;
using Mariliz.Application.Harness;
using Mariliz.Application.Workspace;
using Mariliz.Core.Plan;

namespace Mariliz.Application.Protocol;

/// <summary>Liga os eventos das execuções ao fluxo de saída da sessão, que só existe depois de a sessão ser criada.</summary>
public sealed class EventRelay
{
    public Action<string, int, string, JsonObject>? Target { get; set; }
    public void Emit(string runId, int seq, string type, JsonObject data) => Target?.Invoke(runId, seq, type, data);
}

/// <summary>
/// Laço do protocolo NDJSON v1: uma mensagem JSON por linha. Só escreve respostas e eventos no <c>output</c>;
/// diagnósticos são responsabilidade de quem hospeda a sessão (stderr). Execuções longas respondem com o
/// <c>runId</c> e avisam o progresso por eventos, então o laço nunca fica preso esperando uma delas.
/// </summary>
public sealed class EngineSession
{
    public const int ProtocolVersion = 1;
    public const int MaxMessageBytes = 4 * 1024 * 1024;

    private readonly string _engineVersion;
    private readonly string[] _capabilities;
    private readonly WorkspaceAuthorizer? _workspace;
    private readonly ConfigureProjectService? _configure;
    private readonly RunTracker? _tracker;
    private readonly SemaphoreSlim _outputGate = new(1, 1);
    private TextWriter? _output;

    public EngineSession(
        string engineVersion,
        IEnumerable<string>? capabilities = null,
        WorkspaceAuthorizer? workspace = null,
        ConfigureProjectService? configure = null,
        EventRelay? relay = null,
        RunTracker? tracker = null)
    {
        _engineVersion = engineVersion;
        _workspace = workspace;
        _configure = configure;
        _tracker = tracker;
        if (relay is not null) relay.Target = WriteEvent;

        var extra = new List<string>();
        if (workspace is not null) extra.Add("workspace.authorize");
        if (configure is not null) extra.AddRange(["project.discover", "harness.plan", "harness.apply", "harness.validate", "harness.revert", "harness.runs", "run.snapshot", "run.cancel"]);
        _capabilities = (capabilities ?? []).Concat(extra).ToArray();
    }

    public async Task RunAsync(TextReader input, TextWriter output, CancellationToken ct)
    {
        _output = output;
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var line = await input.ReadLineAsync(ct);
                if (line is null) return;
                // Alguns escritores de stdin (ex.: PowerShell 5.1) prefixam a primeira linha com um BOM UTF-8.
                line = line.TrimStart('﻿');
                if (string.IsNullOrWhiteSpace(line)) continue;

                var (response, shutdown) = await HandleAsync(line, ct);
                await WriteLineAsync(response.ToJsonString());
                if (shutdown) return;
            }
        }
        finally
        {
            // Sem a interface (ou ao encerrar), nenhuma execução fica rodando: o que está no meio é desfeito.
            if (_tracker is not null) await _tracker.CancelAllAndWaitAsync(TimeSpan.FromSeconds(15));
        }
    }

    internal (JsonObject Response, bool Shutdown) Handle(string line) => HandleAsync(line, CancellationToken.None).GetAwaiter().GetResult();

    internal async Task<(JsonObject Response, bool Shutdown)> HandleAsync(string line, CancellationToken ct)
    {
        if (System.Text.Encoding.UTF8.GetByteCount(line) > MaxMessageBytes)
            return (Error(null, ProtocolErrorCodes.MessageTooLarge, "Mensagem acima do limite permitido."), false);

        JsonObject? message;
        try { message = JsonNode.Parse(line) as JsonObject; }
        catch (JsonException) { message = null; }

        if (message is null)
            return (Error(null, ProtocolErrorCodes.InvalidMessage, "A mensagem não é um objeto JSON válido."), false);

        var id = message["id"]?.GetValue<string>();

        if (message["v"]?.GetValue<int>() != ProtocolVersion)
            return (Error(id, ProtocolErrorCodes.UnsupportedProtocol, "Versão de protocolo não suportada."), false);

        if (message["kind"]?.GetValue<string>() != "request" || id is null || message["method"] is not JsonValue methodNode)
            return (Error(id, ProtocolErrorCodes.InvalidMessage, "Requisição malformada."), false);

        var method = methodNode.GetValue<string>();
        var p = message["params"] as JsonObject;

        try
        {
            return method switch
            {
                "engine.hello" => (Hello(id, p), false),
                "engine.shutdown" => (Ok(id, new JsonObject()), true),
                "workspace.authorize" when _workspace is not null => (Authorize(id, p), false),
                _ when _configure is not null && IsConfigureMethod(method) => (await HandleConfigureAsync(id, method, p, ct), false),
                _ => (Error(id, ProtocolErrorCodes.MethodNotFound, "Método desconhecido."), false)
            };
        }
        catch (ParamException ex)
        {
            return (Error(id, ProtocolErrorCodes.InvalidParams, ex.Message), false);
        }
        catch (EngineException ex)
        {
            return (Error(id, ex.Code, ex.Message), false);
        }
        catch (OperationCanceledException)
        {
            return (Error(id, "CANCELLED", "A operação foi cancelada."), false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or JsonException)
        {
            // O texto da exceção pode conter caminhos ou dados do projeto: só o tipo vai para o protocolo.
            return (Error(id, "INTERNAL_ERROR", $"Falha interna ({ex.GetType().Name})."), false);
        }
    }

    private static bool IsConfigureMethod(string method) =>
        method is "project.discover" or "harness.plan" or "harness.apply" or "harness.validate" or "harness.revert"
            or "harness.runs" or "run.snapshot" or "run.cancel";

    private async Task<JsonObject> HandleConfigureAsync(string id, string method, JsonObject? p, CancellationToken ct)
    {
        var service = _configure!;
        switch (method)
        {
            case "project.discover":
                return Ok(id, Json(await service.DiscoverAsync(Str(p, "projectId"), ct)));

            case "harness.plan":
            {
                var env = OptionalStr(p, "aiEnvironment");
                if (env is not null && !AiEnvironments.Known.Contains(env)) throw new ParamException("aiEnvironment desconhecido.");
                var force = p?["allowTlcForceReinstall"]?.GetValue<bool>() ?? false;
                var updateTlc = p?["updateTlc"]?.GetValue<bool>() ?? false;
                return Ok(id, Json(await service.PlanAsync(Str(p, "projectId"), new PlanOptions(env, force, updateTlc), ct)));
            }

            case "harness.apply":
            {
                var resolutions = new Dictionary<string, ConflictResolution>();
                if (p?["resolutions"] is JsonObject map)
                    foreach (var (key, value) in map)
                        resolutions[key] = value?.GetValue<string>() switch
                        {
                            "keep" => ConflictResolution.Keep,
                            "overwrite" => ConflictResolution.Overwrite,
                            _ => throw new ParamException("resolutions só aceita 'keep' ou 'overwrite'.")
                        };
                var approvals = new ApplyApprovals(StrList(p, "approvedChangeIds"), StrList(p, "approvedCommandIds"), resolutions);
                var run = service.StartApply(Str(p, "projectId"), Str(p, "planId"), approvals);
                return Ok(id, new JsonObject { ["runId"] = run.RunId });
            }

            case "harness.validate":
                return Ok(id, new JsonObject { ["runId"] = service.StartValidate(Str(p, "projectId")).RunId });

            case "harness.revert":
                return Ok(id, new JsonObject { ["runId"] = service.StartRevert(Str(p, "projectId"), Str(p, "runId")).RunId });

            case "harness.runs":
                return Ok(id, new JsonObject { ["runs"] = ConfigureProjectService.ToJson(service.ListRuns(Str(p, "projectId"))) });

            case "run.snapshot":
                return Ok(id, service.Snapshot(Str(p, "runId")));

            case "run.cancel":
                return Ok(id, new JsonObject { ["cancelled"] = service.Cancel(Str(p, "runId")) });
        }
        return Error(id, ProtocolErrorCodes.MethodNotFound, "Método desconhecido.");
    }

    // ------------------------------------------------------------------ Saída

    private void WriteEvent(string runId, int seq, string type, JsonObject data)
    {
        var message = new JsonObject
        {
            ["v"] = ProtocolVersion, ["kind"] = "event", ["runId"] = runId, ["type"] = type, ["seq"] = seq, ["data"] = data.DeepClone()
        };
        // Eventos vêm de outras threads; a escrita é serializada com as respostas.
        WriteLineAsync(message.ToJsonString()).GetAwaiter().GetResult();
    }

    private async Task WriteLineAsync(string json)
    {
        var output = _output;
        if (output is null) return;
        await _outputGate.WaitAsync();
        try
        {
            await output.WriteLineAsync(json);
            await output.FlushAsync();
        }
        finally { _outputGate.Release(); }
    }

    // ------------------------------------------------------------------ Métodos simples

    private JsonObject Hello(string id, JsonObject? parameters)
    {
        var offered = parameters?["protocol"] as JsonArray;
        if (offered is null || parameters?["hostVersion"] is null)
            return Error(id, ProtocolErrorCodes.InvalidParams, "Parâmetros de engine.hello inválidos.");

        if (!offered.Any(n => n is JsonValue v && v.TryGetValue<int>(out var p) && p == ProtocolVersion))
            return Error(id, ProtocolErrorCodes.UnsupportedProtocol, "Nenhuma versão de protocolo em comum.");

        return Ok(id, new JsonObject
        {
            ["protocol"] = ProtocolVersion,
            ["engineVersion"] = _engineVersion,
            ["capabilities"] = new JsonArray(_capabilities.Select(c => (JsonNode)JsonValue.Create(c)!).ToArray())
        });
    }

    private JsonObject Authorize(string id, JsonObject? parameters)
    {
        if (parameters?["path"] is not JsonValue pathNode || !pathNode.TryGetValue<string>(out var path))
            return Error(id, ProtocolErrorCodes.InvalidParams, "Parâmetros de workspace.authorize inválidos.");

        var result = _workspace!.Authorize(path);
        if (result.Project is { } p)
            return Ok(id, new JsonObject { ["projectId"] = p.ProjectId, ["displayName"] = p.DisplayName, ["displayPath"] = p.DisplayPath });

        return result.Error switch
        {
            WorkspaceError.NotFound => Error(id, ProtocolErrorCodes.PathNotFound, "A pasta escolhida não foi encontrada."),
            WorkspaceError.NotAllowed => Error(id, ProtocolErrorCodes.PathNotAllowed, "Escolha a pasta de um projeto, não a raiz de uma unidade."),
            _ => Error(id, ProtocolErrorCodes.PathNotReadable, "Não há permissão para ler a pasta escolhida.")
        };
    }

    // ------------------------------------------------------------------ Utilidades

    private sealed class ParamException(string message) : Exception(message);

    private static string Str(JsonObject? p, string name) =>
        p?[name] is JsonValue v && v.TryGetValue<string>(out var s) && !string.IsNullOrWhiteSpace(s) ? s : throw new ParamException($"'{name}' é obrigatório.");

    private static string? OptionalStr(JsonObject? p, string name) =>
        p?[name] is JsonValue v && v.TryGetValue<string>(out var s) && !string.IsNullOrWhiteSpace(s) ? s : null;

    private static List<string> StrList(JsonObject? p, string name)
    {
        if (p?[name] is null) return [];
        if (p[name] is not JsonArray array) throw new ParamException($"'{name}' precisa ser uma lista.");
        return array.Select(n => n is JsonValue v && v.TryGetValue<string>(out var s) ? s : throw new ParamException($"'{name}' só aceita textos.")).ToList();
    }

    private static JsonObject Json<T>(T value) => (JsonObject)ConfigureProjectService.ToJson(value)!;

    private static JsonObject Ok(string id, JsonObject result) => new()
    {
        ["v"] = ProtocolVersion, ["kind"] = "response", ["id"] = id, ["result"] = result
    };

    private static JsonObject Error(string? id, string code, string message) => new()
    {
        ["v"] = ProtocolVersion, ["kind"] = "response", ["id"] = id,
        ["error"] = new JsonObject { ["code"] = code, ["message"] = message }
    };
}
