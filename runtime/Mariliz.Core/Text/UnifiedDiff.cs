using System.Text;

namespace Mariliz.Core.Text;

/// <summary>Diff unificado simples (LCS por linhas), suficiente para revisar arquivos de configuração.</summary>
public static class UnifiedDiff
{
    private const int MaxLines = 1500;
    private const int Context = 3;

    public static (string Diff, bool Truncated) Create(string path, string? before, string after)
    {
        var a = Split(before);
        var b = Split(after);

        var header = new StringBuilder();
        header.Append("--- ").AppendLine(before is null ? "/dev/null" : "a/" + path);
        header.Append("+++ b/").AppendLine(path);

        if (a.Length > MaxLines || b.Length > MaxLines)
        {
            header.AppendLine($"@@ arquivo grande: {a.Length} → {b.Length} linhas, diff omitido @@");
            return (header.ToString(), true);
        }

        var ops = Diff(a, b);
        var hunks = BuildHunks(ops);
        foreach (var hunk in hunks) header.Append(hunk);
        return (header.ToString(), false);
    }

    private static string[] Split(string? text) =>
        text is null || text.Length == 0 ? [] : text.Replace("\r\n", "\n").TrimEnd('\n').Split('\n');

    private enum Op { Keep, Add, Del }

    private static List<(Op Op, string Line)> Diff(string[] a, string[] b)
    {
        var lcs = new int[a.Length + 1, b.Length + 1];
        for (var i = a.Length - 1; i >= 0; i--)
            for (var j = b.Length - 1; j >= 0; j--)
                lcs[i, j] = a[i] == b[j] ? lcs[i + 1, j + 1] + 1 : Math.Max(lcs[i + 1, j], lcs[i, j + 1]);

        var ops = new List<(Op, string)>();
        int x = 0, y = 0;
        while (x < a.Length && y < b.Length)
        {
            if (a[x] == b[y]) { ops.Add((Op.Keep, a[x])); x++; y++; }
            else if (lcs[x + 1, y] >= lcs[x, y + 1]) { ops.Add((Op.Del, a[x])); x++; }
            else { ops.Add((Op.Add, b[y])); y++; }
        }
        while (x < a.Length) ops.Add((Op.Del, a[x++]));
        while (y < b.Length) ops.Add((Op.Add, b[y++]));
        return ops;
    }

    private static IEnumerable<string> BuildHunks(List<(Op Op, string Line)> ops)
    {
        var changed = ops.Select((o, i) => (o, i)).Where(t => t.o.Op != Op.Keep).Select(t => t.i).ToList();
        if (changed.Count == 0) yield break;

        var start = 0;
        while (start < changed.Count)
        {
            var end = start;
            while (end + 1 < changed.Count && changed[end + 1] - changed[end] <= Context * 2 + 1) end++;

            var from = Math.Max(0, changed[start] - Context);
            var to = Math.Min(ops.Count - 1, changed[end] + Context);

            int oldStart = 1, newStart = 1;
            for (var i = 0; i < from; i++)
            {
                if (ops[i].Op != Op.Add) oldStart++;
                if (ops[i].Op != Op.Del) newStart++;
            }
            var slice = ops.GetRange(from, to - from + 1);
            var oldCount = slice.Count(o => o.Op != Op.Add);
            var newCount = slice.Count(o => o.Op != Op.Del);

            var sb = new StringBuilder();
            sb.AppendLine($"@@ -{oldStart},{oldCount} +{newStart},{newCount} @@");
            foreach (var (op, line) in slice)
                sb.Append(op switch { Op.Add => '+', Op.Del => '-', _ => ' ' }).AppendLine(line);
            yield return sb.ToString();

            start = end + 1;
        }
    }
}
