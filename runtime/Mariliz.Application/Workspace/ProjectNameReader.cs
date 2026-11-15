using System.Text.Json;
using System.Text.RegularExpressions;

namespace Mariliz.Application.Workspace;

// Só muda o nome exibido: o id continua vindo do caminho, para nada do que já foi gravado mudar de projeto.
internal static partial class ProjectNameReader
{
    private const int MaxLength = 80;

    internal static string? Read(IReadOnlyList<string> rootFiles, Func<string, string?> readText)
    {
        var files = new HashSet<string>(rootFiles, StringComparer.OrdinalIgnoreCase);
        string? Text(string name) => files.Contains(name) ? readText(name) : null;

        return Valid(PackageJson(Text("package.json")))
            ?? Valid(TomlName(Text("pyproject.toml"), "project", "tool.poetry"))
            ?? Valid(TomlName(Text("Cargo.toml"), "package"))
            ?? Valid(GoModule(Text("go.mod")))
            ?? Valid(Solution(rootFiles));
    }

    private static string? PackageJson(string? text)
    {
        if (text is null) return null;
        try
        {
            using var document = JsonDocument.Parse(text);
            return document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("name", out var name)
                && name.ValueKind == JsonValueKind.String
                ? name.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? TomlName(string? text, params string[] sections)
    {
        if (text is null) return null;
        foreach (var section in sections)
        {
            var inside = false;
            foreach (var raw in text.Split('\n'))
            {
                var line = raw.Trim();
                if (line.StartsWith('['))
                {
                    inside = line == $"[{section}]";
                    continue;
                }
                if (inside && TomlNameLine().Match(line) is { Success: true } match) return match.Groups[1].Value;
            }
        }
        return null;
    }

    private static string? GoModule(string? text)
    {
        if (text is null || GoModuleLine().Match(text) is not { Success: true } match) return null;
        var module = match.Groups[1].Value.TrimEnd('/');
        return module[(module.LastIndexOf('/') + 1)..];
    }

    // Com mais de uma solução na raiz não há um nome que represente o projeto.
    private static string? Solution(IReadOnlyList<string> rootFiles) =>
        rootFiles.Where(name => Path.GetExtension(name).ToLowerInvariant() is ".sln" or ".slnx").ToList() is [var only]
            ? Path.GetFileNameWithoutExtension(only)
            : null;

    private static string? Valid(string? name)
    {
        var trimmed = name?.Trim();
        if (string.IsNullOrEmpty(trimmed) || trimmed.Length > MaxLength || trimmed.Any(char.IsControl)) return null;
        return trimmed;
    }

    [GeneratedRegex("""^name\s*=\s*["']([^"']+)["']""")]
    private static partial Regex TomlNameLine();

    [GeneratedRegex(@"^\s*module\s+""?([^\s""]+)""?", RegexOptions.Multiline)]
    private static partial Regex GoModuleLine();
}
