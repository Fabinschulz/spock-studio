using System.Text.RegularExpressions;

namespace Mariliz.Application.Compatibility;

// A versão como os arquivos do projeto a escrevem ("net8.0", "^18.2.0", "1.8", ">=3.10") vira a linha do catálogo.
public static partial class VersionCycles
{
    public static string? Of(CatalogTechnology technology, string? version)
    {
        if (string.IsNullOrWhiteSpace(version)) return null;
        var text = version.Trim().ToLowerInvariant();
        if (technology.Id == "dotnet")
        {
            // .NET Framework (net48) e .NET Standard não são linhas do .NET moderno.
            if (text.StartsWith("netstandard", StringComparison.Ordinal)) return null;
            if (text.StartsWith("netcoreapp", StringComparison.Ordinal)) text = text["netcoreapp".Length..];
            else if (text.StartsWith("net", StringComparison.Ordinal))
            {
                text = text[3..].Split('-')[0];
                if (!text.Contains('.')) return null;
            }
        }

        var numbers = Numbers().Matches(text).Select(match => match.Value).ToList();
        if (numbers.Count == 0) return null;
        // Java 8 e anteriores se escrevem 1.8.
        if (technology.Id == "java" && numbers[0] == "1" && numbers.Count > 1) numbers.RemoveAt(0);
        return technology.Cycle switch
        {
            "major" => Trim(numbers[0]),
            _ when numbers.Count >= 2 => $"{Trim(numbers[0])}.{Trim(numbers[1])}",
            // .NET escrito só com o número maior (global.json não faz isso, mas um engines ou uma nota pode): 8 vale 8.0.
            _ when technology.Id == "dotnet" => $"{Trim(numbers[0])}.0",
            _ => null
        };
    }

    public static int Compare(string left, string right)
    {
        var a = left.Split('.').Select(int.Parse).ToArray();
        var b = right.Split('.').Select(int.Parse).ToArray();
        for (var i = 0; i < Math.Max(a.Length, b.Length); i++)
        {
            var difference = (i < a.Length ? a[i] : 0).CompareTo(i < b.Length ? b[i] : 0);
            if (difference != 0) return difference;
        }
        return 0;
    }

    private static string Trim(string number) => number.TrimStart('0') is { Length: > 0 } trimmed ? trimmed : "0";

    [GeneratedRegex(@"\d+")]
    private static partial Regex Numbers();
}
