using Mariliz.Application.Integrations;

namespace Mariliz.Infrastructure.Integrations;

/// <summary>
/// Sistemas sem cofre implementado (hoje Linux e macOS). Em vez de gravar a credencial em texto puro, a central recusa criar
/// conexões e diz por quê.
/// </summary>
public sealed class UnavailableSecretVault : ISecretVault
{
    public bool Available => false;

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
