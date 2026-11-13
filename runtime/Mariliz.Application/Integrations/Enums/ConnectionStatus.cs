namespace Mariliz.Application.Integrations;

public enum ConnectionStatus
{
    Connected,

    Error,

    // Os dados já sincronizados continuam, sem novas chamadas.
    Disconnected
}
