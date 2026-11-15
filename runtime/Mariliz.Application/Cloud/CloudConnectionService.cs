using System.Text.Json.Nodes;
using Mariliz.Application.Execution;
using Mariliz.Application.Integrations;
using Mariliz.Application.Ports;

namespace Mariliz.Application.Cloud;

// A conexão guarda só nomes (perfil, região, tenant, projeto): a credencial fica onde o SDK oficial a procura. Por isso uma
// conexão com configuração válida é gravada mesmo sem credencial: a pessoa vê "aguardando autenticação" e valida depois.
public sealed class CloudConnectionService(ICloudStore store, CloudProviderRegistry providers, IClock clock, IAuditLog audit)
{
    public const int MaxConnections = 16;
    public const int MaxNameLength = 60;

    private readonly object _gate = new();

    public IReadOnlyList<CloudConnection> List() => store.Connections();

    public CloudConnection Require(string connectionId) =>
        store.Connections().FirstOrDefault(c => c.Id == connectionId) ?? throw CloudErrors.ConnectionNotFound();

    public int ProjectsUsing(string connectionId) => store.Links().Count(l => l.ConnectionId == connectionId);

    public async Task<CloudConnection> CreateAsync(
        string provider, string name, string mechanism, IReadOnlyDictionary<string, string> values, CancellationToken ct)
    {
        var target = providers.Require(provider);
        var cleanName = CleanName(name);
        var settings = providers.Normalize(provider, mechanism, values);
        if (store.Connections().Count >= MaxConnections)
            throw CloudErrors.LimitReached("O limite de conexões cloud foi atingido. Remova uma que não é mais usada.");

        var (status, identity, error) = await IdentifyAsync(target, settings, ct);
        var now = clock.UtcNow;
        var connection = new CloudConnection(NewId("cloud-"), provider, cleanName, settings, status, identity, Unchecked(target, now),
            now, now, error, target.Descriptor.Version);
        lock (_gate) store.SaveConnection(connection);
        Audit("cloud.connection_created", connection);
        return connection;
    }

    // Uma recusa não é exceção: vira estado da conexão, que é o que a pessoa consulta.
    public async Task<CloudConnection> ValidateAsync(string connectionId, CancellationToken ct)
    {
        var connection = Require(connectionId);
        var (status, identity, error) = await IdentifyAsync(providers.Require(connection.Provider), connection.Settings, ct);
        var updated = Update(connectionId, c => c with
        {
            Status = status, Identity = identity ?? c.Identity, CheckedAt = clock.UtcNow, LastError = error
        });
        Audit("cloud.connection_validated", updated);
        return updated;
    }

    public async Task<CloudConnection> CheckPermissionsAsync(string connectionId, CancellationToken ct)
    {
        var connection = Require(connectionId);
        var target = providers.Require(connection.Provider);
        var capabilities = target.Descriptor.Capabilities;
        var results = target is ICloudPermissionTester tester
            ? await TestAsync(tester, connection.Settings, capabilities, ct)
            : await ProbeAsync(target, connection.Settings, capabilities, ct);

        var updated = Update(connectionId, c => c with { Permissions = results, CheckedAt = clock.UtcNow });
        audit.Record("cloud.permissions_checked", new JsonObject
        {
            ["connectionId"] = updated.Id, ["provider"] = updated.Provider, ["name"] = updated.Name,
            ["allowed"] = results.Count(r => r.State == CloudPermissionState.Allowed),
            ["denied"] = results.Count(r => r.State == CloudPermissionState.Denied),
            ["failed"] = results.Count(r => r.State == CloudPermissionState.Error)
        });
        return updated;
    }

    public void Remove(string connectionId)
    {
        CloudConnection connection;
        lock (_gate)
        {
            connection = Require(connectionId);
            if (store.Links().Any(l => l.ConnectionId == connectionId)) throw CloudErrors.ConnectionInUse();
            store.RemoveConnection(connectionId);
        }
        Audit("cloud.connection_removed", connection);
    }

    // Só erro de credencial muda o estado: leitura negada ou serviço desabilitado são da leitura, não da conexão.
    internal void RecordFailure(string connectionId, EngineException ex)
    {
        if (CloudErrors.StatusOf(ex.Code) is not { } status || store.Connections().All(c => c.Id != connectionId)) return;
        Update(connectionId, c => c with { Status = status, CheckedAt = clock.UtcNow, LastError = new CloudError(ex.Code, ex.Message, clock.UtcNow) });
    }

    internal void RecordSuccess(string connectionId)
    {
        if (store.Connections().FirstOrDefault(c => c.Id == connectionId) is not { Status: not CloudConnectionStatus.Connected }) return;
        Update(connectionId, c => c with { Status = CloudConnectionStatus.Connected, CheckedAt = clock.UtcNow, LastError = null });
    }

    private async Task<(CloudConnectionStatus, CloudIdentity?, CloudError?)> IdentifyAsync(ICloudProvider target, CloudSettings settings, CancellationToken ct)
    {
        try
        {
            return (CloudConnectionStatus.Connected, await target.IdentifyAsync(settings, ct), null);
        }
        catch (EngineException ex) when (ex.Code.StartsWith(CloudErrors.Prefix, StringComparison.Ordinal))
        {
            var status = ex.Code == CloudErrors.PermissionDeniedCode ? CloudConnectionStatus.PermissionDenied
                : CloudErrors.StatusOf(ex.Code) ?? CloudConnectionStatus.Unavailable;
            return (status, null, new CloudError(ex.Code, ex.Message, clock.UtcNow));
        }
    }

    private async Task<IReadOnlyList<CloudPermission>> ProbeAsync(
        ICloudProvider target, CloudSettings settings, IReadOnlyList<CloudCapability> capabilities, CancellationToken ct)
    {
        var results = new List<CloudPermission>();
        foreach (var capability in capabilities)
        {
            var (state, code) = await ProbeOneAsync(target, settings, capability, ct);
            results.Add(new CloudPermission(capability.Id, state, code, clock.UtcNow));
        }
        return results;
    }

    private static async Task<(CloudPermissionState, string?)> ProbeOneAsync(
        ICloudProvider target, CloudSettings settings, CloudCapability capability, CancellationToken ct)
    {
        try
        {
            var region = capability.Region == CloudRegionUse.Required ? settings.Region : null;
            await target.ReadAsync(settings, new CloudQuery(capability.Id, region, Limit: 1), ct);
            return (CloudPermissionState.Allowed, null);
        }
        catch (EngineException ex) when (ex.Code == CloudErrors.PermissionDeniedCode)
        {
            return (CloudPermissionState.Denied, ex.Code);
        }
        catch (EngineException ex) when (ex.Code.StartsWith(CloudErrors.Prefix, StringComparison.Ordinal))
        {
            return (CloudPermissionState.Error, ex.Code);
        }
    }

    private async Task<IReadOnlyList<CloudPermission>> TestAsync(
        ICloudPermissionTester tester, CloudSettings settings, IReadOnlyList<CloudCapability> capabilities, CancellationToken ct)
    {
        IReadOnlyDictionary<string, bool> granted;
        try
        {
            granted = await tester.TestPermissionsAsync(settings, capabilities.SelectMany(c => c.Permissions).Distinct(StringComparer.Ordinal).ToList(), ct);
        }
        catch (EngineException ex) when (ex.Code.StartsWith(CloudErrors.Prefix, StringComparison.Ordinal))
        {
            return capabilities.Select(c => new CloudPermission(c.Id, CloudPermissionState.Error, ex.Code, clock.UtcNow)).ToList();
        }
        return capabilities
            .Select(c => c.Permissions.All(p => granted.TryGetValue(p, out var has) && has)
                ? new CloudPermission(c.Id, CloudPermissionState.Allowed, null, clock.UtcNow)
                : new CloudPermission(c.Id, CloudPermissionState.Denied, CloudErrors.PermissionDeniedCode, clock.UtcNow))
            .ToList();
    }

    private static IReadOnlyList<CloudPermission> Unchecked(ICloudProvider target, DateTimeOffset at) =>
        target.Descriptor.Capabilities.Select(c => new CloudPermission(c.Id, CloudPermissionState.Unchecked, null, at)).ToList();

    private CloudConnection Update(string connectionId, Func<CloudConnection, CloudConnection> change)
    {
        lock (_gate)
        {
            var updated = change(Require(connectionId));
            store.SaveConnection(updated);
            return updated;
        }
    }

    private void Audit(string action, CloudConnection connection) =>
        audit.Record(action, new JsonObject
        {
            ["connectionId"] = connection.Id, ["provider"] = connection.Provider, ["name"] = connection.Name,
            ["mechanism"] = connection.Settings.Mechanism, ["status"] = IntegrationJson.Node(connection.Status)
        });

    internal static string NewId(string prefix) => prefix + Guid.NewGuid().ToString("N")[..Mariliz.Core.Text.Ids.HexLength];

    private static string CleanName(string name)
    {
        var trimmed = name.Trim();
        if (trimmed.Length is 0 or > MaxNameLength || trimmed.Any(char.IsControl))
            throw CloudErrors.SettingsInvalid($"Dê um nome de até {MaxNameLength} caracteres à conexão.");
        return trimmed;
    }
}
