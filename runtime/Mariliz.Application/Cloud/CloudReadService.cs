using System.Text.Json.Nodes;
using Mariliz.Application.Execution;
using Mariliz.Application.Integrations;
using Mariliz.Application.Ports;

namespace Mariliz.Application.Cloud;

// A única porta de leitura, para a interface e para o MCP: decide se a leitura pode acontecer e a registra. Um agente só lê
// capacidade que o projeto liberou para agentes; nenhuma instrução dele muda isso, porque nada aqui vem do pedido além de ids.
public sealed class CloudReadService(
    CloudConnectionService connections, CloudProjectService projects, CloudProviderRegistry providers, IClock clock, IAuditLog audit)
{
    public async Task<CloudReadResult> ReadAsync(CloudReadRequest request, CloudCaller caller, CancellationToken ct)
    {
        var connection = connections.Require(request.ConnectionId);
        var target = providers.Require(connection.Provider);
        var capability = providers.RequireCapability(connection.Provider, request.Capability);
        Authorize(request, caller, connection);

        var limit = CloudProviderRegistry.Limit(request.Limit);
        var region = capability.Region switch
        {
            CloudRegionUse.Required => Region(target, request.Region ?? connection.Settings.Region),
            CloudRegionUse.Filter when request.Region is not null => target.NormalizeRegion(request.Region),
            _ => null
        };
        CloudTable table;
        try
        {
            table = await target.ReadAsync(connection.Settings, new CloudQuery(capability.Id, region, limit), ct);
            if (table.Rows.Any(row => row.Count != capability.Columns.Count)) throw CloudErrors.InvalidResponse();
        }
        catch (EngineException ex) when (ex.Code.StartsWith(CloudErrors.Prefix, StringComparison.Ordinal))
        {
            connections.RecordFailure(connection.Id, ex);
            Audit(request, caller, connection, region, rows: 0, truncated: false, outcome: ex.Code);
            throw;
        }

        connections.RecordSuccess(connection.Id);
        var rows = table.Rows.Take(limit).ToList();
        var truncated = table.Truncated || table.Rows.Count > limit;
        Audit(request, caller, connection, region, rows.Count, truncated, outcome: "ok");
        return new CloudReadResult(connection.Id, connection.Name, connection.Provider, connection.Identity?.Account, capability.Id, region,
            capability.Columns, rows, truncated, clock.UtcNow);
    }

    // A pessoa pode explorar a própria conexão sem projeto; agente nunca lê sem projeto, vínculo, capacidade e liberação.
    private void Authorize(CloudReadRequest request, CloudCaller caller, CloudConnection connection)
    {
        if (request.ProjectId is null)
        {
            if (caller.Kind == CloudCallerKind.Agent) throw CloudErrors.NotLinked();
            return;
        }
        projects.RequireProject(request.ProjectId);
        var link = projects.Find(request.ProjectId, connection.Id) ?? throw CloudErrors.NotLinked();
        if (!link.Capabilities.Contains(request.Capability, StringComparer.Ordinal)) throw CloudErrors.CapabilityNotEnabled();
        if (caller.Kind == CloudCallerKind.Agent && !link.AgentAccess) throw CloudErrors.AgentNotAllowed();
    }

    private static string Region(ICloudProvider target, string? region) =>
        region is null ? throw CloudErrors.RegionInvalid("Esta leitura é por região: informe a região ou defina a região padrão da conexão.")
            : target.NormalizeRegion(region);

    private void Audit(CloudReadRequest request, CloudCaller caller, CloudConnection connection, string? region, int rows, bool truncated, string outcome) =>
        audit.Record("cloud.read", new JsonObject
        {
            ["projectId"] = request.ProjectId, ["connectionId"] = connection.Id, ["provider"] = connection.Provider, ["name"] = connection.Name,
            ["capability"] = request.Capability, ["region"] = region, ["rows"] = rows, ["truncated"] = truncated, ["outcome"] = outcome,
            ["caller"] = IntegrationJson.Node(caller.Kind), ["client"] = caller.Client
        });
}
