using System.Text.Json.Nodes;
using Mariliz.Application.Execution;
using Mariliz.Application.Ports;

namespace Mariliz.Application.Integrations;

/// <summary>
/// Ciclo de vida das conexões: conectar, testar, trocar a credencial, desconectar e remover. Uma conexão só é gravada depois
/// que o provedor aceita a credencial, e a credencial só existe no cofre.
/// </summary>
public sealed class ConnectionService(IIntegrationStore store, ISecretVault vault, ConnectorRegistry connectors, IClock clock, IAuditLog audit)
{
    public const int MaxConnections = 32;
    public const int MaxNameLength = 60;

    private readonly object _gate = new();

    public IReadOnlyList<IntegrationConnection> List() => store.Connections();

    /// <summary>Em quantos vínculos de projeto a conexão é usada. Enquanto for maior que zero, ela não pode ser removida.</summary>
    public int ProjectsUsing(string connectionId) => store.Integrations().Count(i => i.ConnectionId == connectionId);

    public IntegrationConnection Require(string connectionId) =>
        store.Connections().FirstOrDefault(c => c.Id == connectionId) ?? throw IntegrationErrors.ConnectionNotFound();

    public async Task<IntegrationConnection> CreateAsync(
        string provider, string name, IReadOnlyDictionary<string, string> settings, string secret, CancellationToken ct)
    {
        if (!vault.Available) throw IntegrationErrors.VaultUnavailable();
        var connector = connectors.Require(provider);
        var cleanName = CleanName(name);
        var normalized = connector.NormalizeSettings(settings);
        var cleanSecret = CleanSecret(secret);
        if (store.Connections().Count >= MaxConnections)
            throw IntegrationErrors.LimitReached("O limite de conexões foi atingido. Remova uma conexão que não é mais usada.");

        var probe = await connector.ProbeAsync(new ConnectorAccess(normalized, cleanSecret), ct);
        var now = clock.UtcNow;
        var connection = new IntegrationConnection(
            "conn-" + Guid.NewGuid().ToString("N")[..12], provider, cleanName, probe.Account, ConnectionStatus.Connected, probe.Scopes,
            normalized, now, now, LastSyncAt: null, LastError: null, connector.Descriptor.Version);

        lock (_gate)
        {
            vault.Store(connection.Id, cleanSecret);
            store.SaveConnection(connection);
        }
        Audit("connection.created", connection);
        return connection;
    }

    /// <summary>Confere a credencial com o provedor. Uma recusa não é exceção: fica registrada na conexão, que é o que a pessoa consulta.</summary>
    public async Task<IntegrationConnection> TestAsync(string connectionId, CancellationToken ct)
    {
        var connection = Require(connectionId);
        var access = AccessFor(connection);
        try
        {
            var probe = await connectors.Require(connection.Provider).ProbeAsync(access, ct);
            return Update(connectionId, c => c with
            {
                Status = ConnectionStatus.Connected, Account = probe.Account, Scopes = probe.Scopes, CheckedAt = clock.UtcNow, LastError = null
            });
        }
        catch (EngineException ex) when (ex.Code.StartsWith(IntegrationErrors.Prefix, StringComparison.Ordinal))
        {
            return Update(connectionId, c => c with
            {
                Status = ConnectionStatus.Error, CheckedAt = clock.UtcNow, LastError = new IntegrationError(ex.Code, ex.Message, clock.UtcNow)
            });
        }
    }

    /// <summary>Troca a credencial. A nova é conferida antes: se o provedor a recusar, a antiga continua valendo.</summary>
    public async Task<IntegrationConnection> RotateAsync(string connectionId, string secret, CancellationToken ct)
    {
        if (!vault.Available) throw IntegrationErrors.VaultUnavailable();
        var connection = Require(connectionId);
        var cleanSecret = CleanSecret(secret);
        var probe = await connectors.Require(connection.Provider).ProbeAsync(new ConnectorAccess(connection.Settings, cleanSecret), ct);

        IntegrationConnection updated;
        lock (_gate)
        {
            vault.Store(connectionId, cleanSecret);
            updated = Update(connectionId, c => c with
            {
                Status = ConnectionStatus.Connected, Account = probe.Account, Scopes = probe.Scopes, CheckedAt = clock.UtcNow, LastError = null
            });
        }
        Audit("connection.credential_updated", updated);
        return updated;
    }

    /// <summary>Apaga a credencial e mantém o registro. O que já foi sincronizado continua disponível, com a data da leitura.</summary>
    public IntegrationConnection Disconnect(string connectionId)
    {
        IntegrationConnection updated;
        lock (_gate)
        {
            Require(connectionId);
            vault.Remove(connectionId);
            updated = Update(connectionId, c => c with { Status = ConnectionStatus.Disconnected, Scopes = [], LastError = null });
        }
        Audit("connection.disconnected", updated);
        return updated;
    }

    public void Remove(string connectionId)
    {
        IntegrationConnection connection;
        lock (_gate)
        {
            connection = Require(connectionId);
            if (store.Integrations().Any(i => i.ConnectionId == connectionId)) throw IntegrationErrors.ConnectionInUse();
            vault.Remove(connectionId);
            store.RemoveConnection(connectionId);
        }
        Audit("connection.removed", connection);
    }

    /// <summary>A credencial sai do cofre só aqui, para quem vai chamar o provedor.</summary>
    internal ConnectorAccess AccessFor(IntegrationConnection connection)
    {
        if (connection.Status == ConnectionStatus.Disconnected || !vault.TryRead(connection.Id, out var secret))
            throw IntegrationErrors.ConnectionDisconnected();
        return new ConnectorAccess(connection.Settings, secret);
    }

    /// <summary>Resultado de uma sincronização na conexão que a fez. Credencial recusada muda o status; outras falhas só ficam anotadas.</summary>
    internal void RecordSync(string connectionId, DateTimeOffset at, IntegrationError? error)
    {
        if (store.Connections().All(c => c.Id != connectionId)) return;
        Update(connectionId, c => error is null
            ? c with { LastSyncAt = at, LastError = null, Status = c.Status == ConnectionStatus.Error ? ConnectionStatus.Connected : c.Status }
            : c with { LastError = error, Status = error.Code == IntegrationErrors.AuthFailedCode ? ConnectionStatus.Error : c.Status });
    }

    private IntegrationConnection Update(string connectionId, Func<IntegrationConnection, IntegrationConnection> change)
    {
        lock (_gate)
        {
            var updated = change(Require(connectionId));
            store.SaveConnection(updated);
            return updated;
        }
    }

    private void Audit(string action, IntegrationConnection connection) =>
        audit.Record(action, new JsonObject { ["connectionId"] = connection.Id, ["provider"] = connection.Provider, ["name"] = connection.Name });

    private static string CleanName(string name)
    {
        var trimmed = name.Trim();
        if (trimmed.Length is 0 or > MaxNameLength || trimmed.Any(char.IsControl))
            throw IntegrationErrors.SettingsInvalid($"Dê um nome de até {MaxNameLength} caracteres à conexão.");
        return trimmed;
    }

    private static string CleanSecret(string secret)
    {
        var trimmed = secret.Trim();
        if (trimmed.Length is < 8 or > 512 || trimmed.Any(c => char.IsWhiteSpace(c) || char.IsControl(c)))
            throw IntegrationErrors.SecretRejected();
        return trimmed;
    }
}
