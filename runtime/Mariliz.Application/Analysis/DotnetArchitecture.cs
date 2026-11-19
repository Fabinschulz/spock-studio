using Mariliz.Application.Discovery;
using Mariliz.Application.Ports;
using Mariliz.Core.Discovery;
using Mariliz.Core.Text;
using L = Mariliz.Application.Analysis.ArchitectureLayers;

namespace Mariliz.Application.Analysis;

// O grafo sai dos ProjectReference; a camada, do nome do projeto (Catalog.Application, Mariliz.Infrastructure.Cloud) ou, sem
// nome conhecido, da pasta (src/BuildingBlocks/Mediator). Um projeto sem camada reconhecida não gera achado de direção.
internal static class DotnetArchitecture
{
    private static readonly Dictionary<string, string> LayerBySegment = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Domain"] = L.Domain, ["Core"] = L.Domain, ["Entities"] = L.Domain, ["Model"] = L.Domain, ["Models"] = L.Domain,
        ["Application"] = L.Application, ["UseCases"] = L.Application,
        ["Infrastructure"] = L.Infrastructure, ["Infra"] = L.Infrastructure, ["Persistence"] = L.Infrastructure, ["Data"] = L.Infrastructure,
        ["DataAccess"] = L.Infrastructure, ["Integrations"] = L.Infrastructure, ["Integration"] = L.Infrastructure, ["Adapters"] = L.Infrastructure,
        ["Api"] = L.Host, ["WebApi"] = L.Host, ["Web"] = L.Host, ["Host"] = L.Host, ["Hosts"] = L.Host, ["Worker"] = L.Host, ["Cli"] = L.Host,
        ["Runtime"] = L.Host, ["Server"] = L.Host, ["Functions"] = L.Host, ["Gateway"] = L.Host,
        ["SharedKernel"] = L.Shared, ["Shared"] = L.Shared, ["Common"] = L.Shared, ["BuildingBlocks"] = L.Shared, ["Kernel"] = L.Shared,
        ["Contracts"] = L.Shared, ["Abstractions"] = L.Shared
    };

    // Orquestrar casos de uso e validar a entrada é papel da aplicação; banco, nuvem, web, cache e SDKs de IA são infraestrutura.
    private static readonly HashSet<string> AllowedInApplication = new(StringComparer.Ordinal) { "mediatr", "fluentvalidation", "automapper" };

    private static readonly HashSet<string> TestFrameworks = new(StringComparer.Ordinal) { "xunit", "nunit", "mstest" };

    internal static (List<DotnetProjectNode> Nodes, List<ArchitectureFinding> Findings) Analyze(IProjectFiles files, IReadOnlyList<string> all)
    {
        var central = new DotnetDetector.CentralVersions(files, all);
        var projects = all.Where(path => path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
            .Order(StringComparer.Ordinal)
            .Select(path => DotnetDetector.Read(files, path, central, []))
            .OfType<DotnetDetector.DotnetProject>()
            .ToList();
        var layers = projects.ToDictionary(project => project.Path, LayerOf, StringComparer.Ordinal);
        var frameworks = projects.ToDictionary(project => project.Path, project => FrameworksOf(all, project), StringComparer.Ordinal);
        var tested = projects.Where(project => layers[project.Path] == L.Tests)
            .SelectMany(project => project.ProjectReferences)
            .ToHashSet(StringComparer.Ordinal);

        var nodes = projects.Select(project => new DotnetProjectNode(
                project.Path,
                NameOf(project.Path),
                layers[project.Path],
                project.ProjectReferences.Where(layers.ContainsKey).ToList(),
                frameworks[project.Path].Select(framework => framework.Id).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList(),
                layers[project.Path] == L.Tests || tested.Contains(project.Path)))
            .ToList();

        var findings = new List<ArchitectureFinding>();
        foreach (var project in projects)
        {
            var from = layers[project.Path];
            foreach (var reference in project.ProjectReferences.Where(layers.ContainsKey))
            {
                var to = layers[reference];
                if (!Violates(from, to)) continue;
                findings.Add(new ArchitectureFinding(ArchitectureFindingCodes.DependencyDirection, ArchitectureSeverities.Warning,
                    $"{NameOf(project.Path)} ({Label(from)}) depende de {NameOf(reference)} ({Label(to)})",
                    [new Evidence(project.Path, $"ProjectReference={reference}")]));
            }

            var used = frameworks[project.Path].Where(framework => !TestFrameworks.Contains(framework.Id)).ToList();
            var misplaced = from switch
            {
                L.Domain => used,
                L.Application => used.Where(framework => !AllowedInApplication.Contains(framework.Id)).ToList(),
                _ => []
            };
            if (misplaced.Count == 0) continue;
            var code = from == L.Domain ? ArchitectureFindingCodes.FrameworkInDomain : ArchitectureFindingCodes.InfrastructureInApplication;
            findings.Add(new ArchitectureFinding(code, ArchitectureSeverities.Warning,
                $"{NameOf(project.Path)} ({Label(from)}) usa {string.Join(", ", misplaced.Select(framework => framework.Name).Distinct(StringComparer.Ordinal))}",
                misplaced.SelectMany(framework => framework.Evidence).Take(6).ToList()));
        }

        var untested = nodes.Where(node => !node.Tested && node.Layer != L.Unknown).ToList();
        if (untested.Count > 0 && nodes.Any(node => node.Layer == L.Tests))
            findings.Add(new ArchitectureFinding(ArchitectureFindingCodes.UntestedProject, ArchitectureSeverities.Info,
                $"{untested.Count} projeto(s) sem projeto de teste que os referencie: {string.Join(", ", untested.Take(8).Select(node => node.Name))}",
                untested.Take(8).Select(node => new Evidence(node.Path)).ToList()));
        else if (nodes.Count > 0 && nodes.All(node => node.Layer != L.Tests))
            findings.Add(new ArchitectureFinding(ArchitectureFindingCodes.UntestedProject, ArchitectureSeverities.Info,
                "Nenhum projeto de teste .NET", nodes.Take(3).Select(node => new Evidence(node.Path)).ToList()));

        return (nodes, findings);
    }

    internal static string Label(string layer) => layer switch
    {
        L.Domain => "domínio",
        L.Application => "aplicação",
        L.Infrastructure => "infraestrutura",
        L.Host => "host",
        L.Shared => "compartilhado",
        L.Tests => "testes",
        _ => "camada não reconhecida"
    };

    // Só a direção conhecida conta: domínio e compartilhado no centro, depois aplicação, infraestrutura e o host por fora.
    private static bool Violates(string from, string to)
    {
        if (from is L.Tests or L.Host or L.Unknown) return false;
        if (to == L.Tests) return true;
        return to != L.Unknown && Rank(to) > Rank(from);
    }

    private static int Rank(string layer) => layer switch
    {
        L.Domain or L.Shared => 0,
        L.Application => 1,
        L.Infrastructure => 2,
        _ => 3
    };

    private static string LayerOf(DotnetDetector.DotnetProject project)
    {
        var name = NameOf(project.Path);
        var segments = name.Split('.');
        if (project.IsTest || segments.Any(segment => segment.EndsWith("Test", StringComparison.OrdinalIgnoreCase)
                || segment.EndsWith("Tests", StringComparison.OrdinalIgnoreCase)))
            return L.Tests;
        foreach (var segment in segments.Reverse())
            if (LayerBySegment.TryGetValue(segment, out var layer)) return layer;
        foreach (var folder in ProjectPath.Parent(project.Path).Split('/').Reverse())
        {
            if (folder.Equals("tests", StringComparison.OrdinalIgnoreCase) || folder.Equals("test", StringComparison.OrdinalIgnoreCase)) return L.Tests;
            if (LayerBySegment.TryGetValue(folder, out var layer)) return layer;
        }
        return L.Unknown;
    }

    private static List<FrameworkFinding> FrameworksOf(IReadOnlyList<string> all, DotnetDetector.DotnetProject project)
    {
        var found = new List<FrameworkFinding>();
        DotnetDetector.Collect(found, all, "", project);
        return found;
    }

    private static string NameOf(string csproj) => Path.GetFileNameWithoutExtension(csproj);
}
