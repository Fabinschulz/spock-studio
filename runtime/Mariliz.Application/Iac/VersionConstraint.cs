namespace Mariliz.Application.Iac;

// A sintaxe de required_version: condições separadas por vírgula, todas valendo (">= 1.11, < 2.0", "~> 1.9", "= 1.16.5").
public sealed class VersionConstraint
{
    private readonly IReadOnlyList<(string Operator, IacVersion Version, int Parts)> _conditions;

    private VersionConstraint(string text, IReadOnlyList<(string, IacVersion, int)> conditions)
    {
        Text = text;
        _conditions = conditions;
    }

    public string Text { get; }

    public static VersionConstraint? TryParse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var conditions = new List<(string, IacVersion, int)>();
        foreach (var raw in text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var op = new[] { ">=", "<=", "!=", "~>", ">", "<", "=" }.FirstOrDefault(o => raw.StartsWith(o, StringComparison.Ordinal)) ?? "=";
            var versionText = raw[(raw.StartsWith(op, StringComparison.Ordinal) ? op.Length : 0)..].Trim();
            if (IacVersion.TryParse(versionText) is not { } version) return null;
            conditions.Add((op, version, versionText.Split('-')[0].Split('.').Length));
        }
        return conditions.Count == 0 ? null : new VersionConstraint(text.Trim(), conditions);
    }

    public bool IsSatisfiedBy(IacVersion version) => _conditions.All(c => Holds(c.Operator, c.Version, c.Parts, version));

    // ~> trava todas as partes menos a última escrita: "~> 1.9" aceita 1.x a partir de 1.9; "~> 1.9.2" aceita 1.9.x a partir de 1.9.2.
    private static bool Holds(string op, IacVersion bound, int parts, IacVersion version) => op switch
    {
        ">=" => version >= bound,
        "<=" => version <= bound,
        ">" => version > bound,
        "<" => version < bound,
        "!=" => version.CompareTo(bound) != 0,
        "~>" => version >= bound && (parts <= 2 ? version.Major == bound.Major : version.Major == bound.Major && version.Minor == bound.Minor),
        _ => version.CompareTo(bound) == 0
    };

    public override string ToString() => Text;
}
