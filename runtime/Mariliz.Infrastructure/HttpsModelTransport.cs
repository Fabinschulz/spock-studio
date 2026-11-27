using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Mariliz.Application.Execution;
using Mariliz.Application.Models;
using Mariliz.Infrastructure.ModelFormats;

namespace Mariliz.Infrastructure;

// A chave vai no cabeçalho e não entra na mensagem de erro. O formato é escolhido pelo provedor do endereço, nunca pelo chamador.
public sealed class HttpsModelTransport : IModelTransport, IModelChatTransport, IDisposable
{
    private readonly HttpClient _http;

    public HttpsModelTransport(HttpMessageHandler? handler = null)
    {
        _http = handler is null ? new HttpClient() : new HttpClient(handler, disposeHandler: false);
        // O prazo é de quem chama (o ModelGateway põe um em cada operação: prova 10 s, conversa 30 s). Um prazo próprio aqui, menor que
        // o da conversa, a cortaria antes da hora.
        _http.Timeout = Timeout.InfiniteTimeSpan;
    }

    internal TimeSpan RequestTimeout => _http.Timeout;

    public async Task<ModelTransportResult> SendAsync(Uri endpoint, string key, string prompt, int maxTokens, CancellationToken ct)
    {
        var provider = Allowed(endpoint);
        var reply = await ExchangeAsync(provider, key, provider.DefaultModel, [new ChatMessage("user", prompt)], maxTokens, jsonObject: false, ct);
        return new ModelTransportResult(reply.Text, reply.TotalTokens);
    }

    public async Task<ChatTransportResult> SendChatAsync(Uri endpoint, string key, string model, ChatRequest chat, CancellationToken ct)
    {
        var provider = Allowed(endpoint);
        var reply = await ExchangeAsync(provider, key, model, chat.Messages, chat.MaxTokens, chat.JsonObject, ct);
        return new ChatTransportResult(reply.Text, reply.Version.Length > 0 ? reply.Version : model, reply.InputTokens, reply.OutputTokens);
    }

    private async Task<ModelReply> ExchangeAsync(
        ModelProvider provider, string key, string model, IReadOnlyList<ChatMessage> messages, int maxTokens, bool jsonObject, CancellationToken ct)
    {
        var format = ModelFormatRegistry.For(provider);
        using var request = new HttpRequestMessage(HttpMethod.Post, format.Url(provider, model));
        format.Authenticate(request, key);
        request.Content = new StringContent(format.Body(model, messages, maxTokens, jsonObject).ToJsonString(), Encoding.UTF8, "application/json");

        try
        {
            using var response = await _http.SendAsync(request, ct);
            var text = await response.Content.ReadAsStringAsync(ct);
            ThrowIfRefused(format, response.StatusCode, text);
            // Um 5xx é do lado do provedor (ou do gateway na frente dele), não uma resposta fora do contrato; o corpo não é repetido.
            if ((int)response.StatusCode >= 500)
                throw new EngineException("MODEL_OVERLOADED", $"O provedor está indisponível ou sobrecarregado agora (HTTP {(int)response.StatusCode}).");
            var reply = ReadReply(format, response.IsSuccessStatusCode, text);
            return jsonObject ? reply with { Text = JsonReply.Extract(reply.Text) } : reply;
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or FormatException)
        {
            // Só a falha de conexão chega aqui; dizer a causa (DNS, TLS, proxy) é o que deixa a pessoa agir. Nunca a mensagem da exceção,
            // que pode trazer o endereço ou o cabeçalho.
            throw new EngineException("MODEL_UNAVAILABLE", $"O modelo não pôde ser contactado ({Cause(ex)}).");
        }
    }

    // Uma resposta que chegou mas não é um JSON do formato esperado é do provedor (ou de um gateway na frente dele), não da conexão.
    private static ModelReply ReadReply(IModelFormat format, bool success, string text)
    {
        try
        {
            if (!success || JsonNode.Parse(text) is not JsonObject document)
                throw new EngineException("MODEL_INVALID_RESPONSE", "O modelo respondeu fora do contrato.");
            return format.Read(document);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException)
        {
            throw new EngineException("MODEL_INVALID_RESPONSE", $"O modelo respondeu fora do contrato ({ex.GetType().Name}).");
        }
    }

    private static string Cause(Exception ex) =>
        ex is HttpRequestException { HttpRequestError: not HttpRequestError.Unknown } http ? http.HttpRequestError.ToString() : ex.GetType().Name;

    // O corpo da resposta não é repetido: ele pode citar a chave.
    private static void ThrowIfRefused(IModelFormat format, HttpStatusCode status, string body)
    {
        if (format.RefusesKey(status, body))
            throw new EngineException("MODEL_AUTH_FAILED", "O provedor recusou a chave.");
        if (status == HttpStatusCode.TooManyRequests)
            throw new EngineException("MODEL_RATE_LIMITED", "O provedor limitou o uso desta chave agora. Tente de novo em instantes.");
    }

    // Qualquer endereço fora da lista é recusado antes de a chave sair.
    private static ModelProvider Allowed(Uri endpoint) =>
        ModelProviders.ByEndpoint(endpoint) ?? throw new EngineException("MODEL_ENDPOINT_DENIED", "O destino do modelo não é permitido.");

    public void Dispose() => _http.Dispose();
}
