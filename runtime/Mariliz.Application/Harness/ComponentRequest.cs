using System.Text.RegularExpressions;
using Mariliz.Core.Plan;

namespace Mariliz.Application.Harness;

/// <summary>
/// Pedido feito na tela para dispensar componentes do Harness ou voltar a aplicá-los. Não muda nada sozinho: vira uma
/// edição de <c>components.exclude</c> em <c>mariliz.yaml</c>, que entra no plano como qualquer mudança, com a diferença
/// e a aprovação da pessoa, e pode ser revertida como as demais.
/// </summary>
public static partial class ComponentRequest
{
    /// <summary>Agentes, skills, regras (inclusive as do projeto) e o hook. A estrutura de especificação e a declaração ficam de fora.</summary>
    public static bool IsManageable(string id) =>
        Catalog.Find(id) is { Kind: ComponentKind.Agent or ComponentKind.Skill or ComponentKind.Rule or ComponentKind.Hook }
        || CustomRuleId().IsMatch(id);

    /// <summary>A lista de exclusão depois do pedido: mantém a ordem declarada, tira o que volta e acrescenta o que sai.</summary>
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
