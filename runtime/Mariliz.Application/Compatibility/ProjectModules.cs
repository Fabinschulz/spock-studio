using Mariliz.Core.Discovery;

namespace Mariliz.Application.Compatibility;

/// <summary>Uma tecnologia numa pasta do projeto, com a versão que os arquivos declaram. É fato da descoberta; a
/// classificação de suporte é calculada na hora, porque depende da data e do catálogo.</summary>
/// <param name="Origin">stack (linguagem ou plataforma), runtime (versão fixada à parte) ou framework.</param>
public sealed record ProjectModule(
    string Root,
    string TechnologyId,
    string Name,
    string? Version,
    string Origin,
    Confidence Confidence,
    IReadOnlyList<string> Evidence);

public static class ProjectModules
{
    public const string Stack = "stack";
    public const string Runtime = "runtime";
    public const string Framework = "framework";

    // Por pasta, sem agrupar: num monorepo, o serviço em .NET 6 e o em .NET 10 são restrições diferentes.
    public static IReadOnlyList<ProjectModule> From(DiscoveryReport report) =>
        report.Stacks.Select(stack => new ProjectModule(Folder(stack.Root), stack.Id, stack.Name, stack.Version, Stack, stack.Confidence, Paths(stack.Evidence)))
            .Concat(report.Runtimes.Select(runtime => new ProjectModule(Folder(runtime.Root), runtime.TechnologyId, runtime.Name, runtime.Version, Runtime,
                Confidence.High, Paths(runtime.Evidence))))
            .Concat(report.Frameworks.Where(framework => framework.Version is not null && report.Stacks.All(stack => stack.Id != framework.Id || stack.Root != framework.Root))
                .Select(framework => new ProjectModule(Folder(framework.Root), framework.Id, framework.Name, framework.Version, Framework, Confidence.Medium,
                    Paths(framework.Evidence))))
            .OrderBy(module => module.Root, StringComparer.Ordinal)
            .ThenBy(module => module.Origin, StringComparer.Ordinal)
            .ThenBy(module => module.TechnologyId, StringComparer.Ordinal)
            .ToList();

    private static string Folder(string root) => root.Length == 0 ? "." : root;

    private static IReadOnlyList<string> Paths(IEnumerable<Evidence> evidence) =>
        evidence.Select(item => item.Detail is null ? item.Path : $"{item.Path}: {item.Detail}").Distinct(StringComparer.Ordinal).Take(4).ToList();
}
