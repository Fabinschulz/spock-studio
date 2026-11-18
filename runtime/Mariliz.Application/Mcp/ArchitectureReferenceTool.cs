using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Mariliz.Application.Context;
using Mariliz.Application.Workflows;

namespace Mariliz.Application.Mcp;

// W1-4: a pasta Organização para a IDE. Por id (o agente recebeu a lista na intenção architecture), por tecnologia e por
// assunto. O JSON segue contracts/schemas/mcp/spock_architecture_reference.output.schema.json; um teste confere.
public static partial class ArchitectureReferenceTool
{
    public const string Name = "spock_architecture_reference";
    public const int DefaultLimit = 8;
    public const int MaxLimit = 20;
    public const int MaxTopic = 200;
    public const string Notice = "Padrões de outros projetos, da pasta Organização: argumento de engenharia, não é convenção deste projeto.";

    private static readonly HashSet<string> Stopwords = new(StringComparer.Ordinal)
    {
        "para", "como", "com", "uma", "que", "dos", "das", "nos", "nas", "por", "sem", "mais", "entre", "sobre", "quando"
    };

    private static JsonObject Text(string description) => new() { ["type"] = "string", ["description"] = description };

    private static JsonObject Strings(string description) => new()
    {
        ["type"] = "array", ["items"] = new JsonObject { ["type"] = "string" }, ["description"] = description
    };

    public static JsonObject OutputSchema() => new()
    {
        ["type"] = "object",
        ["required"] = new JsonArray("technologies", "topic", "patterns", "omitted", "notice"),
        ["properties"] = new JsonObject
        {
            ["technologies"] = Strings("Stacks usadas no filtro; vazio quando nenhuma foi informada nem detectada (vem tudo)."),
            ["topic"] = new JsonObject { ["type"] = new JsonArray("string", "null"), ["description"] = "O assunto pedido, quando houve." },
            ["patterns"] = new JsonObject
            {
                ["type"] = "array",
                ["items"] = new JsonObject
                {
                    ["type"] = "object",
                    ["required"] = new JsonArray("id", "title", "section", "technologies", "source", "analyzedAt", "classification", "details"),
                    ["properties"] = new JsonObject
                    {
                        ["id"] = Text("Id estável do padrão (REF-…)."),
                        ["title"] = Text("O padrão em uma linha."),
                        ["section"] = Text("Seção do catálogo."),
                        ["technologies"] = Strings("Stacks a que a seção vale; vazio quando vale para todas."),
                        ["source"] = Text("Arquivo da pasta Organização."),
                        ["analyzedAt"] = new JsonObject { ["type"] = "string", ["format"] = "date-time" },
                        ["classification"] = new JsonObject { ["type"] = "string", ["enum"] = new JsonArray(ContextClassifications.Reference) },
                        ["details"] = new JsonObject
                        {
                            ["type"] = "array",
                            ["items"] = new JsonObject
                            {
                                ["type"] = "object",
                                ["required"] = new JsonArray("label", "text"),
                                ["properties"] = new JsonObject { ["label"] = Text("Coluna da tabela."), ["text"] = Text("Conteúdo.") },
                                ["additionalProperties"] = false
                            }
                        }
                    },
                    ["additionalProperties"] = false
                }
            },
            ["omitted"] = new JsonObject { ["type"] = "integer", ["minimum"] = 0, ["description"] = "Quantos casaram e ficaram de fora pelo limite." },
            ["notice"] = Text("O que esses padrões são e o que não são.")
        },
        ["additionalProperties"] = false
    };

    public static readonly McpToolInfo Info = new(
        Name, "Referências arquiteturais da Organização",
        "Padrões de engenharia extraídos de outros projetos (React, Angular, .NET, monorepo, Harness), com onde foram vistos, o problema " +
        "que resolvem, quando usar e quando evitar. Peça por 'id' (o REF-… da lista da intenção architecture) para ler um padrão inteiro, " +
        "ou por 'technology' e 'topic'. Sem nada, usa as stacks do projeto. São argumento, não convenção do projeto: diga isso ao usar.",
        new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject
            {
                ["project"] = new JsonObject { ["type"] = "string", ["description"] = "Nome ou id do projeto, como em list_projects. Sem ele, vale o projeto da pasta atual." },
                ["id"] = new JsonObject { ["type"] = "string", ["pattern"] = "^[Rr][Ee][Ff]-[A-Za-z]+[0-9]+$", ["description"] = "Um padrão pelo id (REF-R4)." },
                ["technology"] = new JsonObject
                {
                    ["type"] = "string", ["pattern"] = "^[a-z0-9-]{1,40}$",
                    ["description"] = "Stack para filtrar (react, nextjs, angular, dotnet, node, python…). Sem ela, valem as stacks do projeto."
                },
                ["topic"] = new JsonObject { ["type"] = "string", ["maxLength"] = MaxTopic, ["description"] = "Assunto em palavras (autenticação, migração, estado da tela…)." },
                ["limit"] = new JsonObject
                {
                    ["type"] = "integer", ["minimum"] = 1, ["maximum"] = MaxLimit, ["description"] = $"Quantos padrões trazer (padrão {DefaultLimit})."
                }
            },
            ["additionalProperties"] = false
        },
        OutputSchema: OutputSchema());

    public sealed record Selection(IReadOnlyList<string> Technologies, string? Topic, IReadOnlyList<ReferencePattern> Patterns, int Omitted);

    // Sem modelo: o id acha um padrão; senão, a stack filtra e as palavras do assunto ordenam (título pesa mais que o resto).
    public static Selection Select(IReadOnlyList<ReferencePattern> catalog, string? id, IReadOnlyList<string> stacks, string? topic, int limit)
    {
        if (id is not null)
        {
            var found = catalog.FirstOrDefault(pattern => string.Equals(pattern.Id, id, StringComparison.OrdinalIgnoreCase))
                ?? throw new McpToolException($"O padrão {id} não existe na pasta Organização. Chame sem 'id' para ver a lista.");
            return new Selection(found.Technologies, null, [found], 0);
        }

        var applicable = catalog.Where(pattern => pattern.AppliesTo(stacks.ToHashSet(StringComparer.Ordinal))).ToList();
        var terms = Terms(topic);
        var ranked = terms.Count == 0
            ? applicable.OrderBy(pattern => pattern.Technologies.Count == 0).ToList()
            : applicable.Select((pattern, order) => (pattern, order, score: Score(pattern, terms)))
                .Where(item => item.score > 0)
                .OrderByDescending(item => item.score).ThenBy(item => item.order)
                .Select(item => item.pattern).ToList();
        return new Selection(stacks, topic, ranked.Take(limit).ToList(), Math.Max(0, ranked.Count - limit));
    }

    public static McpReply Reply(Selection selection) => new(Render(selection), Structured(selection));

    public static JsonObject Structured(Selection selection) => new()
    {
        ["technologies"] = new JsonArray(selection.Technologies.Select(id => (JsonNode)id).ToArray()),
        ["topic"] = selection.Topic,
        ["patterns"] = new JsonArray(selection.Patterns.Select(pattern => (JsonNode)new JsonObject
        {
            ["id"] = pattern.Id,
            ["title"] = pattern.Title,
            ["section"] = pattern.Section,
            ["technologies"] = new JsonArray(pattern.Technologies.Select(id => (JsonNode)id).ToArray()),
            ["source"] = pattern.Source,
            ["analyzedAt"] = pattern.AnalyzedAt.ToString("O"),
            ["classification"] = ContextClassifications.Reference,
            ["details"] = new JsonArray(pattern.Details.Select(detail => (JsonNode)new JsonObject { ["label"] = detail.Label, ["text"] = detail.Text }).ToArray())
        }).ToArray()),
        ["omitted"] = selection.Omitted,
        ["notice"] = Notice
    };

    private static string Render(Selection selection)
    {
        var filter = selection.Technologies.Count == 0 ? "todas as stacks" : string.Join(", ", selection.Technologies);
        var text = new StringBuilder("# Referências da Organização\n")
            .Append($"Filtro: {filter}{(selection.Topic is null ? "" : $" · assunto: {selection.Topic}")}. {Notice}\n");
        if (selection.Patterns.Count == 0)
            text.Append("\nNenhum padrão casa com o pedido. Tente outras palavras no 'topic' ou outra 'technology'.\n");
        foreach (var pattern in selection.Patterns)
        {
            var applies = pattern.Technologies.Count == 0 ? "todas as stacks" : string.Join(", ", pattern.Technologies);
            text.Append($"\n## {pattern.Id} · {pattern.Title}\n")
                .Append($"{pattern.Section} · vale para {applies} · {pattern.Source}, análise de {pattern.AnalyzedAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}\n");
            foreach (var detail in pattern.Details) text.Append($"- {detail.Label}: {detail.Text}\n");
        }
        if (selection.Omitted > 0) text.Append($"\n{selection.Omitted} padrões a mais casaram; aumente 'limit' ou refine o 'topic'.\n");
        return text.ToString();
    }

    private static List<string> Terms(string? topic) => topic is null ? [] : Word().Matches(Fold(topic))
        .Select(match => match.Value).Where(word => word.Length >= 3 && !Stopwords.Contains(word)).Distinct().ToList();

    private static int Score(ReferencePattern pattern, IReadOnlyList<string> terms)
    {
        var title = Fold(pattern.Title);
        var rest = Fold($"{pattern.Section} {string.Join(" ", pattern.Details.Select(detail => detail.Text))}");
        return terms.Sum(term => (title.Contains(term, StringComparison.Ordinal) ? 3 : 0) + (rest.Contains(term, StringComparison.Ordinal) ? 1 : 0));
    }

    private static string Fold(string text) => new(text.Normalize(NormalizationForm.FormD)
        .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray().Select(char.ToLowerInvariant).ToArray());

    [GeneratedRegex(@"[a-z0-9]+")]
    private static partial Regex Word();
}
