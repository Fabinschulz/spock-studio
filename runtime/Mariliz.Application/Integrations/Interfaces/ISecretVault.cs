namespace Mariliz.Application.Integrations;

/// <summary>
/// Cofre das credenciais das conexões, protegido pelo sistema operacional. A credencial entra por aqui e só sai para montar
/// a chamada ao provedor: não vai para o registro da conexão, o protocolo, o diário nem a auditoria.
/// </summary>
public interface ISecretVault
{
    /// <summary>Falso quando o sistema não oferece armazenamento protegido. Nesse caso nenhuma conexão é criada.</summary>
    bool Available { get; }

    /// <summary>Como o cofre se chama para a pessoa (o do Windows, o Chaveiro do macOS, o serviço de segredos do Linux).</summary>
    string Name => "cofre do sistema";

    /// <summary>Quando não está disponível, o que falta e o que fazer, em palavras simples. Nulo se não souber ou se está disponível.</summary>
    string? UnavailableHint => null;

    void Store(string connectionId, string secret);

    bool TryRead(string connectionId, out string secret);

    void Remove(string connectionId);
}
