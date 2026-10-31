using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Mariliz.Application.Execution;
using Mariliz.Application.Harness;
using Mariliz.Application.Workflows;
using Mariliz.Application.Workspace;

namespace Mariliz.Application.Protocol;

/// <summary>
/// Laço do protocolo NDJSON v1: uma mensagem JSON por linha. Só escreve respostas e eventos no <c>output</c>;
/// diagnósticos são de quem hospeda a sessão (stderr). Execuções longas respondem com o <c>runId</c> e avisam o
/// progresso por eventos, então o laço nunca fica preso esperando uma delas.
/// </summary>
public sealed class EngineSession
{
    private readonly string _engineVersion;
    private readonly string[] _capabilities;
    private readonly WorkspaceAuthorizer? _workspace;
    private readonly ConfigureRequestHandler? _configure;
    private readonly WorkflowRequestHandler? _workflows;
    private readonly RunTracker? _tracker;
    private readonly SemaphoreSlim _outputGate = new(1, 1);
    private TextWriter? _output;

    public EngineSession(
        string engineVersion,
        IEnumerable<string>? capabilities = null,
        WorkspaceAuthorizer? workspace = null,
        ConfigureProjectService? configure = null,
        EventRelay? relay = null,
        RunTracker? tracker = null,
        WorkflowService? workflows = null)
    {
        _engineVersion = engineVersion;
        _workspace = workspace;
        _configure = configure is null ? null : new ConfigureRequestHandler(configure);
        _workflows = workflows is null ? null : new WorkflowRequestHandler(workflows);
        _tracker = tracker;
        if (relay is not null) relay.Target = WriteEvent;

        var offered = new List<string>(capabilities ?? []);
        if (workspace is not null) offered.Add(ProtocolMethods.AuthorizeWorkspace);
        if (configure is not null) offered.AddRange(ProtocolMethods.Configure);
        if (workflows is not null) offered.AddRange(ProtocolMethods.Workflow);
        _capabilities = [.. offered];
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
            if (_workflows is not null) await _workflows.ShutdownAsync();
        }
    }

    internal (JsonObject Response, bool Shutdown) Handle(string line) => HandleAsync(line, CancellationToken.None).GetAwaiter().GetResult();

    internal async Task<(JsonObject Response, bool Shutdown)> HandleAsync(string line, CancellationToken ct)
    {
        if (Encoding.UTF8.GetByteCount(line) > ProtocolMessages.MaxMessageBytes)
            return Reply(ProtocolMessages.Error(null, ProtocolErrorCodes.MessageTooLarge, "Mensagem acima do limite permitido."));

        var message = ParseObject(line);
        if (message is null)
            return Reply(ProtocolMessages.Error(null, ProtocolErrorCodes.InvalidMessage, "A mensagem não é um objeto JSON válido."));

        var id = message["id"]?.GetValue<string>();

        if (message["v"]?.GetValue<int>() != ProtocolMessages.Version)
            return Reply(ProtocolMessages.Error(id, ProtocolErrorCodes.UnsupportedProtocol, "Versão de protocolo não suportada."));

        if (message["kind"]?.GetValue<string>() != "request" || id is null || message["method"] is not JsonValue methodNode)
            return Reply(ProtocolMessages.Error(id, ProtocolErrorCodes.InvalidMessage, "Requisição malformada."));

        var method = methodNode.GetValue<string>();
        var parameters = message["params"] as JsonObject;

        try
        {
            return await DispatchAsync(id, method, parameters, ct);
        }
        catch (ParamException ex)
        {
            return Reply(ProtocolMessages.Error(id, ProtocolErrorCodes.InvalidParams, ex.Message));
        }
        catch (EngineException ex)
        {
            return Reply(ProtocolMessages.Error(id, ex.Code, ex.Message));
        }
        catch (OperationCanceledException)
        {
            return Reply(ProtocolMessages.Error(id, "CANCELLED", "A operação foi cancelada."));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or JsonException)
        {
            // O texto da exceção pode conter caminhos ou dados do projeto: só o tipo vai para o protocolo.
            return Reply(ProtocolMessages.Error(id, "INTERNAL_ERROR", $"Falha interna ({ex.GetType().Name})."));
        }
    }

    private async Task<(JsonObject Response, bool Shutdown)> DispatchAsync(string id, string method, JsonObject? parameters, CancellationToken ct)
    {
        switch (method)
        {
            case ProtocolMethods.Hello:
                return Reply(Hello(id, parameters));
            case ProtocolMethods.Shutdown:
                return (ProtocolMessages.Ok(id, new JsonObject()), true);
            case ProtocolMethods.AuthorizeWorkspace when _workspace is not null:
                return Reply(Authorize(id, parameters));
            case ProtocolMethods.RuntimeHealth when _workflows is not null:
                return Reply(ProtocolMessages.Ok(id, new JsonObject
                {
                    ["engine"] = "ready",
                    ["aiRuntime"] = _workflows.AiRuntimeStatus()
                }));
        }

        if (_configure is not null && ProtocolMethods.Configure.Contains(method))
            return Reply(ProtocolMessages.Ok(id, await _configure.HandleAsync(method, new RequestParams(parameters), ct)));

        if (_workflows is not null && ProtocolMethods.Workflow.Contains(method))
            return Reply(ProtocolMessages.Ok(id, await _workflows.HandleAsync(method, new RequestParams(parameters), ct)));

        return Reply(ProtocolMessages.Error(id, ProtocolErrorCodes.MethodNotFound, "Método desconhecido."));
    }

    private static (JsonObject Response, bool Shutdown) Reply(JsonObject response) => (response, false);

    private static JsonObject? ParseObject(string line)
    {
        try { return JsonNode.Parse(line) as JsonObject; }
        catch (JsonException) { return null; }
    }

    private void WriteEvent(string runId, int seq, string type, JsonObject data) =>
        // Eventos vêm de outras threads; a escrita é serializada com as respostas.
        WriteLineAsync(ProtocolMessages.Event(runId, seq, type, data).ToJsonString()).GetAwaiter().GetResult();

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

    private JsonObject Hello(string id, JsonObject? parameters)
    {
        if (parameters?["protocol"] is not JsonArray offered || parameters["hostVersion"] is null)
            return ProtocolMessages.Error(id, ProtocolErrorCodes.InvalidParams, "Parâmetros de engine.hello inválidos.");

        if (!offered.Any(n => n is JsonValue v && v.TryGetValue<int>(out var version) && version == ProtocolMessages.Version))
            return ProtocolMessages.Error(id, ProtocolErrorCodes.UnsupportedProtocol, "Nenhuma versão de protocolo em comum.");

        return ProtocolMessages.Ok(id, new JsonObject
        {
            ["protocol"] = ProtocolMessages.Version,
            ["engineVersion"] = _engineVersion,
            ["capabilities"] = new JsonArray(_capabilities.Select(c => (JsonNode)JsonValue.Create(c)!).ToArray())
        });
    }

    private JsonObject Authorize(string id, JsonObject? parameters)
    {
        if (parameters?["path"] is not JsonValue pathNode || !pathNode.TryGetValue<string>(out var path))
            return ProtocolMessages.Error(id, ProtocolErrorCodes.InvalidParams, "Parâmetros de workspace.authorize inválidos.");

        var result = _workspace!.Authorize(path);
        if (result.Project is { } project)
        {
            return ProtocolMessages.Ok(id, new JsonObject
            {
                ["projectId"] = project.ProjectId, ["displayName"] = project.DisplayName, ["displayPath"] = project.DisplayPath
            });
        }

        return result.Error switch
        {
            WorkspaceError.NotFound => ProtocolMessages.Error(id, ProtocolErrorCodes.PathNotFound, "A pasta escolhida não foi encontrada."),
            WorkspaceError.NotAllowed => ProtocolMessages.Error(id, ProtocolErrorCodes.PathNotAllowed, "Escolha a pasta de um projeto, não a raiz de uma unidade."),
            _ => ProtocolMessages.Error(id, ProtocolErrorCodes.PathNotReadable, "Não há permissão para ler a pasta escolhida.")
        };
    }
}
