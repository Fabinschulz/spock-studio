using System.Text.Json.Nodes;
using Mariliz.Application.Cloud;
using Mariliz.Application.Integrations;

namespace Mariliz.Application.Protocol;

// A interface é a pessoa: lê pela mesma porta que o MCP, sem o atalho dos agentes.
internal sealed class CloudRequestHandler(CloudHub hub)
{
    public async Task<JsonObject> HandleAsync(string method, RequestParams p, CancellationToken ct) => method switch
    {
        ProtocolMethods.CloudCatalog => new JsonObject { ["providers"] = Array(hub.Providers.Catalog().Select(d => IntegrationJson.Node(d))) },
        ProtocolMethods.CloudList => new JsonObject { ["connections"] = Array(hub.Connections.List().Select(ConnectionJson)) },
        ProtocolMethods.CloudCreate => Connection(await hub.Connections.CreateAsync(
            p.Str("provider"), p.Str("name"), p.Str("mechanism"), p.StringMap("settings"), ct)),
        ProtocolMethods.CloudValidate => Connection(await hub.Connections.ValidateAsync(p.Str("connectionId"), ct)),
        ProtocolMethods.CloudPermissions => Connection(await hub.Connections.CheckPermissionsAsync(p.Str("connectionId"), ct)),
        ProtocolMethods.CloudRemove => Removed(() => hub.Connections.Remove(p.Str("connectionId"))),
        ProtocolMethods.CloudLinks => new JsonObject { ["links"] = Array(hub.Projects.List(p.Str("projectId")).Select(l => IntegrationJson.Node(l))) },
        ProtocolMethods.CloudLink => Link(hub.Projects.Link(p.Str("projectId"), p.Str("connectionId"), p.StringList("capabilities"), p.Flag("agentAccess"))),
        ProtocolMethods.CloudConfigure => Link(hub.Projects.Configure(
            p.Str("projectId"), p.Str("linkId"), p.Has("capabilities") ? p.StringList("capabilities") : null, p.OptionalFlag("agentAccess"))),
        ProtocolMethods.CloudUnlink => Removed(() => hub.Projects.Unlink(p.Str("projectId"), p.Str("linkId"))),
        ProtocolMethods.CloudRead => new JsonObject
        {
            ["result"] = IntegrationJson.Node(await hub.Reads.ReadAsync(
                new CloudReadRequest(p.Str("connectionId"), p.Str("capability"), p.OptionalStr("region"), p.OptionalInt("limit"), p.OptionalStr("projectId")),
                CloudCaller.Person, ct))
        },
        _ => throw new InvalidOperationException($"Método sem tratador: {method}.")
    };

    private JsonObject Connection(CloudConnection connection) => new() { ["connection"] = ConnectionJson(connection) };

    private JsonNode? ConnectionJson(CloudConnection connection)
    {
        var json = IntegrationJson.Node(connection)!.AsObject();
        json["projects"] = hub.Connections.ProjectsUsing(connection.Id);
        return json;
    }

    private static JsonObject Link(CloudProjectLink link) => new() { ["link"] = IntegrationJson.Node(link) };

    private static JsonObject Removed(Action remove)
    {
        remove();
        return new JsonObject { ["removed"] = true };
    }

    private static JsonArray Array(IEnumerable<JsonNode?> nodes) => new(nodes.ToArray());
}
