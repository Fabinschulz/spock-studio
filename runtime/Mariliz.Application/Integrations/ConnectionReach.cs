using Mariliz.Application.Workspace;

namespace Mariliz.Application.Integrations;

/// <summary>
/// O que uma conexão pode fazer no provedor. Hoje todo conector só lê: o contrato de transporte tem só <c>GET</c>
/// (<see cref="IConnectorTransport"/>) e um pedido não carrega método nem corpo (<see cref="ConnectorRequest"/>). O vocabulário
/// já distingue "altera dados" para que uma escrita futura nunca possa ser descrita como leitura.
/// </summary>
public static class ImpactKinds
{
    public const string Read = "read";
    public const string Write = "write";

    /// <summary>O conector da conexão não existe mais nesta versão: nada se afirma sobre o que ele faz.</summary>
    public const string Unknown = "unknown";
}

public sealed record ReachDataKind(string Id, string Label);

/// <summary>Um projeto que usa a conexão, com o recurso, o que se lê dele e se os dados podem virar contexto de IA.</summary>
/// <param name="ProjectName">Nulo quando o projeto não está autorizado neste computador: o motor não conhece o nome nem o caminho.</param>
public sealed record ReachProject(
    string ProjectId,
    string? ProjectName,
    string Resource,
    string ResourceName,
    IReadOnlyList<ReachDataKind> DataKinds,
    bool FeedsKnowledge,
    DateTimeOffset LinkedAt);

/// <summary>As quatro perguntas de uma conexão que a conexão em si não responde: onde é usada, o que se lê, o que pode fazer e se alimenta IA.</summary>
public sealed record ConnectionReach(string ConnectionId, string Impact, IReadOnlyList<ReachProject> Projects);

/// <summary>
/// Junta, para cada conexão, os vínculos de projeto que a usam. Só leitura e só do que o motor já guarda: nada vai ao provedor.
/// Não concede nada: uma conexão ativa não dá acesso a agentes, o recorte e a política de cada vínculo é que decidem.
/// </summary>
public sealed class ConnectionReachService(IIntegrationStore store, ConnectorRegistry connectors, WorkspaceAuthorizer workspace)
{
    public IReadOnlyList<ConnectionReach> List()
    {
        var descriptors = connectors.Catalog().ToDictionary(d => d.Provider, StringComparer.Ordinal);
        var names = workspace.Projects().ToDictionary(p => p.ProjectId, p => p.DisplayName, StringComparer.Ordinal);
        var integrations = store.Integrations();

        return store.Connections().Select(connection =>
        {
            descriptors.TryGetValue(connection.Provider, out var descriptor);
            var labels = descriptor?.DataKinds.ToDictionary(kind => kind.Id, kind => kind.Label, StringComparer.Ordinal) ?? [];

            var projects = integrations
                .Where(integration => integration.ConnectionId == connection.Id)
                .Select(integration => new ReachProject(
                    integration.ProjectId,
                    names.GetValueOrDefault(integration.ProjectId),
                    integration.Scope.Resource,
                    integration.Scope.ResourceName,
                    integration.Scope.DataKinds.Select(kind => new ReachDataKind(kind, labels.GetValueOrDefault(kind, kind))).ToList(),
                    integration.Policy.FeedsKnowledge,
                    integration.LinkedAt))
                .OrderBy(project => project.ProjectName ?? "￿", StringComparer.OrdinalIgnoreCase)
                .ThenBy(project => project.ResourceName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return new ConnectionReach(connection.Id, descriptor?.Impact ?? ImpactKinds.Unknown, projects);
        }).ToList();
    }
}
