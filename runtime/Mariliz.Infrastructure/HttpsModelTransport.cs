using System.Net.Http.Headers;
using System.Text.Json;
using System.Text;
using System.Text.Json.Nodes;
using Mariliz.Application.Execution;
using Mariliz.Application.Models;

namespace Mariliz.Infrastructure;

/// <summary>Chama só o endpoint allowlist. A chave vai no cabeçalho e não entra na mensagem de erro.</summary>
public sealed class HttpsModelTransport : IModelTransport, IDisposable
{
    private readonly HttpClient _http;

    public HttpsModelTransport(HttpMessageHandler? handler = null)
    {
        _http = handler is null ? new HttpClient() : new HttpClient(handler, disposeHandler: false);
        _http.Timeout = TimeSpan.FromSeconds(15);
    }

    public async Task<ModelTransportResult> SendAsync(Uri endpoint, string key, string prompt, int maxTokens, CancellationToken ct)
    {
        if (!string.Equals(endpoint.Scheme, "https", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(endpoint.Host, ModelGateway.Endpoint.Host, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(endpoint.AbsolutePath, ModelGateway.Endpoint.AbsolutePath, StringComparison.Ordinal))
            throw new EngineException("MODEL_ENDPOINT_DENIED", "O destino do modelo não é permitido.");

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        request.Content = new StringContent(new JsonObject
        {
            ["model"] = "gpt-4o-mini",
            ["max_tokens"] = maxTokens,
            ["messages"] = new JsonArray(new JsonObject { ["role"] = "user", ["content"] = prompt })
        }.ToJsonString(), Encoding.UTF8, "application/json");

        try
        {
            using var response = await _http.SendAsync(request, ct);
            var body = await response.Content.ReadAsStringAsync(ct);
            if (!response.IsSuccessStatusCode || JsonNode.Parse(body) is not JsonObject document)
                throw new EngineException("MODEL_INVALID_RESPONSE", "O modelo respondeu fora do contrato.");

            var text = document["choices"]?[0]?["message"]?["content"]?.GetValue<string>() ?? "";
            var tokens = document["usage"]?["total_tokens"]?.GetValue<int>() ?? 0;
            return new ModelTransportResult(text, tokens);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or InvalidOperationException or FormatException)
        {
            throw new EngineException("MODEL_UNAVAILABLE", "O modelo não pôde ser contactado.");
        }
    }

    public void Dispose() => _http.Dispose();
}
