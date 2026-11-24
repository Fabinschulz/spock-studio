using Mariliz.Application.Discovery;
using Mariliz.Application.Ports;

namespace Mariliz.Application.Compatibility;

/// <param name="Guide">A página oficial das mudanças incompatíveis da linha de destino; nula quando o Spock não conhece uma.</param>
public sealed record VersionHop(string From, string To, string? Guide);

/// <param name="Id">V1, V2...: é como o passo de atualização do plano cita o achado.</param>
/// <param name="Target">A linha que a política do Spock recomenda hoje (MV-5).</param>
/// <param name="Hops">O caminho de LTS em LTS até o alvo; sem LTS na tecnologia, de linha em linha.</param>
public sealed record VersionFinding(
    string Id,
    string Root,
    string TechnologyId,
    string Name,
    string? Version,
    string Cycle,
    string Status,
    string StatusLabel,
    string? Eol,
    string Target,
    IReadOnlyList<VersionHop> Hops);

// MV-7: o que o plano de modernização pode atualizar e por onde. Cada pasta com uma tecnologia do catálogo numa linha mais
// antiga que a recomendada vira um achado, com o caminho calculado pelo catálogo. O plano só aceita saltos desse caminho.
public static class VersionUpgrades
{
    public static IReadOnlyList<VersionFinding> Find(IProjectFiles files, ReleaseCatalog catalog, DateTimeOffset now)
    {
        var scan = ProjectDiscoverer.Scan(files);
        var modules = CompatibilityAssessor.Assess(ProjectModules.From(scan.Stacks, scan.Frameworks, scan.Runtimes), catalog, now).Modules;
        return Find(modules, catalog, DateOnly.FromDateTime(now.UtcDateTime));
    }

    public static IReadOnlyList<VersionFinding> Find(IReadOnlyList<ModuleCompatibility> modules, ReleaseCatalog catalog, DateOnly today)
    {
        var targets = new Dictionary<string, string?>(StringComparer.Ordinal);
        var findings = new List<VersionFinding>();
        foreach (var module in modules
                     .Where(module => module.Cycle is not null)
                     .DistinctBy(module => (module.Root, module.TechnologyId))
                     .OrderBy(module => module.Root, StringComparer.Ordinal)
                     .ThenBy(module => module.TechnologyId, StringComparer.Ordinal))
        {
            if (catalog.Find(module.TechnologyId) is not { } technology) continue;
            if (!targets.TryGetValue(module.TechnologyId, out var target))
            {
                target = VersionPolicy.Recommend([module.TechnologyId], VersionGoals.Production, catalog, today).Technologies.FirstOrDefault()?.Choice?.Cycle;
                targets[module.TechnologyId] = target;
            }
            if (target is null || VersionCycles.Compare(module.Cycle!, target) >= 0) continue;
            findings.Add(new VersionFinding($"V{findings.Count + 1}", module.Root, module.TechnologyId, module.Name, module.Version, module.Cycle!,
                module.Status, module.StatusLabel, module.Eol, target, Hops(technology, module.Cycle!, target)));
        }
        return findings;
    }

    // De LTS em LTS é o caminho que os fornecedores documentam (o guia de cada LTS parte da anterior); quem não tem LTS
    // anda de linha em linha. Pré-lançamento nunca entra.
    public static IReadOnlyList<VersionHop> Hops(CatalogTechnology technology, string from, string to)
    {
        var between = technology.Lines
            .Where(line => line.Status != "preview")
            .Where(line => VersionCycles.Compare(line.Cycle, from) > 0 && VersionCycles.Compare(line.Cycle, to) <= 0)
            .ToList();
        var hasLts = technology.Lines.Any(line => line.Channel == "lts");
        var stops = between
            .Where(line => !hasLts || line.Channel == "lts" || line.Cycle == to)
            .Select(line => line.Cycle)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(cycle => cycle, Comparer<string>.Create(VersionCycles.Compare))
            .ToList();
        var hops = new List<VersionHop>();
        var current = from;
        foreach (var stop in stops)
        {
            hops.Add(new VersionHop(current, stop, UpgradeGuides.For(technology, stop)));
            current = stop;
        }
        return hops;
    }
}

// As páginas oficiais de mudanças incompatíveis por linha de destino. Só fontes do próprio fornecedor; tecnologia fora daqui
// pede ao agente um link https da fonte oficial.
public static class UpgradeGuides
{
    private static readonly Dictionary<string, string> React = new(StringComparer.Ordinal)
    {
        ["18"] = "https://react.dev/blog/2022/03/08/react-18-upgrade-guide",
        ["19"] = "https://react.dev/blog/2024/04/25/react-19-upgrade-guide"
    };

    public static string? For(CatalogTechnology technology, string to) => technology.Id switch
    {
        "dotnet" => $"https://learn.microsoft.com/dotnet/core/compatibility/{to}",
        "node" => $"https://github.com/nodejs/node/blob/main/doc/changelogs/CHANGELOG_V{to}.md",
        "python" => $"https://docs.python.org/3/whatsnew/{to}.html",
        "php" => $"https://www.php.net/manual/en/migration{to.Replace(".", "", StringComparison.Ordinal)}.php",
        "angular" => "https://angular.dev/update-guide",
        "react" => React.GetValueOrDefault(to),
        // O guia da linha mais nova fica no site principal; os anteriores, no subdomínio da versão.
        "vite" => technology.Lines.Select(line => line.Cycle).Max(Comparer<string>.Create(VersionCycles.Compare)) == to
            ? "https://vite.dev/guide/migration"
            : $"https://v{to}.vite.dev/guide/migration",
        _ => null
    };
}
