using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Mariliz.Application.Workflows;
using static Mariliz.Application.Ideas.IdeaChecks;

namespace Mariliz.Application.Ideas;

// A escolha de stack segue o §10 do catálogo da Organização (W4-3): cada fator do projeto cita uma linha das tabelas e
// favorece uma opção dela, e a opção escolhida é a que os fatores citados favorecem. Assim, os mesmos fatores levam à mesma
// escolha, e quem decide diferente precisa escrever a exceção.
internal static partial class StackChecks
{
    public const string BackendQuestion = "10.4";
    public const string FrontendQuestion = "10.1";

    private static readonly Dictionary<string, string> QuestionOfArea = new(StringComparer.OrdinalIgnoreCase)
    {
        ["backend"] = BackendQuestion,
        ["frontend"] = FrontendQuestion
    };

    public static IReadOnlyList<IdeaCheck> For(JsonObject architecture, JsonObject spec) =>
        For(architecture, spec, OrganizationReferences.DecisionCriteria);

    public static IReadOnlyList<IdeaCheck> For(JsonObject architecture, JsonObject spec, IReadOnlyList<DecisionCriterion> criteria)
    {
        var byId = criteria.ToDictionary(criterion => criterion.Id, StringComparer.Ordinal);
        var questions = criteria.GroupBy(criterion => criterion.Question)
            .ToDictionary(group => group.Key, group => group.SelectMany(criterion => criterion.Options.Select(option => option.Name)).ToHashSet(StringComparer.Ordinal));
        var factors = Objects(architecture, "factors").Select(item => Factor.From(item, byId)).ToList();
        var known = factors.Where(factor => factor.Valid).GroupBy(factor => factor.Id).Where(group => group.Count() == 1)
            .ToDictionary(group => group.Key, group => group.Single(), StringComparer.Ordinal);
        var repeated = factors.GroupBy(factor => factor.Id).Where(group => group.Count() > 1).Select(group => group.Key).ToHashSet(StringComparer.Ordinal);
        var badFactors = factors.Where(factor => !factor.Valid || repeated.Contains(factor.Id)).Select(factor => factor.Id).Distinct().ToList();

        var requirements = Objects(spec, "requirements").Concat(Objects(spec, "nonFunctional"))
            .Select(item => Text(item, "id")).Where(id => id.Length > 0).ToHashSet(StringComparer.Ordinal);
        var stack = Objects(architecture, "stack").Select((item, index) => Choice.From(item, index)).ToList();

        var uncited = new List<string>();
        var unanswered = new List<string>();
        var inconsistent = new List<string>();
        var withoutTradeoff = new List<string>();
        foreach (var choice in stack)
        {
            if (!Cites(choice, questions, known, requirements)) uncited.Add(choice.Name);
            if (QuestionOfArea.TryGetValue(choice.Area, out var expected) && choice.Question != expected) unanswered.Add($"{choice.Name}: {expected}");
            if (choice.Question.Length > 0 && !Consistent(choice, known)) inconsistent.Add(choice.Name);
            if (!choice.TradeoffsStated) withoutTradeoff.Add(choice.Name);
        }

        return
        [
            Check("factors", "Cada fator tem id único (F1, F2...), descreve um fato do projeto e cita um critério do §10 do catálogo, favorecendo uma opção que o critério tem",
                badFactors.Count == 0, badFactors),
            Check("criteria", "Cada escolha da stack cita o critério: a pergunta do §10, a opção e os fatores dela, ou requisitos que existem na especificação",
                uncited.Count == 0, uncited),
            Check("questions", $"Backend e frontend são decididos pelas perguntas {BackendQuestion} e {FrontendQuestion} do catálogo",
                unanswered.Count == 0, unanswered),
            Check("consistent", "A opção de cada escolha é a que os fatores citados favorecem (mesmos fatores, mesma escolha), ou a escolha explica a exceção",
                inconsistent.Count == 0, inconsistent),
            Check("tradeoffs", "Cada escolha da stack diz a alternativa descartada, por quê, e o trade-off que aceita",
                withoutTradeoff.Count == 0, withoutTradeoff)
        ];
    }

    /// <summary>A opção que os fatores citados favorecem; nulo no empate ou sem fator da pergunta.</summary>
    public static string? Favored(IEnumerable<string> factorIds, string question, IReadOnlyDictionary<string, Factor> factors)
    {
        var tally = factorIds.Select(id => factors.GetValueOrDefault(id)).OfType<Factor>()
            .Where(factor => factor.Question == question)
            .GroupBy(factor => factor.Favors, StringComparer.Ordinal)
            .Select(group => (Option: group.Key, Count: group.Count()))
            .OrderByDescending(item => item.Count)
            .ToList();
        if (tally.Count == 0 || (tally.Count > 1 && tally[0].Count == tally[1].Count)) return null;
        return tally[0].Option;
    }

    private static bool Cites(Choice choice, Dictionary<string, HashSet<string>> questions, Dictionary<string, Factor> factors, HashSet<string> requirements)
    {
        if (choice.Requirements.Any(id => !requirements.Contains(id))) return false;
        if (choice.Question.Length == 0) return choice.Requirements.Count > 0;
        return questions.TryGetValue(choice.Question, out var options) && options.Contains(choice.Option)
            && choice.Factors.Count > 0
            && choice.Factors.All(id => factors.TryGetValue(id, out var factor) && factor.Question == choice.Question);
    }

    private static bool Consistent(Choice choice, Dictionary<string, Factor> factors) =>
        choice.Exception.Length > 0 || Favored(choice.Factors, choice.Question, factors) == choice.Option;

    private static IdeaCheck Check(string id, string label, bool passed, IReadOnlyList<string> items) =>
        new(id, label, passed, passed || items.Count == 0 ? null : string.Join(", ", items.Select(item => item.Length == 0 ? "(sem id)" : item).Take(12)));

    internal sealed record Factor(string Id, string Criterion, string Question, string Favors, bool Valid)
    {
        public static Factor From(JsonObject item, IReadOnlyDictionary<string, DecisionCriterion> criteria)
        {
            var id = Text(item, "id");
            var criterionId = Text(item, "criterion");
            var favors = Text(item, "favors");
            if (!criteria.TryGetValue(criterionId, out var criterion))
                return new(id, criterionId, "", favors, false);
            // A linha de "Escolha | Quando" é a própria opção: o fator que a cita não precisa repetir o nome.
            if (favors.Length == 0 && criterion.Options.Count == 1) favors = criterion.Options[0].Name;
            var valid = FactorId().IsMatch(id) && Text(item, "fact").Length > 0
                        && criterion.Options.Any(option => option.Name.Equals(favors, StringComparison.Ordinal));
            return new(id, criterionId, criterion.Question, favors, valid);
        }
    }

    private sealed record Choice(string Name, string Area, string Question, string Option, IReadOnlyList<string> Factors,
        IReadOnlyList<string> Requirements, string Exception, bool TradeoffsStated)
    {
        public static Choice From(JsonObject item, int index)
        {
            var area = Text(item, "area");
            var alternatives = Objects(item, "alternatives");
            return new(area.Length > 0 ? area : $"stack {index + 1}", area, Text(item, "question"), Text(item, "option"),
                Strings(item, "factors"), Strings(item, "requirements"), Text(item, "exception"),
                alternatives.Count > 0 && alternatives.All(alternative => Text(alternative, "choice").Length > 0 && Text(alternative, "whyNot").Length > 0)
                && Text(item, "tradeoffs").Length > 0);
        }
    }

    [GeneratedRegex(@"^F\d{1,2}$")]
    private static partial Regex FactorId();
}
