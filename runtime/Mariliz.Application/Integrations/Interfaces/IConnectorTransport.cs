namespace Mariliz.Application.Integrations;

/// <summary>
/// Leitura HTTPS de um provedor. Só GET: os conectores não escrevem nos sistemas externos.
/// Falha de rede ou de prazo vira <see cref="ConnectorTransportException"/>; qualquer resposta HTTP volta como está.
/// </summary>
public interface IConnectorTransport
{
    Task<ConnectorResponse> GetAsync(ConnectorRequest request, CancellationToken ct);
}
