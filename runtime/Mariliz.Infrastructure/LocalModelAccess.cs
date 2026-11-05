using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Mariliz.Application.Models;

namespace Mariliz.Infrastructure;

/// <summary>
/// O acesso ao modelo das execuções: um <see cref="ModelBroker"/> e o servidor HTTP que o expõe só em 127.0.0.1. O servidor
/// sobe na primeira execução que pede modelo e responde a uma rota (<c>POST /v1/model</c>) com token por execução.
/// </summary>
public sealed class LocalModelAccess(ModelBroker broker) : IModelAccess, IDisposable
{
    private const int MaxBody = 1_000_000;
    private readonly object _gate = new();
    private readonly CancellationTokenSource _stop = new();
    private HttpListener? _listener;
    private string _url = "";

    public bool Configured => broker.Configured;

    public ModelGrant Open(string executionId, int tokenBudget)
    {
        var url = EnsureStarted();
        return new ModelGrant(url, broker.Open(executionId, tokenBudget), tokenBudget);
    }

    public ModelUsage Close(string executionId) => broker.Close(executionId);

    public IReadOnlyList<ModelCallRecord> Calls(string executionId) => broker.Calls(executionId);

    public void Dispose()
    {
        _stop.Cancel();
        lock (_gate) _listener?.Close();
        _stop.Dispose();
    }

    private string EnsureStarted()
    {
        lock (_gate)
        {
            if (_listener is not null) return _url;
            for (var attempt = 0; attempt < 5; attempt++)
            {
                var port = FreePort();
                var listener = new HttpListener();
                listener.Prefixes.Add($"http://127.0.0.1:{port}/");
                try
                {
                    listener.Start();
                }
                catch (HttpListenerException)
                {
                    listener.Close();
                    continue;
                }
                _listener = listener;
                _url = $"http://127.0.0.1:{port}/v1/model";
                _ = Task.Run(() => ServeAsync(listener));
                return _url;
            }
        }
        throw new InvalidOperationException("Não foi possível abrir o proxy de modelo em 127.0.0.1.");
    }

    private static int FreePort()
    {
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        try { return ((IPEndPoint)probe.LocalEndpoint).Port; }
        finally { probe.Stop(); }
    }

    private async Task ServeAsync(HttpListener listener)
    {
        while (!_stop.IsCancellationRequested)
        {
            HttpListenerContext context;
            try { context = await listener.GetContextAsync(); }
            catch (Exception ex) when (ex is HttpListenerException or ObjectDisposedException or InvalidOperationException) { return; }
            _ = Task.Run(() => HandleAsync(context));
        }
    }

    private async Task HandleAsync(HttpListenerContext context)
    {
        try
        {
            var reply = await ReplyAsync(context.Request);
            var bytes = Encoding.UTF8.GetBytes(reply.Body.ToJsonString());
            context.Response.StatusCode = reply.HttpStatus;
            context.Response.ContentType = "application/json; charset=utf-8";
            context.Response.ContentLength64 = bytes.Length;
            await context.Response.OutputStream.WriteAsync(bytes, _stop.Token);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // Sem detalhe na resposta: o texto de uma exceção pode ter caminhos ou trechos do pedido.
            try { context.Response.StatusCode = 500; } catch (Exception) { /* a conexão já caiu */ }
        }
        finally
        {
            try { context.Response.Close(); } catch (Exception) { /* a conexão já caiu */ }
        }
    }

    private async Task<BrokerReply> ReplyAsync(HttpListenerRequest request)
    {
        if (request.HttpMethod != "POST" || request.Url?.AbsolutePath != "/v1/model")
            return new BrokerReply(404, new JsonObject { ["error"] = "not_found" });
        if (request.ContentLength64 is < 0 or > MaxBody)
            return new BrokerReply(413, new JsonObject { ["error"] = "payload_too_large" });

        var header = request.Headers["Authorization"];
        var bearer = header is not null && header.StartsWith("Bearer ", StringComparison.Ordinal) ? header["Bearer ".Length..] : null;

        JsonObject? body;
        try
        {
            using var reader = new StreamReader(request.InputStream, Encoding.UTF8);
            body = JsonNode.Parse(await reader.ReadToEndAsync(_stop.Token)) as JsonObject;
        }
        catch (JsonException)
        {
            body = null;
        }
        return await broker.HandleAsync(bearer, body, _stop.Token);
    }
}
