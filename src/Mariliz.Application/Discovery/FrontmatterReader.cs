using System.Text.RegularExpressions;

namespace Mariliz.Application.Discovery;

/// <summary>Leitura mínima de frontmatter YAML de arquivos Markdown (campos de primeiro nível, valores escalares).</summary>
public static class FrontmatterReader
{
    public static Dictionary<string, string>? Read(string markdown)
    {
        var text = markdown.Replace("\r\n", "\n");
        if (!text.StartsWith("---\n", StringComparison.Ordinal)) return null;
        var end = text.IndexOf("\n---", 4, StringComparison.Ordinal);
        if (end < 0) return null;

        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var line in text[4..end].Split('\n'))
        {
            var m = Regex.Match(line, @"^([A-Za-z][\w-]*):\s*(.*)$");
            if (m.Success) result[m.Groups[1].Value] = m.Groups[2].Value.Trim();
        }
        return result;
    }

    public static string? Issue(string? markdown, string expectedName, bool requireTools)
    {
        if (markdown is null) return "Arquivo ilegível.";
        var fm = FrontmatterReader.Read(markdown);
        if (fm is null) return "Sem frontmatter.";
        if (!fm.TryGetValue("name", out var name) || string.IsNullOrWhiteSpace(name)) return "Frontmatter sem 'name'.";
        if (!string.Equals(name, expectedName, StringComparison.Ordinal)) return $"'name' ({name}) difere do nome do arquivo ({expectedName}).";
        if (!fm.TryGetValue("description", out var d) || string.IsNullOrWhiteSpace(d)) return "Frontmatter sem 'description'.";
        if (requireTools && !fm.ContainsKey("tools")) return "Frontmatter sem 'tools' explícito.";
        return null;
    }
}
