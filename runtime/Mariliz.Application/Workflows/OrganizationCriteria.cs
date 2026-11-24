using Mariliz.Application.Discovery;

namespace Mariliz.Application.Workflows;

/// <param name="Id">Número da pergunta e o fator da linha ("10.4-conhecimento-da-equipe"): estável enquanto o texto do fator não muda.</param>
/// <param name="Question">A subseção que faz a pergunta ("10.4").</param>
/// <param name="Options">O que a linha diz de cada opção que ela favorece; a célula "-" fica de fora.</param>
public sealed record DecisionCriterion(
    string Id,
    string Question,
    string QuestionTitle,
    string Factor,
    IReadOnlyList<DecisionOption> Options,
    string Source,
    DateTimeOffset AnalyzedAt);

public sealed record DecisionOption(string Name, string Text);

public sealed record DecisionQuestion(string Id, string Title, IReadOnlyList<string> Options);

public static partial class OrganizationReferences
{
    private const string DecisionSection = "Diretrizes de decisão";
    private const string FavorsPrefix = "Favorece ";

    private static readonly Lazy<IReadOnlyList<DecisionCriterion>> AllCriteria =
        new(() => Files().SelectMany(file => CriteriaOf(file.Markdown)).ToList());

    // As linhas das tabelas das "Diretrizes de decisão" (§10 do catálogo): o critério que cada escolha de stack cita (W4-3).
    public static IReadOnlyList<DecisionCriterion> DecisionCriteria => AllCriteria.Value;

    public static IReadOnlyList<DecisionQuestion> DecisionQuestions =>
        DecisionCriteria.GroupBy(criterion => criterion.Question)
            .Select(group => new DecisionQuestion(group.Key, group.First().QuestionTitle,
                group.SelectMany(criterion => criterion.Options.Select(option => option.Name)).Distinct(StringComparer.Ordinal).ToList()))
            .ToList();

    // Duas formas de tabela: "Escolha | Quando" (a linha é a opção) e "Fator | Favorece A | Favorece B" ou "Contexto | Leve |
    // Completo" (cada coluna é uma opção, e a célula "-" diz que o fator não a favorece).
    internal static IEnumerable<DecisionCriterion> CriteriaOf(string markdown)
    {
        var frontmatter = FrontmatterReader.Read(markdown);
        if (frontmatter is null || !frontmatter.TryGetValue("titulo", out var title) || !frontmatter.TryGetValue("analisadoEm", out var analyzed)
            || !DateTimeOffset.TryParse(analyzed, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AssumeUniversal, out var analyzedAt))
            yield break;

        foreach (var section in Section().Split(markdown.Replace("\r\n", "\n")).Skip(1))
        {
            if (!Heading().Replace(section[..section.IndexOf('\n')].Trim(), "").Equals(DecisionSection, StringComparison.Ordinal)) continue;

            var lines = section.Split('\n');
            string? question = null;
            var questionTitle = "";
            IReadOnlyList<string> header = [];
            var rows = false;
            for (var index = 0; index < lines.Length; index++)
            {
                var line = lines[index];
                if (line.StartsWith("### ", StringComparison.Ordinal))
                {
                    var subsection = SubsectionHeading().Match(line);
                    question = subsection.Success ? subsection.Groups["id"].Value : null;
                    questionTitle = subsection.Success ? subsection.Groups["title"].Value.Trim() : "";
                    rows = false;
                    continue;
                }
                if (question is null) continue;
                if (Separator().IsMatch(line) && index > 0)
                {
                    header = Cells(lines[index - 1]);
                    rows = true;
                    continue;
                }
                if (!rows || !line.StartsWith('|'))
                {
                    if (!line.StartsWith('|')) rows = false;
                    continue;
                }

                var cells = Cells(line).Select(Clean).ToList();
                var factor = cells[0];
                if (factor.Length == 0) continue;
                List<DecisionOption> options = header.Count == 2
                    ? [new DecisionOption(factor, cells.ElementAtOrDefault(1) ?? "")]
                    : cells.Skip(1).Select((cell, column) => (Name: OptionName(header.ElementAtOrDefault(column + 1) ?? ""), Text: cell))
                        .Where(option => option.Name.Length > 0 && !IsEmptyCell(option.Text))
                        .Select(option => new DecisionOption(option.Name, option.Text))
                        .ToList();
                if (options.Count == 0) continue;
                yield return new DecisionCriterion($"{question}-{Slug(factor)}", question, questionTitle, factor, options, title, analyzedAt);
            }
        }
    }

    private static string OptionName(string header) =>
        (header.StartsWith(FavorsPrefix, StringComparison.Ordinal) ? header[FavorsPrefix.Length..] : header).Trim();

    private static bool IsEmptyCell(string cell) => cell.Length == 0 || cell == "-" || cell == EmptyCell;

    [System.Text.RegularExpressions.GeneratedRegex(@"^### (?<id>\d+\.\d+)\s+(?<title>.+)$")]
    private static partial System.Text.RegularExpressions.Regex SubsectionHeading();
}
