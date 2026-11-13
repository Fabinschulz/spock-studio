using System.Text.Json.Nodes;
using Mariliz.Application.Integrations;

namespace Mariliz.Application.Protocol;

// A credencial entra em connection.create e connection.rotate e nunca volta: nenhuma resposta daqui a contém.
internal sealed class IntegrationRequestHandler(IntegrationHub hub)
{
    public async Task<JsonObject> HandleAsync(string method, RequestParams p, CancellationToken ct) => method switch
    {
        ProtocolMethods.IntegrationCatalog => Catalog(),
        ProtocolMethods.ConnectionList => new JsonObject { ["connections"] = Array(hub.Connections.List().Select(ConnectionJson)) },
        ProtocolMethods.ConnectionCreate => Connection(await hub.Connections.CreateAsync(
            p.Str("provider"), p.Str("name"), p.StringMap("settings"), p.Str("secret"), ct)),
        ProtocolMethods.ConnectionTest => Connection(await hub.Connections.TestAsync(p.Str("connectionId"), ct)),
        ProtocolMethods.ConnectionRotate => Connection(await hub.Connections.RotateAsync(p.Str("connectionId"), p.Str("secret"), ct)),
        ProtocolMethods.ConnectionDisconnect => Connection(hub.Connections.Disconnect(p.Str("connectionId"))),
        ProtocolMethods.ConnectionRemove => Removed(() => hub.Connections.Remove(p.Str("connectionId"))),
        ProtocolMethods.ConnectionReach => new JsonObject { ["connections"] = Array(hub.Reach.List().Select(reach => IntegrationJson.Node(reach))) },
        ProtocolMethods.IntegrationList => new JsonObject
        {
            ["integrations"] = Array(hub.Projects.List(p.Str("projectId")).Select(IntegrationJsonOf))
        },
        ProtocolMethods.IntegrationLink => Integration(await hub.Projects.LinkAsync(
            p.Str("projectId"), p.Str("connectionId"), p.Str("resource"), p.StringList("dataKinds"), ct)),
        ProtocolMethods.IntegrationConfigure => Integration(hub.Projects.Configure(
            p.Str("projectId"), p.Str("integrationId"), OptionalKinds(p), p.OptionalFlag("feedsKnowledge"))),
        ProtocolMethods.IntegrationUnlink => Removed(() => hub.Projects.Unlink(p.Str("projectId"), p.Str("integrationId"))),
        ProtocolMethods.IntegrationSync => new JsonObject
        {
            ["sync"] = IntegrationJson.Node(hub.Sync.Start(p.Str("projectId"), p.Str("integrationId")))
        },
        ProtocolMethods.IntegrationHistory => new JsonObject
        {
            ["syncs"] = Array(hub.Sync.History(p.Str("projectId"), p.Str("integrationId")).Select(e => IntegrationJson.Node(e)))
        },
        _ => throw new InvalidOperationException($"Método sem tratador: {method}.")
    };

    public Task ShutdownAsync() => hub.Sync.ShutdownAsync();

    private JsonObject Catalog() => new()
    {
        ["vaultAvailable"] = hub.VaultAvailable,
        ["vaultName"] = hub.VaultName,
        ["vaultHint"] = hub.VaultHint,
        ["connectors"] = Array(hub.Connectors.Catalog().Select(d => IntegrationJson.Node(d)))
    };

    private JsonObject Connection(IntegrationConnection connection) => new() { ["connection"] = ConnectionJson(connection) };

    private JsonNode? ConnectionJson(IntegrationConnection connection)
    {
        var json = IntegrationJson.Node(connection)!.AsObject();
        json["projects"] = hub.Connections.ProjectsUsing(connection.Id);
        return json;
    }

    private JsonObject Integration(ProjectIntegration integration) => new() { ["integration"] = IntegrationJsonOf(integration) };

    private JsonNode? IntegrationJsonOf(ProjectIntegration integration)
    {
        var connection = hub.Connections.List().FirstOrDefault(c => c.Id == integration.ConnectionId);
        var synced = hub.Projects.Data(integration);
        var items = synced.Items.Values;

        var json = IntegrationJson.Node(integration)!.AsObject();
        json["connectionName"] = connection?.Name ?? "";
        json["connectionStatus"] = IntegrationJson.Node(connection?.Status ?? ConnectionStatus.Disconnected);
        json["checkpoint"] = IntegrationJson.Node(synced.Checkpoint);
        json["syncing"] = hub.Activity.IsRunning(integration.Id);
        json["lastSync"] = IntegrationJson.Node(synced.History.FirstOrDefault());
        json["items"] = new JsonObject
        {
            ["total"] = items.Count,
            ["byKind"] = Counts(items.GroupBy(i => i.Item.Kind)),
            ["byStatusCategory"] = Counts(items.GroupBy(i => i.Item.StatusCategory)),
            ["lastSyncedAt"] = IntegrationJson.Node(items.Count == 0 ? (DateTimeOffset?)null : items.Max(i => i.SyncedAt))
        };
        return json;
    }

    private static JsonObject Counts(IEnumerable<IGrouping<string, StoredWorkItem>> groups)
    {
        var counts = new JsonObject();
        foreach (var group in groups.OrderBy(g => g.Key, StringComparer.Ordinal)) counts[group.Key] = group.Count();
        return counts;
    }

    private static IReadOnlyList<string>? OptionalKinds(RequestParams p) => p.Has("dataKinds") ? p.StringList("dataKinds") : null;

    private static JsonObject Removed(Action remove)
    {
        remove();
        return new JsonObject { ["removed"] = true };
    }

    private static JsonArray Array(IEnumerable<JsonNode?> nodes) => new(nodes.ToArray());
}
