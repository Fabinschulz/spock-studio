namespace Mariliz.Application.Integrations;

// Só GET: os conectores não escrevem nos sistemas externos. Falha de rede ou de prazo vira ConnectorTransportException; qualquer resposta HTTP volta como está.
public interface IConnectorTransport
{
    Task<ConnectorResponse> GetAsync(ConnectorRequest request, CancellationToken ct);
}
