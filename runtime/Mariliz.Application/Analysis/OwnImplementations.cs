namespace Mariliz.Application.Analysis;

/// <param name="Folder">A pasta do código próprio, pela primeira que aparece no projeto ("src/Domain/Libs/Mediator").</param>
/// <param name="Replaces">O framework que esse código faz as vezes ("MediatR").</param>
public sealed record OwnImplementation(string Name, string Folder, string Replaces, string ReplacesId);

// W3-4: um projeto com mediador, barramento ou resiliência escritos em casa não deve receber a sugestão de trocar por pacote.
// Vale a pasta (Libs/Mediator) ou o projeto (BuildingBlocks/Mediator) com código .cs dentro, e só quando o framework
// equivalente não está nos pacotes: com MediatR instalado, uma pasta Mediator é só onde ele é configurado.
internal static class OwnImplementations
{
    private static readonly (string[] Names, string FrameworkId, string Framework)[] Known =
    [
        (["Mediator", "Mediation"], "mediatr", "MediatR"),
        (["Messaging", "EventBus", "MessageBus"], "masstransit", "MassTransit"),
        (["Resilience"], "polly", "Polly")
    ];

    internal static List<OwnImplementation> Detect(IReadOnlyList<string> all, IReadOnlyList<DotnetProjectNode> projects)
    {
        var used = projects.SelectMany(project => project.Frameworks).ToHashSet(StringComparer.Ordinal);
        var code = all.Where(path => path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) && !IsTest(path)).ToList();
        var found = new List<OwnImplementation>();
        foreach (var (names, frameworkId, framework) in Known)
        {
            if (used.Contains(frameworkId)) continue;
            var folder = code.Select(path => FolderNamed(path, names)).OfType<string>()
                .OrderBy(path => path.Count(c => c == '/')).ThenBy(path => path, StringComparer.Ordinal).FirstOrDefault();
            if (folder is not null) found.Add(new OwnImplementation(folder[(folder.LastIndexOf('/') + 1)..], folder, framework, frameworkId));
        }
        return found;
    }

    private static string? FolderNamed(string path, string[] names)
    {
        var segments = path.Split('/');
        for (var index = 0; index < segments.Length - 1; index++)
            if (names.Contains(segments[index], StringComparer.OrdinalIgnoreCase))
                return string.Join('/', segments[..(index + 1)]);
        return null;
    }

    private static bool IsTest(string path) =>
        path.Split('/').Any(segment => segment is "tests" or "test" || segment.EndsWith(".Tests", StringComparison.OrdinalIgnoreCase));
}
