using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Mariliz.Application.Audit;
using Mariliz.Application.Engineering;
using Mariliz.Application.Execution;
using Mariliz.Application.Harness;
using Mariliz.Application.Integrations;
using Mariliz.Application.Management;
using Mariliz.Application.Management.Assistant;
using Mariliz.Application.Management.Investigation;
using Mariliz.Application.Models;
using Mariliz.Application.Timeline;
using Mariliz.Application.Workflows;
using Mariliz.Application.Workspace;

namespace Mariliz.Application.Protocol;

/// <summary>
/// Laço do protocolo NDJSON v1: uma mensagem JSON por linha. Só escreve respostas e eventos no <c>output</c>;
/// diagnósticos são de quem hospeda a sessão (stderr). Execuções longas respondem com o <c>runId</c> e avisam o
/// progresso por eventos; pedidos que esperam rede ou modelo (<see cref="ProtocolMethods.Slow"/>) rodam fora do laço e respondem
/// quando terminam. Assim o laço nunca fica preso esperando um deles.
/// </summary>
public sealed class EngineSession
{
    private readonly string _engineVersion;
    private readonly string[] _capabilities;
    private readonly WorkspaceAuthorizer? _workspace;
    private readonly ConfigureRequestHandler? _configure;
    private readonly WorkflowRequestHandler? _workflows;
    private readonly RunRequestHandler? _runs;
    private readonly ModelRequestHandler? _models;
    private readonly IntegrationRequestHandler? _integrations;
    private readonly ManagementRequestHandler? _management;
    private readonly DecisionRequestHandler? _decisions;
    private readonly TimelineRequestHandler? _timeline;
    private readonly AuditRequestHandler? _audit;
    private readonly EngineeringRequestHandler? _engineering;
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
        WorkflowService? workflows = null,
        ModelGateway? models = null,
        RunQueries? runs = null,
        IntegrationHub? integrations = null,
        ManagementService? management = null,
        ManagementAssistant? assistant = null,
        TechnologyProfileService? profiles = null,
        ProjectKnowledgeService? knowledge = null,
        HarnessStatusService? harness = null,
        HarnessRevisionService? revisions = null,
        McpInventoryService? mcp = null,
        DecisionService? decisions = null,
        ReportService? reports = null,
        ProjectTimelineService? timeline = null,
        ProjectAuditService? audit = null,
        InvestigationService? investigation = null,
        PortfolioAssistant? portfolioAssistant = null)
    {
        _engineVersion = engineVersion;
        _workspace = workspace;
        _configure = configure is null ? null : new ConfigureRequestHandler(configure);
        _workflows = workflows is null ? null : new WorkflowRequestHandler(workflows);
        _models = models is null ? null : new ModelRequestHandler(models);
        _runs = runs is null ? null : new RunRequestHandler(runs);
        _integrations = integrations is null ? null : new IntegrationRequestHandler(integrations);
        _management = management is null ? null : new ManagementRequestHandler(management, assistant, reports, investigation, portfolioAssistant);
        _decisions = decisions is null ? null : new DecisionRequestHandler(decisions);
        _timeline = timeline is null ? null : new TimelineRequestHandler(timeline);
        _audit = audit is null ? null : new AuditRequestHandler(audit);
        _engineering = profiles is null ? null : new EngineeringRequestHandler(profiles, knowledge, harness, revisions, mcp);
        _tracker = tracker;
        if (relay is not null) relay.Target = WriteEvent;

        var offered = new List<string>(capabilities ?? []);
        if (workspace is not null) offered.Add(ProtocolMethods.AuthorizeWorkspace);
        if (configure is not null) offered.AddRange(ProtocolMethods.Configure);
        if (workflows is not null) offered.AddRange(ProtocolMethods.Workflow);
        if (runs is not null) offered.AddRange(ProtocolMethods.RunControl);
        if (models is not null) offered.AddRange(ProtocolMethods.Model);
        if (integrations is not null) offered.AddRange(ProtocolMethods.Integrations);
        if (management is not null) offered.AddRange(ProtocolMethods.Management);
        if (decisions is not null) offered.AddRange(ProtocolMethods.Decision);
        if (timeline is not null) offered.AddRange(ProtocolMethods.Timeline);
        if (audit is not null) offered.AddRange(ProtocolMethods.Audit);
        if (profiles is not null) offered.AddRange(ProtocolMethods.Engineering);
        _capabilities = [.. offered];
    }

    public async Task RunAsync(TextReader input, TextWriter output, CancellationToken ct)
    {
        _output = output;
        // Os pedidos longos rodam fora do laço; só o laço mexe nesta lista.
        var inFlight = new List<Task>();
        using var inFlightCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var line = await input.ReadLineAsync(ct);
                if (line is null)
                {
                    // A entrada fechou: os pedidos que ainda esperam o modelo ou a rede (cada um com o seu prazo) são respondidos.
                    await Task.WhenAll(inFlight);
                    return;
                }

                // Alguns escritores de stdin (ex.: PowerShell 5.1) prefixam a primeira linha com um BOM UTF-8.
                line = line.TrimStart('\uFEFF');
                if (string.IsNullOrWhiteSpace(line)) continue;
                inFlight.RemoveAll(task => task.IsCompleted);

                var (request, error) = ParseRequest(line);
                if (request is null)
                {
                    await WriteLineAsync(error!.ToJsonString());
                    continue;
                }

                if (ProtocolMethods.Slow.Contains(request.Method))
                {
                    inFlight.Add(RespondAsync(request, inFlightCts.Token));
                    continue;
                }

                var (response, shutdown) = await ExecuteAsync(request, ct);
                await WriteLineAsync(response.ToJsonString());
                if (shutdown) return;
            }
        }
        finally
        {
            // Ao encerrar, o que ainda espera o modelo ou a rede é cancelado, e depois nenhuma execução fica rodando.
            await inFlightCts.CancelAsync();
            try { await Task.WhenAll(inFlight).WaitAsync(TimeSpan.FromSeconds(10)); }
            catch (Exception ex) when (ex is TimeoutException or OperationCanceledException) { }
            if (_tracker is not null) await _tracker.CancelAllAndWaitAsync(TimeSpan.FromSeconds(15));
            if (_workflows is not null) await _workflows.ShutdownAsync();
            if (_integrations is not null) await _integrations.ShutdownAsync();
        }
    }

    internal (JsonObject Response, bool Shutdown) Handle(string line) => HandleAsync(line, CancellationToken.None).GetAwaiter().GetResult();

    internal async Task<(JsonObject Response, bool Shutdown)> HandleAsync(string line, CancellationToken ct)
    {
        var (request, error) = ParseRequest(line);
        return request is null ? Reply(error!) : await ExecuteAsync(request, ct);
    }

    private sealed record Request(string Id, string Method, JsonObject? Params);

    /// <summary>O envelope vem de fora: campo com tipo errado vira erro de protocolo, nunca exceção que derruba o processo.</summary>
    private static (Request? Request, JsonObject? Error) ParseRequest(string line)
    {
        if (Encoding.UTF8.GetByteCount(line) > ProtocolMessages.MaxMessageBytes)
            return (null, ProtocolMessages.Error(null, ProtocolErrorCodes.MessageTooLarge, "Mensagem acima do limite permitido."));

        var message = ParseObject(line);
        if (message is null)
            return (null, ProtocolMessages.Error(null, ProtocolErrorCodes.InvalidMessage, "A mensagem não é um objeto JSON válido."));

        var id = ReadString(message, "id");

        if (!(message["v"] is JsonValue version && version.TryGetValue<int>(out var number) && number == ProtocolMessages.Version))
            return (null, ProtocolMessages.Error(id, ProtocolErrorCodes.UnsupportedProtocol, "Versão de protocolo não suportada."));

        var method = ReadString(message, "method");
        if (ReadString(message, "kind") != "request" || id is null || method is null)
            return (null, ProtocolMessages.Error(id, ProtocolErrorCodes.InvalidMessage, "Requisição malformada."));

        return (new Request(id, method, message["params"] as JsonObject), null);
    }

    private async Task<(JsonObject Response, bool Shutdown)> ExecuteAsync(Request request, CancellationToken ct)
    {
        try
        {
            return await DispatchAsync(request.Id, request.Method, request.Params, ct);
        }
        catch (ParamException ex)
        {
            return Reply(ProtocolMessages.Error(request.Id, ProtocolErrorCodes.InvalidParams, ex.Message));
        }
        catch (EngineException ex)
        {
            return Reply(ProtocolMessages.Error(request.Id, ex.Code, ex.Message));
        }
        catch (OperationCanceledException)
        {
            return Reply(ProtocolMessages.Error(request.Id, ProtocolErrorCodes.Cancelled, "A operação foi cancelada."));
        }
        catch (Exception ex)
        {
            // Fronteira de cada pedido: nenhuma falha de um pedido derruba a sessão (e com ela o motor).
            // O texto da exceção pode conter caminhos ou dados do projeto: só o tipo vai para o protocolo.
            return Reply(ProtocolMessages.Error(request.Id, ProtocolErrorCodes.InternalError, $"Falha interna ({ex.GetType().Name})."));
        }
    }

    /// <summary>Um pedido longo: roda e escreve a própria resposta quando termina (a escrita é serializada com as demais).</summary>
    private async Task RespondAsync(Request request, CancellationToken ct)
    {
        var (response, _) = await ExecuteAsync(request, ct);
        try { await WriteLineAsync(response.ToJsonString()); }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException)
        {
            // A saída já fechou (a interface foi embora): não há para quem responder.
        }
    }

    private async Task<(JsonObject Response, bool Shutdown)> DispatchAsync(string id, string method, JsonObject? parameters, CancellationToken ct) =>
        method switch
        {
            ProtocolMethods.Hello => Reply(Hello(id, parameters)),
            ProtocolMethods.Shutdown => (ProtocolMessages.Ok(id, new JsonObject()), true),
            ProtocolMethods.AuthorizeWorkspace when _workspace is not null => Reply(Authorize(id, parameters)),
            ProtocolMethods.RuntimeHealth when _workflows is not null => Reply(Health(id)),
            _ when _models is not null && ProtocolMethods.Model.Contains(method) =>
                Reply(ProtocolMessages.Ok(id, await _models.HandleAsync(method, parameters, ct))),
            _ when _configure is not null && ProtocolMethods.Configure.Contains(method) =>
                Reply(ProtocolMessages.Ok(id, await _configure.HandleAsync(method, new RequestParams(parameters), ct))),
            _ when _runs is not null && ProtocolMethods.RunControl.Contains(method) =>
                Reply(ProtocolMessages.Ok(id, _runs.Handle(method, new RequestParams(parameters)))),
            _ when _workflows is not null && ProtocolMethods.Workflow.Contains(method) =>
                Reply(ProtocolMessages.Ok(id, await _workflows.HandleAsync(method, new RequestParams(parameters), ct))),
            _ when _integrations is not null && ProtocolMethods.Integrations.Contains(method) =>
                Reply(ProtocolMessages.Ok(id, await _integrations.HandleAsync(method, new RequestParams(parameters), ct))),
            _ when _management is not null && ProtocolMethods.Management.Contains(method) =>
                Reply(ProtocolMessages.Ok(id, await _management.HandleAsync(method, new RequestParams(parameters), ct))),
            _ when _decisions is not null && ProtocolMethods.Decision.Contains(method) =>
                Reply(ProtocolMessages.Ok(id, _decisions.Handle(method, new RequestParams(parameters)))),
            _ when _audit is not null && ProtocolMethods.Audit.Contains(method) =>
                Reply(ProtocolMessages.Ok(id, _audit.Handle(method, new RequestParams(parameters)))),
            _ when _timeline is not null && ProtocolMethods.Timeline.Contains(method) =>
                Reply(ProtocolMessages.Ok(id, _timeline.Handle(method, new RequestParams(parameters)))),
            _ when _engineering is not null && ProtocolMethods.Engineering.Contains(method) =>
                Reply(ProtocolMessages.Ok(id, await _engineering.HandleAsync(method, new RequestParams(parameters), ct))),
            _ => Reply(ProtocolMessages.Error(id, ProtocolErrorCodes.MethodNotFound, "Método desconhecido."))
        };

    private JsonObject Health(string id) =>
        ProtocolMessages.Ok(id, new JsonObject
        {
            ["engine"] = "ready",
            ["aiRuntime"] = _workflows!.AiRuntimeStatus()
        });

    private static string? ReadString(JsonObject? values, string name) =>
        values?[name] is JsonValue node && node.TryGetValue<string>(out var text) ? text : null;

    private static (JsonObject Response, bool Shutdown) Reply(JsonObject response) => (response, false);

    private static JsonObject? ParseObject(string line)
    {
        try { return JsonNode.Parse(line) as JsonObject; }
        catch (JsonException) { return null; }
    }

    private void WriteEvent(string runId, int seq, string type, JsonObject data, DateTimeOffset at) =>
        // Eventos vêm de outras threads; a escrita é serializada com as respostas.
        WriteLineAsync(ProtocolMessages.Event(runId, seq, type, data, at).ToJsonString()).GetAwaiter().GetResult();

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
