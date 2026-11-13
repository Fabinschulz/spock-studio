using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using Mariliz.Application.Integrations;

namespace Mariliz.Infrastructure.Integrations;

// O arquivo só abre na mesma conta do Windows: copiado para outra máquina ou lido por outro usuário, não revela a credencial.
[SupportedOSPlatform("windows")]
public sealed class DpapiSecretVault(string dataRoot) : ISecretVault
{
    // Amarra o dado cifrado a este uso: outro programa do mesmo usuário que chame a DPAPI sem esta entropia não o abre por engano.
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("mariliz.integrations.v1");

    public bool Available => true;

    public string Name => "Proteção do usuário do Windows (DPAPI)";

    public void Store(string connectionId, string secret) =>
        AtomicFile.Write(
            IntegrationPaths.Secret(dataRoot, connectionId),
            ProtectedData.Protect(Encoding.UTF8.GetBytes(secret), Entropy, DataProtectionScope.CurrentUser));

    public bool TryRead(string connectionId, out string secret)
    {
        secret = "";
        var path = IntegrationPaths.Secret(dataRoot, connectionId);
        if (!File.Exists(path)) return false;
        try
        {
            secret = Encoding.UTF8.GetString(ProtectedData.Unprotect(File.ReadAllBytes(path), Entropy, DataProtectionScope.CurrentUser));
            return secret.Length > 0;
        }
        catch (Exception ex) when (ex is CryptographicException or IOException)
        {
            return false;
        }
    }

    public void Remove(string connectionId)
    {
        var path = IntegrationPaths.Secret(dataRoot, connectionId);
        if (File.Exists(path)) File.Delete(path);
        if (File.Exists(path + ".tmp")) File.Delete(path + ".tmp");
    }
}
