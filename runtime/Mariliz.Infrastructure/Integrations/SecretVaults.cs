using Mariliz.Application.Integrations;
using Mariliz.Application.Ports;

namespace Mariliz.Infrastructure.Integrations;

/// <summary>Os sistemas em que o Spock Studio sabe guardar credenciais.</summary>
public enum VaultPlatform
{
    Windows,
    MacOs,
    Linux,
    Other
}

/// <summary>Escolhe o cofre do sistema. É uma função e não um <c>if</c> espalhado para o teste conferir cada sistema sem rodar nele.</summary>
public static class SecretVaults
{
    public static VaultPlatform CurrentPlatform() =>
        OperatingSystem.IsWindows() ? VaultPlatform.Windows
        : OperatingSystem.IsMacOS() ? VaultPlatform.MacOs
        : OperatingSystem.IsLinux() ? VaultPlatform.Linux
        : VaultPlatform.Other;

    public static ISecretVault For(VaultPlatform platform, string dataRoot, IProcessRunner runner) => platform switch
    {
        VaultPlatform.Windows when OperatingSystem.IsWindows() => new DpapiSecretVault(dataRoot),
        VaultPlatform.MacOs => new KeychainSecretVault(runner),
        VaultPlatform.Linux => new SecretServiceVault(runner),
        _ => new UnavailableSecretVault()
    };
}
