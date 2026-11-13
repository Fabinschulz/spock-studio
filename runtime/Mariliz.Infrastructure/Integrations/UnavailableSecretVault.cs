using Mariliz.Application.Integrations;

namespace Mariliz.Infrastructure.Integrations;

// Em vez de gravar a credencial em texto puro, a central recusa criar conexões e diz por quê.
public sealed class UnavailableSecretVault : ISecretVault
{
    public bool Available => false;

    public string Name => "nenhum";

    public string? UnavailableHint => "Este sistema operacional não tem cofre implementado: o Spock Studio guarda credenciais no Windows, no macOS e no Linux.";

    public void Store(string connectionId, string secret) => throw IntegrationErrors.VaultUnavailable();

    public bool TryRead(string connectionId, out string secret)
    {
        secret = "";
        return false;
    }

    public void Remove(string connectionId)
    {
    }
}
