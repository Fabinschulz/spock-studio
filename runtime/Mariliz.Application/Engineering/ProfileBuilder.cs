using Mariliz.Application.Integrations;
using Mariliz.Core.Discovery;

namespace Mariliz.Application.Engineering;

public static class ProfileBuilder
{
    public const int MaxHistory = 100;

    public static TechnologyProfile Build(
        DiscoveryReport report,
        IReadOnlyList<ProjectIntegration> integrations,
        TechnologyProfile? previous,
        DateTimeOffset now)
    {
        var technologies = report.Stacks
            .GroupBy(stack => stack.Id, StringComparer.Ordinal)
            .Select(group => new ProfileTechnology(
                group.Key,
                group.First().Name,
                TechnologyCatalog.Category(group.Key),
                group.Select(stack => stack.Version).OfType<string>().Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList(),
                group.Select(stack => stack.Root.Length == 0 ? "." : stack.Root).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList(),
                // A melhor evidência entre as pastas (High é o menor valor do enum): basta uma prova forte de que a tecnologia existe.
                group.Min(stack => stack.Confidence),
                group.SelectMany(stack => stack.Evidence.Select(evidence => evidence.Path)).Distinct(StringComparer.Ordinal).Take(6).ToList()))
            .Concat(FrameworkTechnologies(report))
            .OrderBy(technology => technology.Category, StringComparer.Ordinal)
            .ThenBy(technology => technology.Name, StringComparer.Ordinal)
            .ToList();

        var tooling = report.Tooling
            .Select(tool => new ProfileTool(tool.Kind, tool.Name, tool.Command))
            .DistinctBy(tool => (tool.Kind, tool.Name, tool.Command))
            .ToList();

        var connections = integrations
            .Select(integration => new ProfileConnection(TechnologyCatalog.ConnectionRole(integration.Provider), integration.Provider, integration.Scope.ResourceName))
            .OrderBy(connection => connection.Role, StringComparer.Ordinal)
            .ThenBy(connection => connection.Resource, StringComparer.Ordinal)
            .ToList();

        var profile = new TechnologyProfile(
            report.ProjectId,
            now,
            technologies,
            tooling,
            report.AiEnvironments.Select(environment => environment.Name).ToList(),
            connections,
            // A orientação é sobre regra de stack: um framework repetiria a linha da stack dele.
            technologies.Where(technology => report.Stacks.Any(stack => stack.Id == technology.Id)).Select(TechnologyCatalog.Guidance).ToList(),
            []);

        var changes = previous is null
            ? [new ProfileChange(now, "created", "Perfil", null, technologies.Count == 1 ? "1 tecnologia" : $"{technologies.Count} tecnologias")]
            : Diff(previous, profile, now);
        var history = changes.Concat(previous?.History ?? []).Take(MaxHistory).ToList();
        return profile with { History = history };
    }

    // Uma dependência declarada prova que o framework está no projeto, não que é usado em toda parte: confiança média.
    private static IEnumerable<ProfileTechnology> FrameworkTechnologies(DiscoveryReport report) =>
        report.Frameworks
            .Where(framework => report.Stacks.All(stack => stack.Id != framework.Id))
            .GroupBy(framework => framework.Id, StringComparer.Ordinal)
            .Select(group => new ProfileTechnology(
                group.Key,
                group.First().Name,
                TechnologyCatalog.Category(group.Key),
                group.Select(framework => framework.Version).OfType<string>().Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList(),
                group.Select(framework => framework.Root.Length == 0 ? "." : framework.Root).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList(),
                Confidence.Medium,
                group.SelectMany(framework => framework.Evidence.Select(evidence => evidence.Path)).Distinct(StringComparer.Ordinal).Take(6).ToList()));

    internal static List<ProfileChange> Diff(TechnologyProfile before, TechnologyProfile after, DateTimeOffset now)
    {
        var changes = new List<ProfileChange>();
        var old = before.Technologies.ToDictionary(t => t.Id, StringComparer.Ordinal);
        var current = after.Technologies.ToDictionary(t => t.Id, StringComparer.Ordinal);

        foreach (var technology in after.Technologies.Where(t => !old.ContainsKey(t.Id)))
            changes.Add(new(now, "added", technology.Name, null, Versions(technology)));
        foreach (var technology in before.Technologies.Where(t => !current.ContainsKey(t.Id)))
            changes.Add(new(now, "removed", technology.Name, Versions(technology), null));
        foreach (var technology in after.Technologies.Where(t => old.ContainsKey(t.Id)))
        {
            var was = Versions(old[technology.Id]);
            var isNow = Versions(technology);
            if (was != isNow) changes.Add(new(now, "changed", technology.Name, was, isNow));
        }

        Compare(changes, before.Tooling.Select(ToolLabel), after.Tooling.Select(ToolLabel), "Ferramenta", now);
        Compare(changes, before.Connections.Select(ConnectionLabel), after.Connections.Select(ConnectionLabel), "Conexão", now);
        return changes;
    }

    private static void Compare(List<ProfileChange> changes, IEnumerable<string> before, IEnumerable<string> after, string subject, DateTimeOffset now)
    {
        var old = before.ToHashSet(StringComparer.Ordinal);
        var current = after.ToHashSet(StringComparer.Ordinal);
        foreach (var item in current.Except(old).Order(StringComparer.Ordinal)) changes.Add(new(now, "added", subject, null, item));
        foreach (var item in old.Except(current).Order(StringComparer.Ordinal)) changes.Add(new(now, "removed", subject, item, null));
    }

    private static string Versions(ProfileTechnology technology) =>
        technology.Versions.Count == 0 ? "sem versão declarada" : string.Join(", ", technology.Versions);

    private static string ToolLabel(ProfileTool tool) => tool.Command is { } command ? $"{tool.Name} ({command})" : tool.Name;

    private static string ConnectionLabel(ProfileConnection connection) => $"{connection.Role}: {connection.Resource}";
}
