namespace Mariliz.Application.Integrations;

public enum ConnectionStatus
{
    /// <summary>A última verificação com o provedor deu certo.</summary>
    Connected,

    /// <summary>A credencial existe, mas a última verificação ou sincronização falhou.</summary>
    Error,

    /// <summary>A credencial foi apagada do cofre. Os dados já sincronizados continuam, sem novas chamadas.</summary>
    Disconnected
}
