using Mariliz.Application.Integrations;

namespace Mariliz.Infrastructure.Integrations;

/// <summary>
/// GET por HTTPS para os provedores. Não segue redirecionamento (a credencial só vai ao endereço que o conector montou) e
/// lê no máximo <see cref="MaxBodyBytes"/> de resposta.
/// </summary>
public sealed class HttpsConnectorTransport : IConnectorTransport, IDisposable
{
    public const int MaxBodyBytes = 16 * 1024 * 1024;

    private readonly HttpClient _http;

    public HttpsConnectorTransport(HttpMessageHandler? handler = null)
    {
        _http = handler is null
            ? new HttpClient(new SocketsHttpHandler { AllowAutoRedirect = false, AutomaticDecompression = System.Net.DecompressionMethods.All })
            : new HttpClient(handler, disposeHandler: false);
        _http.Timeout = TimeSpan.FromSeconds(30);
        _http.MaxResponseContentBufferSize = MaxBodyBytes;
    }

    public async Task<ConnectorResponse> GetAsync(ConnectorRequest request, CancellationToken ct)
    {
        if (!string.Equals(request.Url.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            throw new ConnectorTransportException("Só HTTPS é permitido.");

        using var message = new HttpRequestMessage(HttpMethod.Get, request.Url);
        foreach (var (name, value) in request.Headers) message.Headers.TryAddWithoutValidation(name, value);

        try
        {
            using var response = await _http.SendAsync(message, ct);
            var body = await response.Content.ReadAsStringAsync(ct);
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var header in response.Headers.Concat(response.Content.Headers))
                headers[header.Key.ToLowerInvariant()] = string.Join(", ", header.Value);
            return new ConnectorResponse((int)response.StatusCode, headers, body);
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException || (ex is OperationCanceledException && !ct.IsCancellationRequested))
        {
            // A mensagem da exceção pode trazer o endereço com parâmetros: só o tipo segue adiante.
            throw new ConnectorTransportException(ex.GetType().Name);
        }
    }

    public void Dispose() => _http.Dispose();
}
