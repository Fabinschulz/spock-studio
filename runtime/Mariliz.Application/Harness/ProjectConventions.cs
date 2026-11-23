using System.Text;
using Mariliz.Application.Analysis;

namespace Mariliz.Application.Harness;

// W3-4: o que a análise de arquitetura acha no código vira regra, marcada como convenção do projeto. As regras de padrões da
// pasta Organização dizem o que outros projetos fazem; esta diz o que este faz, e vale antes delas.
public static class ProjectConventions
{
    public const string Classification = "project-convention";
    private const int MaxDivergences = 10;

    private static readonly string[] LayerOrder =
        [ArchitectureLayers.Domain, ArchitectureLayers.Shared, ArchitectureLayers.Application, ArchitectureLayers.Infrastructure, ArchitectureLayers.Host];

    public static bool Applies(ArchitectureReport report) =>
        report.DotnetProjects.Any(node => node.Layer != ArchitectureLayers.Tests) || report.Frontends.Count > 0;

    public static IReadOnlyList<string> Evidence(ArchitectureReport report) =>
        report.DotnetProjects.Select(node => node.Path).Concat(report.Frontends.Select(module => module.Source)).Take(4).ToList();

    // Sem tamanho de arquivo nem contagem de teste: a regra só muda quando a organização do código muda.
    public static string Render(ArchitectureReport report)
    {
        var sb = new StringBuilder();
        sb.Append("# Project conventions\n\n");
        sb.Append("Managed by Spock Studio. Generated from the deterministic architecture analysis of this repository (classification: ")
          .Append(Classification).Append(").\n");
        sb.Append("This is how the code is organized today, and it comes before the Organization patterns: when a pattern and this file\n");
        sb.Append("disagree, follow this file. If a change would go against a convention here, say so instead of changing it silently.\n");

        var production = report.DotnetProjects.Where(node => node.Layer != ArchitectureLayers.Tests).ToList();
        if (production.Count > 0)
        {
            sb.Append("\n## .NET layers\n\n");
            foreach (var layer in LayerOrder.Append(ArchitectureLayers.Unknown))
            {
                var names = production.Where(node => node.Layer == layer).Select(node => node.Name).ToList();
                if (names.Count > 0) sb.Append("- ").Append(Label(layer)).Append(": ").Append(string.Join(", ", names)).Append('\n');
            }
            sb.Append("\nReferences point inward: host, then infrastructure, then application, then domain and shared code. Keep new\n");
            sb.Append("project references in that direction.\n");

            var frameworks = production.Where(node => node.Frameworks.Count > 0).ToList();
            if (frameworks.Count > 0)
            {
                sb.Append("\n## Frameworks in use\n\n");
                foreach (var node in frameworks)
                    sb.Append("- ").Append(node.Name).Append(": ").Append(string.Join(", ", node.Frameworks)).Append('\n');
                sb.Append("\nUse these before adding a package that does the same job.\n");
            }
        }

        if (report.OwnImplementations.Count > 0)
        {
            sb.Append("\n## The project's own building blocks\n\n");
            foreach (var own in report.OwnImplementations)
                sb.Append("- ").Append(own.Name).Append(" in `").Append(own.Folder).Append("`: use it; do not add ").Append(own.Replaces).Append(".\n");
        }

        foreach (var module in report.Frontends)
        {
            sb.Append("\n## Frontend in `").Append(module.Source).Append("`\n\n");
            sb.Append("- Top-level folders: ").Append(Join(module.Folders)).Append(".\n");
            if (module.Features.Count > 0)
            {
                sb.Append("- Features: ").Append(Join(module.Features)).Append(".\n");
                sb.Append("- A feature uses another only through its public entry (`index`, `service`, `form`, `domain`), never its inner files.\n");
            }
        }

        var divergences = report.Findings.Where(finding => finding.Severity == ArchitectureSeverities.Warning).Take(MaxDivergences).ToList();
        if (divergences.Count > 0)
        {
            sb.Append("\n## Known divergences\n\n");
            sb.Append("The code already does these; do not repeat them in new code, and do not fix them as a side effect of another change.\n\n");
            foreach (var finding in divergences)
                sb.Append("- ").Append(finding.Title).Append('\n');
        }
        return sb.ToString();
    }

    private static string Label(string layer) => layer switch
    {
        ArchitectureLayers.Domain => "domain",
        ArchitectureLayers.Shared => "shared",
        ArchitectureLayers.Application => "application",
        ArchitectureLayers.Infrastructure => "infrastructure",
        ArchitectureLayers.Host => "host",
        _ => "no recognized layer"
    };

    private static string Join(IReadOnlyCollection<string> items) => items.Count == 0 ? "none" : string.Join(", ", items);
}
