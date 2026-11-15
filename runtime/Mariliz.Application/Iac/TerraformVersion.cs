using System.Globalization;
using System.Text.RegularExpressions;

namespace Mariliz.Application.Iac;

// Só o que o Terraform publica: maior.menor.correção com sufixo opcional (-rc1, -beta2). Sufixo vem antes da versão final.
public sealed partial record TerraformVersion(int Major, int Minor, int Patch, string? Prerelease) : IComparable<TerraformVersion>
{
    public static TerraformVersion? TryParse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var match = Pattern().Match(text.Trim().TrimStart('v'));
        if (!match.Success) return null;
        int Part(int group) => match.Groups[group].Success ? int.Parse(match.Groups[group].Value, CultureInfo.InvariantCulture) : 0;
        return new TerraformVersion(Part(1), Part(2), Part(3), match.Groups[4].Success ? match.Groups[4].Value : null);
    }

    public static TerraformVersion Parse(string text) =>
        TryParse(text) ?? throw new FormatException($"Versão do Terraform fora do formato: {text}.");

    public bool IsPrerelease => Prerelease is not null;

    public int CompareTo(TerraformVersion? other)
    {
        if (other is null) return 1;
        var core = (Major, Minor, Patch).CompareTo((other.Major, other.Minor, other.Patch));
        if (core != 0) return core;
        return (Prerelease, other.Prerelease) switch
        {
            (null, null) => 0,
            (null, _) => 1,
            (_, null) => -1,
            _ => string.CompareOrdinal(Prerelease, other.Prerelease)
        };
    }

    public static bool operator <(TerraformVersion left, TerraformVersion right) => left.CompareTo(right) < 0;
    public static bool operator >(TerraformVersion left, TerraformVersion right) => left.CompareTo(right) > 0;
    public static bool operator <=(TerraformVersion left, TerraformVersion right) => left.CompareTo(right) <= 0;
    public static bool operator >=(TerraformVersion left, TerraformVersion right) => left.CompareTo(right) >= 0;

    public override string ToString() => $"{Major}.{Minor}.{Patch}" + (Prerelease is null ? "" : "-" + Prerelease);

    [GeneratedRegex(@"^(\d+)(?:\.(\d+))?(?:\.(\d+))?(?:-([0-9A-Za-z.]+))?$")]
    private static partial Regex Pattern();
}
