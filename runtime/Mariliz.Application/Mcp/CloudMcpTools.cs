using System.Text;
using System.Text.Json.Nodes;
using Mariliz.Application.Cloud;
using Mariliz.Application.Workspace;

namespace Mariliz.Application.Mcp;

// O adaptador MCP da cloud: só aparecem as leituras que algum projeto do escopo liberou para agentes, e cada chamada passa
// de novo pela mesma porta da interface (CloudReadService), que confere o vínculo na hora: tirar a liberação vale sem
// reiniciar a IDE, só a lista de ferramentas espera o próximo início.
public sealed class CloudMcpTools(CloudHub cloud, Func<WorkspaceAuthorizer> workspaces, string workingDirectory, IReadOnlySet<string>? scope = null)
{
    public const string ConnectionsTool = "cloud_connections";
    public const int DefaultLimit = 25;
    public const int MaxLimit = 100;
    private const int MaxArgument = 200;

    public static string ToolName(string capability) => capability.Replace('.', '_');

    public IReadOnlyList<McpTool> Tools()
    {
        var projects = new McpProjectResolver(workspaces(), workingDirectory, scope).Projects().Select(p => p.ProjectId).ToHashSet(StringComparer.Ordinal);
        var released = cloud.Projects.AgentLinks().Where(link => projects.Contains(link.ProjectId)).ToList();
        if (released.Count == 0) return [];

        var capabilities = released
            .SelectMany(link => link.Capabilities.Select(id => cloud.Providers.RequireCapability(link.Provider, id)))
            .DistinctBy(capability => capability.Id)
            .OrderBy(capability => capability.Id, StringComparer.Ordinal);
        return [new McpTool(Connections, call => Task.FromResult(RenderConnections(call.Arguments))), .. capabilities.Select(Tool)];
    }

    private static readonly McpToolInfo Connections = new(
        ConnectionsTool, "Conexões cloud do projeto",
        "Conexões de AWS, Azure e Google Cloud que o projeto liberou para agentes: conta, região padrão, estado e as leituras permitidas. " +
        "Só leitura. Use antes das ferramentas aws_*, azure_* e gcp_*.",
        new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject { ["project"] = ProjectArgument() },
            ["additionalProperties"] = false
        },
        OpenWorld: false);

    private McpTool Tool(CloudCapability capability)
    {
        var properties = new JsonObject
        {
            ["project"] = ProjectArgument(),
            ["connection"] = new JsonObject
            {
                ["type"] = "string", ["maxLength"] = MaxArgument,
                ["description"] = "Nome ou id da conexão, como em cloud_connections. Obrigatório só quando o projeto tem mais de uma do provedor."
            },
            ["limit"] = new JsonObject
            {
                ["type"] = "integer", ["minimum"] = 1, ["maximum"] = MaxLimit, ["description"] = $"Quantos itens trazer (padrão {DefaultLimit})."
            }
        };
        if (capability.Region != CloudRegionUse.None)
            properties["region"] = new JsonObject
            {
                ["type"] = "string", ["maxLength"] = 40,
                ["description"] = capability.Region == CloudRegionUse.Required
                    ? "Região a ler. Sem ela, vale a região padrão da conexão."
                    : "Filtro opcional de região. Sem ele, vêm todas."
            };
        var info = new McpToolInfo(ToolName(capability.Id), capability.Title,
            $"{capability.Description} Serviço {capability.Service}; exige {string.Join(", ", capability.Permissions)}. Só leitura.",
            new JsonObject { ["type"] = "object", ["properties"] = properties, ["additionalProperties"] = false },
            OpenWorld: true);
        return new McpTool(info, call => ReadAsync(capability, call));
    }

    private async Task<string> ReadAsync(CloudCapability capability, McpCall call)
    {
        var args = call.Arguments;
        var project = Resolve(args);
        var link = Choose(project.ProjectId, capability, McpArguments.Text(args, "connection", MaxArgument));
        var region = capability.Region == CloudRegionUse.None ? null : McpArguments.Text(args, "region", 40);
        var limit = McpArguments.Number(args, "limit", DefaultLimit, 1, MaxLimit);
        var result = await cloud.Reads.ReadAsync(new CloudReadRequest(link.ConnectionId, capability.Id, region, limit, project.ProjectId),
            CloudCaller.Agent(call.Client), call.Ct);
        return Render(result);
    }

    private string RenderConnections(JsonObject args)
    {
        var project = Resolve(args);
        var links = cloud.Projects.AgentLinks().Where(link => link.ProjectId == project.ProjectId).ToList();
        if (links.Count == 0) return $"{project.DisplayName} não liberou nenhuma conexão cloud para agentes.";
        var text = new StringBuilder($"Conexões cloud de {project.DisplayName} liberadas para agentes (só leitura):");
        foreach (var link in links)
        {
            var connection = cloud.Connections.Require(link.ConnectionId);
            text.Append($"\n- {connection.Name} · id {connection.Id} · {connection.Provider} · conta {connection.Identity?.Account ?? "não identificada"}")
                .Append(connection.Settings.Region is { } region ? $" · região padrão {region}" : "")
                .Append($" · estado {connection.Status}")
                .Append($"\n  leituras: {string.Join(", ", link.Capabilities.Select(ToolName))}");
        }
        return text.ToString();
    }

    private AuthorizedProject Resolve(JsonObject args) =>
        new McpProjectResolver(workspaces(), workingDirectory, scope).Resolve(McpArguments.Text(args, "project", MaxArgument));

    // Só vínculo do projeto, liberado para agentes, com a capacidade; a conexão é escolhida por nome ou id, nunca adivinhada entre várias.
    private CloudProjectLink Choose(string projectId, CloudCapability capability, string? connection)
    {
        var candidates = cloud.Projects.AgentLinks()
            .Where(link => link.ProjectId == projectId && link.Capabilities.Contains(capability.Id, StringComparer.Ordinal))
            .ToList();
        if (connection is not null)
            candidates = candidates.Where(link => link.ConnectionId == connection
                || string.Equals(cloud.Connections.Require(link.ConnectionId).Name, connection, StringComparison.OrdinalIgnoreCase)).ToList();
        return candidates.Count switch
        {
            1 => candidates[0],
            0 => throw new McpToolException("Este projeto não liberou esta leitura para agentes" + (connection is null ? "." : $" na conexão '{connection}'.") + " Veja cloud_connections."),
            _ => throw new McpToolException("O projeto tem mais de uma conexão com esta leitura: informe 'connection' com um nome de cloud_connections.")
        };
    }

    private static string Render(CloudReadResult result)
    {
        var text = new StringBuilder()
            .Append($"{result.Capability} · conexão {result.ConnectionName} · conta {result.Account ?? "não identificada"}")
            .Append(result.Region is null ? "" : $" · região {result.Region}")
            .Append($" · lido em {result.ReadAt.UtcDateTime:yyyy-MM-dd HH:mm:ss} UTC\n");
        if (result.Rows.Count == 0) return text.Append("Nenhum item.").ToString();
        text.Append("| ").Append(string.Join(" | ", result.Columns.Select(c => c.Label))).Append(" |\n")
            .Append('|').Append(string.Concat(result.Columns.Select(_ => " --- |"))).Append('\n');
        foreach (var row in result.Rows) text.Append("| ").Append(string.Join(" | ", row.Select(Cell))).Append(" |\n");
        return text.Append(result.Truncated ? $"{result.Rows.Count} itens; havia mais. Peça com 'limit' maior (até {MaxLimit})." : $"{result.Rows.Count} itens.").ToString();
    }

    private static string Cell(string? value) => string.IsNullOrEmpty(value) ? "-" : value.Replace("|", "/").Replace('\n', ' ');

    private static JsonObject ProjectArgument() => new()
    {
        ["type"] = "string", ["description"] = "Nome ou id do projeto, como em list_projects. Sem ele, vale o projeto da pasta atual ou o único do escopo."
    };
}
