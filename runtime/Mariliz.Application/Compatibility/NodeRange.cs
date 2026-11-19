using System.Text.RegularExpressions;

namespace Mariliz.Application.Compatibility;

public enum RangeVerdict { Satisfied, Unsatisfied, Unknown }

// Faixa de versão no formato do npm (engines.node): "^20.19.0 || >=22.12.0", ">= 20.0.0", "~18.17", "16 - 18".
// A versão do projeto pode vir incompleta (".nvmrc" com "20" é qualquer 20.x): ela vira um intervalo, e a resposta só é
// Satisfied ou Unsatisfied quando o intervalo inteiro cai dentro ou fora da faixa. O resto é Unknown.
public static partial class NodeRange
{
    private readonly record struct Bound(long Value, bool Inclusive);

    private readonly record struct Interval(Bound Low, Bound High);

    private const long Part = 1_000_000;
    private const long Infinite = long.MaxValue;

    public static RangeVerdict Evaluate(string range, string version)
    {
        if (Parse(version) is not { } project) return RangeVerdict.Unknown;
        List<Interval> sets;
        try { sets = range.Split("||").Select(Set).ToList(); }
        catch (FormatException) { return RangeVerdict.Unknown; }
        if (sets.Any(set => Contains(set, project))) return RangeVerdict.Satisfied;
        return sets.Any(set => Overlaps(set, project)) ? RangeVerdict.Unknown : RangeVerdict.Unsatisfied;
    }

    private static Interval Set(string text)
    {
        var low = new Bound(0, true);
        var high = new Bound(Infinite, false);
        var normalized = Spaces().Replace(text.Trim(), "$1");
        if (normalized.Length == 0 || normalized is "*" or "x" or "X") return new Interval(low, high);
        if (Hyphen().Match(normalized) is { Success: true } hyphen)
        {
            var from = Parse(hyphen.Groups[1].Value) ?? throw new FormatException();
            var to = Parse(hyphen.Groups[2].Value) ?? throw new FormatException();
            return new Interval(from.Low, to.High);
        }

        foreach (var comparator in normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var match = Comparator().Match(comparator);
            if (!match.Success) throw new FormatException();
            var version = Parse(match.Groups[2].Value) ?? throw new FormatException();
            var (from, to) = (version.Low, version.High);
            switch (match.Groups[1].Value)
            {
                case ">=": low = Max(low, from); break;
                case ">": low = Max(low, new Bound(to.Value, !to.Inclusive)); break;
                case "<=": high = Min(high, to); break;
                case "<": high = Min(high, new Bound(from.Value, !from.Inclusive)); break;
                case "^": low = Max(low, from); high = Min(high, Caret(match.Groups[2].Value)); break;
                case "~": low = Max(low, from); high = Min(high, Tilde(match.Groups[2].Value)); break;
                default: low = Max(low, from); high = Min(high, to); break;
            }
        }
        return new Interval(low, high);
    }

    // A versão escrita vira o intervalo que ela representa: "20" é [20.0.0, 21.0.0), "20.19" é [20.19.0, 20.20.0).
    private static Interval? Parse(string text)
    {
        var match = Version().Match(text.Trim());
        if (!match.Success) return null;
        var parts = match.Groups[1].Value.Split('.').Where(part => part is not ("x" or "X" or "*")).Select(long.Parse).ToArray();
        if (parts.Length == 0) return null;
        var major = parts[0];
        var minor = parts.Length > 1 ? parts[1] : 0;
        var patch = parts.Length > 2 ? parts[2] : 0;
        var low = Encode(major, minor, patch);
        var high = parts.Length switch
        {
            1 => new Bound(Encode(major + 1, 0, 0), false),
            2 => new Bound(Encode(major, minor + 1, 0), false),
            _ => new Bound(low, true)
        };
        return new Interval(new Bound(low, true), high);
    }

    private static Bound Caret(string text)
    {
        var parts = Version().Match(text.Trim()).Groups[1].Value.Split('.').Where(p => p is not ("x" or "X" or "*")).Select(long.Parse).ToArray();
        long major = parts[0], minor = parts.Length > 1 ? parts[1] : 0;
        if (major > 0 || parts.Length == 1) return new Bound(Encode(major + 1, 0, 0), false);
        return minor > 0 || parts.Length == 2 ? new Bound(Encode(0, minor + 1, 0), false) : new Bound(Encode(0, 0, parts[2] + 1), false);
    }

    private static Bound Tilde(string text)
    {
        var parts = Version().Match(text.Trim()).Groups[1].Value.Split('.').Where(p => p is not ("x" or "X" or "*")).Select(long.Parse).ToArray();
        return parts.Length == 1 ? new Bound(Encode(parts[0] + 1, 0, 0), false) : new Bound(Encode(parts[0], parts[1] + 1, 0), false);
    }

    private static long Encode(long major, long minor, long patch) =>
        Math.Min(major, 9_000) * Part * Part + Math.Min(minor, Part - 1) * Part + Math.Min(patch, Part - 1);

    private static bool Contains(Interval set, Interval project) =>
        Below(set.Low, project.Low) && Above(set.High, project.High);

    private static bool Overlaps(Interval set, Interval project) =>
        Before(project.Low, set.High) && Before(set.Low, project.High);

    // A borda inferior do conjunto não passa da do projeto.
    private static bool Below(Bound set, Bound project) => set.Value < project.Value || (set.Value == project.Value && (set.Inclusive || !project.Inclusive));

    // A borda superior do conjunto não fica aquém da do projeto.
    private static bool Above(Bound set, Bound project) => set.Value > project.Value || (set.Value == project.Value && (set.Inclusive || !project.Inclusive));

    // Uma borda inferior antes de uma superior: há ponto em comum.
    private static bool Before(Bound low, Bound high) => low.Value < high.Value || (low.Value == high.Value && low.Inclusive && high.Inclusive);

    private static Bound Max(Bound a, Bound b) => a.Value > b.Value || (a.Value == b.Value && !a.Inclusive) ? a : b;

    private static Bound Min(Bound a, Bound b) => a.Value < b.Value || (a.Value == b.Value && !a.Inclusive) ? a : b;

    [GeneratedRegex(@"(>=|<=|>|<|=|\^|~)\s+")]
    private static partial Regex Spaces();

    [GeneratedRegex(@"^(\S+)\s+-\s+(\S+)$")]
    private static partial Regex Hyphen();

    [GeneratedRegex(@"^(>=|<=|>|<|=|\^|~)?v?(\d+(?:\.(?:\d+|[xX*])){0,2})(?:[-+].*)?$")]
    private static partial Regex Comparator();

    [GeneratedRegex(@"^[v=]?(\d+(?:\.(?:\d+|[xX*])){0,2})(?:[-+].*)?$")]
    private static partial Regex Version();
}
