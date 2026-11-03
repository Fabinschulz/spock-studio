using System.Xml.Linq;
using Mariliz.Application.Ports;
using Mariliz.Core.Discovery;
using Mariliz.Core.Text;

namespace Mariliz.Application.Discovery;

internal static class DotnetDetector
{
    internal static void Detect(IProjectFiles files, IReadOnlyList<string> all,
        List<StackFinding> stacks, List<ToolingFinding> tooling, List<DiscoveryWarning> warnings)
    {
        var solutions = all.Where(p => p.EndsWith(".sln", StringComparison.OrdinalIgnoreCase) || p.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase)).OrderBy(ProjectPath.Depth).ThenBy(p => p, StringComparer.Ordinal).ToList();
        var projects = all.Where(p => p.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)).OrderBy(p => p, StringComparer.Ordinal).ToList();
        if (solutions.Count == 0 && projects.Count == 0) return;

        var evidence = new List<Evidence>();
        var frameworks = new List<string>();
        var notes = new List<string>();
        var testProjects = 0;
        var web = false;

        foreach (var csproj in projects)
        {
            var text = files.ReadText(csproj);
            if (text is null) continue;
            try
            {
                var xml = XDocument.Parse(text);
                var tfm = xml.Descendants().FirstOrDefault(e => e.Name.LocalName == "TargetFramework")?.Value
                          ?? xml.Descendants().FirstOrDefault(e => e.Name.LocalName == "TargetFrameworks")?.Value;
                if (!string.IsNullOrWhiteSpace(tfm))
                {
                    frameworks.AddRange(tfm.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
                    if (evidence.Count < 12) evidence.Add(new Evidence(csproj, $"TargetFramework={tfm}"));
                }
                var sdk = xml.Root?.Attribute("Sdk")?.Value;
                if (sdk is not null && sdk.Contains("Web", StringComparison.OrdinalIgnoreCase)) web = true;
                var refs = xml.Descendants().Where(e => e.Name.LocalName == "PackageReference").Select(e => e.Attribute("Include")?.Value ?? "").ToList();
                if (refs.Any(r => r.StartsWith("xunit", StringComparison.OrdinalIgnoreCase) || r.StartsWith("NUnit", StringComparison.OrdinalIgnoreCase) || r.StartsWith("MSTest", StringComparison.OrdinalIgnoreCase)))
                    testProjects++;
            }
            catch (System.Xml.XmlException)
            {
                warnings.Add(new(DiscoveryWarningCodes.UnknownVersion, $"Não foi possível ler {csproj}.", [csproj]));
            }
        }

        foreach (var sln in solutions.Take(3)) evidence.Insert(0, new Evidence(sln));

        var version = Newest(frameworks);
        if (web) notes.Add("ASP.NET Core (Microsoft.NET.Sdk.Web)");
        if (testProjects > 0) notes.Add($"{testProjects} projeto(s) de teste");
        if (version is null)
            warnings.Add(new(DiscoveryWarningCodes.UnknownVersion, "A versão do .NET não pôde ser determinada pelos arquivos de projeto.", projects.Take(3).ToList()));

        var root = solutions.Count > 0 ? ProjectPath.Parent(solutions[0]) : ProjectPath.Parent(projects[0]);
        stacks.Add(new StackFinding("dotnet", ".NET", root, version, version is null ? Confidence.Medium : Confidence.High, evidence, notes));

        var target = solutions.Count > 0 ? solutions[0] : projects.Count == 1 ? projects[0] : null;
        var arg = target is null ? "" : " " + target;
        var ev = target is null ? evidence.Take(1).ToList() : [new Evidence(target)];
        tooling.Add(new ToolingFinding("build", "dotnet build", $"dotnet build{arg}", ev));
        if (testProjects > 0) tooling.Add(new ToolingFinding("test", "dotnet test", $"dotnet test{arg}", ev));
    }

    /// <summary>
    /// O framework mais novo da solução, comparando família e versão (texto não serve: "net9.0" &gt; "net10.0" e
    /// "netstandard2.0" &gt; "net10.0" na ordem alfabética). O sufixo de plataforma (<c>net8.0-windows</c>) não conta.
    /// </summary>
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
