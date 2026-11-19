using System.Text.RegularExpressions;
using Mariliz.Application.Ports;
using Mariliz.Core.Discovery;
using Mariliz.Core.Text;

namespace Mariliz.Application.Discovery;

// MV-4: o runtime que um gerenciador de versões fixa para a pasta e as de baixo (asdf .tool-versions, mise.toml, SDKMAN .sdkmanrc).
// O arquivo mais próximo vence; valores sem número (lts, latest, system) não viram versão.
internal static partial class VersionManagers
{
    private static readonly Dictionary<string, string[]> Aliases = new(StringComparer.Ordinal)
    {
        [Technologies.Node.Id] = ["nodejs", "node"],
        [Technologies.Python.Id] = ["python"],
        [Technologies.Ruby.Id] = ["ruby"],
        [Technologies.Go.Id] = ["golang", "go"],
        [Technologies.Java.Id] = ["java"],
        [Technologies.Php.Id] = ["php"],
        [Technologies.Dotnet.Id] = ["dotnet", "dotnet-core"],
        [Technologies.Rust.Id] = ["rust"]
    };

    internal static (string Version, Evidence Evidence)? Find(IProjectFiles files, IReadOnlyList<string> all, string root, string technologyId)
    {
        if (!Aliases.TryGetValue(technologyId, out var names)) return null;
        foreach (var folder in RuntimeDetector.SelfAndAncestors(root))
        {
            if (Read(files, all, ProjectPath.Combine(folder, ".tool-versions")) is { } asdf
                && ToolVersions(asdf.Text, names) is { } fromAsdf)
                return (fromAsdf.Version, new Evidence(asdf.Path, fromAsdf.Line));
            foreach (var mise in new[] { "mise.toml", ".mise.toml" })
                if (Read(files, all, ProjectPath.Combine(folder, mise)) is { } file && MiseTools(file.Text, names) is { } fromMise)
                    return (fromMise.Version, new Evidence(file.Path, fromMise.Line));
            if (technologyId == Technologies.Java.Id && Read(files, all, ProjectPath.Combine(folder, ".sdkmanrc")) is { } sdkman
                && SdkmanJava().Match(sdkman.Text) is { Success: true } java && Number(java.Groups[1].Value) is { } javaVersion)
                return (javaVersion, new Evidence(sdkman.Path, java.Value.Trim()));
        }
        return null;
    }

    private static (string Path, string Text)? Read(IProjectFiles files, IReadOnlyList<string> all, string path) =>
        all.Contains(path) && files.ReadText(path) is { } text ? (path, text) : null;

    // "nodejs 20.11.0 18.19.0": a primeira versão da linha é a que vale.
    private static (string Version, string Line)? ToolVersions(string text, string[] names)
    {
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Split('#')[0].Trim();
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2 && names.Contains(parts[0], StringComparer.Ordinal) && Number(parts[1]) is { } version)
                return (version, line);
        }
        return null;
    }

    // [tools] node = "20", python = ["3.12", "3.11"] ou ruby = { version = "3.3" }.
    private static (string Version, string Line)? MiseTools(string text, string[] names)
    {
        var start = text.IndexOf("[tools]", StringComparison.Ordinal);
        if (start < 0) return null;
        var body = text[(start + "[tools]".Length)..];
        var next = Regex.Match(body, @"(?m)^\s*\[");
        if (next.Success) body = body[..next.Index];
        foreach (var raw in body.Split('\n'))
        {
            var line = raw.Split('#')[0].Trim();
            var match = MiseLine().Match(line);
            if (match.Success && names.Contains(match.Groups["name"].Value, StringComparer.Ordinal)
                && Quoted().Match(match.Groups["value"].Value) is { Success: true } first && Number(first.Groups[1].Value) is { } version)
                return (version, line);
        }
        return null;
    }

    private static string? Number(string value) => VersionNumber().Match(value) is { Success: true } match ? match.Value : null;

    [GeneratedRegex(@"^[""']?(?<name>[\w\-]+)[""']?\s*=\s*(?<value>.+)$")]
    private static partial Regex MiseLine();

    [GeneratedRegex(@"[""']([^""']+)[""']")]
    private static partial Regex Quoted();

    [GeneratedRegex(@"(?m)^\s*java\s*=\s*(\S+)")]
    private static partial Regex SdkmanJava();

    [GeneratedRegex(@"\d+(\.\d+){0,2}")]
    private static partial Regex VersionNumber();
}
