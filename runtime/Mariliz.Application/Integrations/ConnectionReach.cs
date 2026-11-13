using Mariliz.Application.Workspace;

namespace Mariliz.Application.Integrations;

// Hoje todo conector só lê (o transporte só tem GET); o vocabulário já distingue "altera dados" para que uma escrita futura nunca possa ser descrita como leitura.
public static class ImpactKinds
{
    public const string Read = "read";
    public const string Write = "write";

    // O conector da conexão não existe mais nesta versão: nada se afirma sobre o que ele faz.
    public const string Unknown = "unknown";
}

public sealed record ReachDataKind(string Id, string Label);

/// <param name="ProjectName">Nulo quando o projeto não está autorizado neste computador: o motor não conhece o nome nem o caminho.</param>
public sealed record ReachProject(
    string ProjectId,
    string? ProjectName,
    string Resource,
    string ResourceName,
    IReadOnlyList<ReachDataKind> DataKinds,
    bool FeedsKnowledge,
    DateTimeOffset LinkedAt);

public sealed record ConnectionReach(string ConnectionId, string Impact, IReadOnlyList<ReachProject> Projects);

// Não concede nada: uma conexão ativa não dá acesso a agentes; o recorte e a política de cada vínculo é que decidem.
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
