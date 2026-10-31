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

        var version = frameworks.Count == 0 ? null : frameworks.OrderByDescending(f => f, StringComparer.OrdinalIgnoreCase).First();
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
}
