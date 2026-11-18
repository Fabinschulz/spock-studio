using System.Text.RegularExpressions;
using Mariliz.Core.Plan;

namespace Mariliz.Application.Harness;

// Não muda nada sozinho: vira uma edição de components.exclude em mariliz.yaml, que entra no plano com diferença e aprovação da pessoa e pode ser revertida como as demais.
public static partial class ComponentRequest
{
    // A estrutura de especificação e a declaração ficam de fora.
    public static bool IsManageable(string id) =>
        Catalog.Find(id) is { Kind: ComponentKind.Agent or ComponentKind.Skill or ComponentKind.Rule or ComponentKind.Hook or ComponentKind.Tool }
        || CustomRuleId().IsMatch(id);

    public static List<string> Effective(IReadOnlyList<string> declared, IReadOnlyList<string> exclude, IReadOnlyList<string> include)
    {
        var result = declared.Where(id => !include.Contains(id, StringComparer.Ordinal)).ToList();
        foreach (var id in exclude)
            if (!result.Contains(id, StringComparer.Ordinal)) result.Add(id);
        return result;
    }

    [GeneratedRegex("^mariliz\\.rule\\.custom\\.[a-z][a-z0-9-]{2,40}$")]
    private static partial Regex CustomRuleId();
}
