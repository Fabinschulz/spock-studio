using System.Xml.Linq;
using Mariliz.Application.Ports;
using Mariliz.Core.Discovery;
using Mariliz.Core.Text;

namespace Mariliz.Application.Discovery;

internal static class DotnetDetector
{
    private const string CentralVersionsFile = "Directory.Packages.props";
    private const string SharedPropsFile = "Directory.Build.props";

    internal static void Detect(IProjectFiles files, IReadOnlyList<string> all,
        List<StackFinding> stacks, List<ToolingFinding> tooling, List<FrameworkFinding> found, List<DiscoveryWarning> warnings)
    {
        var solutions = all.Where(p => p.EndsWith(".sln", StringComparison.OrdinalIgnoreCase) || p.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase)).OrderBy(ProjectPath.Depth).ThenBy(p => p, StringComparer.Ordinal).ToList();
        var projects = all.Where(p => p.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)).OrderBy(p => p, StringComparer.Ordinal).ToList();
        if (solutions.Count == 0 && projects.Count == 0) return;

        // Os frameworks ficam na raiz da stack, não na pasta de cada .csproj: numa solução com dezenas de projetos, o perfil
        // listaria uma raiz por projeto para o mesmo EF Core.
        var root = solutions.Count > 0 ? ProjectPath.Parent(solutions[0]) : ProjectPath.Parent(projects[0]);
        var central = new CentralVersions(files, all);
        var evidence = new List<Evidence>();
        var frameworks = new List<string>();
        var notes = new List<string>();
        var testProjects = 0;
        var web = false;

        foreach (var csproj in projects)
        {
            if (Parse(files, csproj, warnings) is not { } xml) continue;
            var tfm = xml.Descendants().FirstOrDefault(e => e.Name.LocalName == "TargetFramework")?.Value
                      ?? xml.Descendants().FirstOrDefault(e => e.Name.LocalName == "TargetFrameworks")?.Value;
            var targets = tfm?.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [];
            if (targets.Length > 0)
            {
                frameworks.AddRange(targets);
                if (evidence.Count < 12) evidence.Add(new Evidence(csproj, $"TargetFramework={tfm}"));
            }
            var sdks = Sdks(xml);
            if (sdks.Any(sdk => sdk.Contains("Web", StringComparison.OrdinalIgnoreCase))) web = true;
            var packages = Packages(xml, central.For(csproj));
            if (packages.Keys.Any(IsTestFramework)) testProjects++;
            Frameworks.Add(found, Frameworks.Dotnet, Technologies.Dotnet.Id, root, csproj, packages, byPrefix: true, StringComparison.OrdinalIgnoreCase);
            Implied(found, all, root, csproj, xml, sdks, PlatformVersion(targets));
        }

        // Um pacote no Directory.Build.props entra em todos os projetos abaixo dele, sem aparecer em nenhum .csproj.
        foreach (var props in all.Where(path => IsFile(path, SharedPropsFile)).Order(StringComparer.Ordinal).Take(10))
            if (Parse(files, props, warnings) is { } xml)
                Frameworks.Add(found, Frameworks.Dotnet, Technologies.Dotnet.Id, root, props, Packages(xml, central.For(props)), byPrefix: true, StringComparison.OrdinalIgnoreCase);

        foreach (var sln in solutions.Take(3)) evidence.Insert(0, new Evidence(sln));

        var version = Newest(frameworks);
        if (web) notes.Add("ASP.NET Core (Microsoft.NET.Sdk.Web)");
        if (testProjects > 0) notes.Add($"{testProjects} projeto(s) de teste");
        if (version is null)
            warnings.Add(new(DiscoveryWarningCodes.UnknownVersion, "A versão do .NET não pôde ser determinada pelos arquivos de projeto.", projects.Take(3).ToList()));

        stacks.Add(new StackFinding(Technologies.Dotnet.Id, Technologies.Dotnet.Name, root, version, version is null ? Confidence.Medium : Confidence.High, evidence, notes));

        var target = solutions.Count > 0 ? solutions[0] : projects.Count == 1 ? projects[0] : null;
        var arg = target is null ? "" : " " + target;
        var ev = target is null ? evidence.Take(1).ToList() : [new Evidence(target)];
        tooling.Add(new ToolingFinding("build", "dotnet build", $"dotnet build{arg}", ev));
        if (testProjects > 0) tooling.Add(new ToolingFinding("test", "dotnet test", $"dotnet test{arg}", ev));
    }

    private static XDocument? Parse(IProjectFiles files, string path, List<DiscoveryWarning> warnings)
    {
        if (files.ReadText(path) is not { } text) return null;
        try
        {
            return XDocument.Parse(text);
        }
        catch (System.Xml.XmlException)
        {
            warnings.Add(new(DiscoveryWarningCodes.UnknownVersion, $"Não foi possível ler {path}.", [path]));
            return null;
        }
    }

    private static bool IsTestFramework(string package) =>
        package.StartsWith("xunit", StringComparison.OrdinalIgnoreCase) || package.StartsWith("NUnit", StringComparison.OrdinalIgnoreCase)
        || package.StartsWith("MSTest", StringComparison.OrdinalIgnoreCase);

    private static bool IsFile(string path, string name) => string.Equals(Path.GetFileName(path), name, StringComparison.OrdinalIgnoreCase);

    private static bool Contains(string folder, string path) => folder.Length == 0 || path.StartsWith(folder + "/", StringComparison.Ordinal);

    // <Project Sdk="Microsoft.NET.Sdk.Web/1.0"> ou <Sdk Name="..." />: a versão depois da barra não importa.
    private static List<string> Sdks(XDocument xml) =>
        (xml.Root?.Attribute("Sdk")?.Value.Split(';') ?? [])
            .Concat(xml.Descendants().Where(e => e.Name.LocalName == "Sdk").Select(e => e.Attribute("Name")?.Value ?? ""))
            .Select(sdk => sdk.Split('/')[0].Trim())
            .Where(sdk => sdk.Length > 0)
            .ToList();

    // Sem versão no PackageReference, ela vem do VersionOverride ou do Directory.Packages.props (gestão central de pacotes).
    private static Dictionary<string, string> Packages(XDocument xml, IReadOnlyDictionary<string, string> central)
    {
        var packages = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var reference in xml.Descendants().Where(e => e.Name.LocalName == "PackageReference"))
        {
            var name = reference.Attribute("Include")?.Value.Trim();
            if (string.IsNullOrEmpty(name)) continue;
            packages.TryAdd(name, Value(reference, "Version") ?? Value(reference, "VersionOverride") ?? central.GetValueOrDefault(name) ?? "");
        }
        return packages;
    }

    private static string? Value(XElement element, string name) =>
        element.Attribute(name)?.Value ?? element.Elements().FirstOrDefault(child => child.Name.LocalName == name)?.Value;

    private static void Implied(List<FrameworkFinding> found, IReadOnlyList<string> all, string root, string csproj, XDocument xml,
        List<string> sdks, string? version)
    {
        var dotnet = Technologies.Dotnet.Id;
        foreach (var sdk in sdks)
        {
            var known = sdk.ToLowerInvariant() switch
            {
                "microsoft.net.sdk.web" => Frameworks.AspNetCore,
                "microsoft.net.sdk.blazorwebassembly" => Frameworks.Blazor,
                "microsoft.net.sdk.worker" => Frameworks.Worker,
                _ => null
            };
            if (known is not null) Frameworks.Add(found, known, dotnet, root, version, new Evidence(csproj, $"Sdk={sdk}"));
        }
        if (xml.Descendants().Any(e => e.Name.LocalName == "FrameworkReference"
                && string.Equals(e.Attribute("Include")?.Value, "Microsoft.AspNetCore.App", StringComparison.OrdinalIgnoreCase)))
            Frameworks.Add(found, Frameworks.AspNetCore, dotnet, root, version, new Evidence(csproj, "FrameworkReference=Microsoft.AspNetCore.App"));
        if (xml.Descendants().Any(e => e.Name.LocalName == "UseMaui" && string.Equals(e.Value.Trim(), "true", StringComparison.OrdinalIgnoreCase)))
            Frameworks.Add(found, Frameworks.Maui, dotnet, root, version, new Evidence(csproj, "UseMaui=true"));

        // Componente .razor é Blazor; as Razor Pages usam .cshtml.
        if (found.Any(item => item.Id == Frameworks.Blazor.Id && item.Root == root)) return;
        var folder = ProjectPath.Parent(csproj);
        if (all.FirstOrDefault(path => path.EndsWith(".razor", StringComparison.OrdinalIgnoreCase) && Contains(folder, path)) is { } component)
            Frameworks.Add(found, Frameworks.Blazor, dotnet, root, version, new Evidence(component));
    }

    // ASP.NET Core, Blazor e MAUI acompanham a versão do .NET do projeto (net10.0 vira 10.0); o .NET Framework não tem nenhum deles.
    private static string? PlatformVersion(IEnumerable<string> targets) =>
        Newest(targets) is { } newest && Rank(newest) is { Family: >= 2 } ? Frameworks.VersionOf(newest.Split('-')[0]) : null;

    // Vale o Directory.Packages.props mais próximo acima do projeto, como no MSBuild.
    private sealed class CentralVersions(IProjectFiles files, IReadOnlyList<string> all)
    {
        private static readonly IReadOnlyDictionary<string, string> None = new Dictionary<string, string>();
        private readonly List<string> props = all.Where(path => IsFile(path, CentralVersionsFile)).OrderByDescending(ProjectPath.Depth).ToList();
        private readonly Dictionary<string, IReadOnlyDictionary<string, string>> read = new(StringComparer.Ordinal);

        internal IReadOnlyDictionary<string, string> For(string project)
        {
            if (props.FirstOrDefault(path => Contains(ProjectPath.Parent(path), project)) is not { } nearest) return None;
            if (!read.TryGetValue(nearest, out var versions)) read[nearest] = versions = Read(nearest);
            return versions;
        }

        private IReadOnlyDictionary<string, string> Read(string path)
        {
            if (files.ReadText(path) is not { } text) return None;
            try
            {
                return XDocument.Parse(text).Descendants()
                    .Where(e => e.Name.LocalName == "PackageVersion" && e.Attribute("Include") is not null && e.Attribute("Version") is not null)
                    .GroupBy(e => e.Attribute("Include")!.Value.Trim(), StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(group => group.Key, group => group.First().Attribute("Version")!.Value, StringComparer.OrdinalIgnoreCase);
            }
            catch (System.Xml.XmlException)
            {
                return None;
            }
        }
    }

    // Comparar como texto falha: "net9.0" > "net10.0" e "netstandard2.0" > "net10.0" na ordem alfabética. O sufixo de plataforma (net8.0-windows) não conta.
    internal static string? Newest(IEnumerable<string> frameworks) =>
        frameworks.Select(tfm => (Tfm: tfm, Rank: Rank(tfm)))
            .Where(item => item.Rank is not null)
            .OrderByDescending(item => item.Rank!.Value.Family)
            .ThenByDescending(item => item.Rank!.Value.Version)
            .Select(item => item.Tfm)
            .FirstOrDefault();

    private static (int Family, Version Version)? Rank(string tfm)
    {
        var name = tfm.Split('-')[0].Trim().ToLowerInvariant();
        if (Parse(name, "netstandard") is { } standard) return (1, standard);
        if (Parse(name, "netcoreapp") is { } core) return (2, core);
        if (!name.StartsWith("net", StringComparison.Ordinal)) return null;
        var digits = name[3..];
        // .NET 5+ usa ponto (net8.0, net10.0). O .NET Framework escreve um dígito por componente (net472 = 4.7.2, net48 = 4.8).
        if (digits.Contains('.')) return Version.TryParse(digits, out var modern) ? (3, modern) : null;
        if (digits.Length is < 2 or > 4 || !digits.All(char.IsAsciiDigit)) return null;
        return Version.TryParse(string.Join('.', digits.ToCharArray()), out var framework) ? (0, framework) : null;
    }

    private static Version? Parse(string name, string family) =>
        name.StartsWith(family, StringComparison.Ordinal) && Version.TryParse(name[family.Length..], out var version) ? version : null;
}
