using System.Text;
using System.Text.RegularExpressions;

namespace Mariliz.Application.Quality.Git;

/// <summary>
/// Lê a saída de <c>git diff</c> (com <c>--no-color</c>, sem driver externo nem conversão de texto). Não confia em nada além do
/// formato: um cabeçalho que não se entende é ignorado, nunca vira um arquivo inventado.
/// </summary>
public static partial class UnifiedDiffParser
{
    public const int MaxChangedLinesPerFile = 3000;

    public static IReadOnlyList<ChangedFile> Parse(string patch)
    {
        var files = new List<ChangedFile>();
        var lines = patch.Split('\n');
        var index = 0;
        while (index < lines.Length)
        {
            if (!lines[index].StartsWith("diff --git ", StringComparison.Ordinal)) { index++; continue; }
            var start = index++;
            while (index < lines.Length && !lines[index].StartsWith("diff --git ", StringComparison.Ordinal)) index++;
            if (ParseFile(lines, start, index) is { } file) files.Add(file);
        }
        return files;
    }

    private static ChangedFile? ParseFile(string[] lines, int start, int end)
    {
        string? oldPath = null, newPath = null, renameFrom = null, renameTo = null;
        var isNew = false;
        var isDeleted = false;
        var binary = false;
        var firstHunk = end;
        for (var i = start + 1; i < end; i++)
        {
            var line = lines[i];
            if (line.StartsWith("@@", StringComparison.Ordinal)) { firstHunk = i; break; }
            if (line.StartsWith("new file mode", StringComparison.Ordinal)) isNew = true;
            else if (line.StartsWith("deleted file mode", StringComparison.Ordinal)) isDeleted = true;
            else if (line.StartsWith("rename from ", StringComparison.Ordinal)) renameFrom = Unquote(line["rename from ".Length..]);
            else if (line.StartsWith("rename to ", StringComparison.Ordinal)) renameTo = Unquote(line["rename to ".Length..]);
            else if (line.StartsWith("--- ", StringComparison.Ordinal)) oldPath = StripPrefix(line[4..], "a/");
            else if (line.StartsWith("+++ ", StringComparison.Ordinal)) newPath = StripPrefix(line[4..], "b/");
            else if (line.StartsWith("Binary files ", StringComparison.Ordinal) || line.StartsWith("GIT binary patch", StringComparison.Ordinal)) binary = true;
        }

        var path = renameTo ?? newPath ?? oldPath ?? FromHeader(lines[start]);
        if (path is null) return null;
        var status = renameTo is not null ? FileStatuses.Renamed : isNew ? FileStatuses.Added : isDeleted ? FileStatuses.Deleted : FileStatuses.Modified;
        var previous = status == FileStatuses.Renamed ? renameFrom : null;
        if (status == FileStatuses.Deleted && oldPath is not null) path = oldPath;

        var hunks = new List<DiffHunk>();
        var added = 0;
        var removed = 0;
        var i2 = firstHunk;
        while (i2 < end)
        {
            var match = HunkHeader().Match(lines[i2]);
            if (!match.Success) { i2++; continue; }
            var oldNo = int.Parse(match.Groups["old"].Value);
            var newNo = int.Parse(match.Groups["new"].Value);
            var hunkLines = new List<DiffLine>();
            i2++;
            for (; i2 < end && !lines[i2].StartsWith("@@", StringComparison.Ordinal); i2++)
            {
                var text = lines[i2];
                if (text.Length > 0 && text[^1] == '\r') text = text[..^1];
                if (text.StartsWith('\\')) continue;
                if (text.StartsWith('+')) { hunkLines.Add(new('+', null, newNo++, text[1..])); added++; }
                else if (text.StartsWith('-')) { hunkLines.Add(new('-', oldNo++, null, text[1..])); removed++; }
                else if (text.StartsWith(' ') || text.Length == 0) { hunkLines.Add(new(' ', oldNo++, newNo++, text.Length == 0 ? "" : text[1..])); }
            }
            // O último "\n" do patch gera uma linha vazia que não é do arquivo.
            if (i2 >= end && hunkLines.Count > 0 && hunkLines[^1] is { Kind: ' ', Text: "" } && end == lines.Length) hunkLines.RemoveAt(hunkLines.Count - 1);
            hunks.Add(new DiffHunk(int.Parse(match.Groups["new"].Value), match.Groups["symbol"].Value.Trim() is { Length: > 0 } symbol ? symbol : null, hunkLines));
        }

        string? skipped = null;
        if (binary) skipped = SkipReasons.Binary;
        else if (added + removed > MaxChangedLinesPerFile)
        {
            skipped = SkipReasons.TooLarge;
            hunks = [];
        }
        return new ChangedFile(path, previous, status, added, removed, hunks, skipped);
    }

    // "diff --git a/P b/P": sem renomeação os dois lados são iguais, então o ponto médio separa o caminho mesmo com espaços.
    private static string? FromHeader(string header)
    {
        var rest = header["diff --git ".Length..];
        if (rest.StartsWith('"')) return null;
        var length = (rest.Length - 5) / 2;
        if (length <= 0 || rest.Length != 2 * length + 5) return null;
        var left = rest[..(length + 2)];
        var right = rest[(length + 3)..];
        return left.StartsWith("a/", StringComparison.Ordinal) && right.StartsWith("b/", StringComparison.Ordinal) && left[2..] == right[2..] ? left[2..] : null;
    }

    private static string? StripPrefix(string value, string prefix)
    {
        // O git acrescenta um tab depois de nomes com espaço.
        value = value.TrimEnd('\t');
        if (value == "/dev/null") return null;
        value = Unquote(value);
        return value.StartsWith(prefix, StringComparison.Ordinal) ? value[prefix.Length..] : value;
    }

    private static string Unquote(string value)
    {
        value = value.TrimEnd('\t');
        if (value.Length < 2 || value[0] != '"' || value[^1] != '"') return value;
        var builder = new StringBuilder();
        for (var i = 1; i < value.Length - 1; i++)
        {
            if (value[i] != '\\' || i + 1 >= value.Length - 1) { builder.Append(value[i]); continue; }
            i++;
            builder.Append(value[i] switch { 't' => '\t', 'n' => '\n', '"' => '"', '\\' => '\\', var other => other });
        }
        return builder.ToString();
    }

    [GeneratedRegex(@"^@@ -(?<old>\d+)(?:,\d+)? \+(?<new>\d+)(?:,\d+)? @@(?<symbol>.*)$")]
    private static partial Regex HunkHeader();
}
