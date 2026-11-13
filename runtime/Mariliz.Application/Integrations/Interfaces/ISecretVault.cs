namespace Mariliz.Application.Integrations;

// A credencial só sai para montar a chamada ao provedor: não vai para o registro da conexão, o protocolo, o diário nem a auditoria.
public interface ISecretVault
{
    // Sem armazenamento protegido nenhuma conexão é criada.
    bool Available { get; }

    string Name => "cofre do sistema";

    string? UnavailableHint => null;

    void Store(string connectionId, string secret);

    bool TryRead(string connectionId, out string secret);

    void Remove(string connectionId);
}
