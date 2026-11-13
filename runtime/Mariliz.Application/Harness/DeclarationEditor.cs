using YamlDotNet.Core;

namespace Mariliz.Application.Harness;

// Edita linha a linha, sem reescrever o arquivo: comentários, ordem e o restante ficam como a pessoa deixou. Formato não reconhecido, ou resultado que não lê exatamente como o esperado, não edita e devolve null.
public static class DeclarationEditor
{
    public static string? WithExcluded(string text, IReadOnlyList<string> exclude)
    {
        var lines = text.Replace("\r\n", "\n").Split('\n').ToList();
        var edited = Edit(lines, exclude);
        if (edited is null) return null;

        var result = string.Join("\n", edited);
        return ReadsAsExpected(text, result, exclude) ? result : null;
    }

    private static List<string>? Edit(List<string> lines, IReadOnlyList<string> exclude)
    {
        var components = lines.FindIndex(line => IsKey(line, "components", indent: 0));
        if (components < 0)
        {
            var appended = new List<string>(lines);
            var at = appended.Count > 0 && appended[^1].Length == 0 ? appended.Count - 1 : appended.Count;
            appended.InsertRange(at, ["components:", .. Block("  ", exclude)]);
            return appended;
        }
        if (Value(lines[components]).Length > 0) return null; // components: {} ou na mesma linha: formato não reconhecido.

        var end = components + 1;
        while (end < lines.Count && (IsBlankOrComment(lines[end]) || Indent(lines[end]) > 0)) end++;
        while (end > components + 1 && IsBlankOrComment(lines[end - 1])) end--;

        var excludeLine = -1;
        for (var i = components + 1; i < end; i++)
            if (!IsBlankOrComment(lines[i]) && lines[i].TrimStart().StartsWith("exclude:", StringComparison.Ordinal)) { excludeLine = i; break; }

        var result = new List<string>(lines);
        if (excludeLine < 0)
        {
            var childIndent = Enumerable.Range(components + 1, end - components - 1)
                .Where(i => !IsBlankOrComment(lines[i])).Select(i => new string(' ', Indent(lines[i]))).FirstOrDefault() ?? "  ";
            result.InsertRange(components + 1, Block(childIndent, exclude));
            return result;
        }

        var indent = new string(' ', Indent(lines[excludeLine]));
        var value = Value(lines[excludeLine]);
        var itemsEnd = excludeLine + 1;
        if (value.Length == 0)
        {
            // Lista em bloco: só itens simples ("- id"), na indentação da chave ou mais funda.
            while (itemsEnd < end && (lines[itemsEnd].Trim().Length == 0 || IsItem(lines[itemsEnd], indent.Length))) itemsEnd++;
            if (itemsEnd < end && Indent(lines[itemsEnd]) > indent.Length) return null;
        }
        else if (!(value.StartsWith('[') && value.EndsWith(']'))) return null;

        result.RemoveRange(excludeLine, itemsEnd - excludeLine);
        result.InsertRange(excludeLine, Block(indent, exclude));
        return result;
    }

    private static IEnumerable<string> Block(string indent, IReadOnlyList<string> exclude) =>
        exclude.Count == 0 ? [$"{indent}exclude: []"] : [$"{indent}exclude:", .. exclude.Select(id => $"{indent}  - {id}")];

    private static bool ReadsAsExpected(string original, string edited, IReadOnlyList<string> exclude)
    {
        try
        {
            var before = YamlFiles.Reader.Deserialize<DeclaredConfig>(original) ?? new DeclaredConfig();
            var after = YamlFiles.Reader.Deserialize<DeclaredConfig>(edited) ?? new DeclaredConfig();
            if (!(after.Components?.Exclude ?? []).SequenceEqual(exclude, StringComparer.Ordinal)) return false;

            // Fora a lista, nada mais pode mudar de sentido. "components:" sem nada embaixo lê como nulo.
            before.Components = new DeclaredConfig.ComponentsSection();
            after.Components = new DeclaredConfig.ComponentsSection();
            return YamlFiles.Writer.Serialize(before) == YamlFiles.Writer.Serialize(after);
        }
        catch (YamlException)
        {
            return false;
        }
    }

    private static bool IsKey(string line, string key, int indent) =>
        Indent(line) == indent && line.TrimStart().StartsWith(key + ":", StringComparison.Ordinal);

    private static string Value(string line)
    {
        var value = line[(line.IndexOf(':') + 1)..];
        var comment = value.IndexOf(" #", StringComparison.Ordinal);
        return (comment >= 0 ? value[..comment] : value).Trim();
    }

    private static bool IsItem(string line, int minIndent)
    {
        var trimmed = line.TrimStart();
        if (Indent(line) < minIndent || !trimmed.StartsWith("- ", StringComparison.Ordinal)) return false;
        var item = trimmed[2..].Trim();
        return item.Length > 0 && item.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or '_');
    }

    private static bool IsBlankOrComment(string line) => line.Trim().Length == 0 || line.TrimStart().StartsWith('#');

    private static int Indent(string line) => line.Length - line.TrimStart(' ').Length;
}
