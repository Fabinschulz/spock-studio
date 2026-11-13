using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Mariliz.Application.Execution;

namespace Mariliz.Application.Management.Assistant;

// Uma afirmação só passa se citar um fato existente e todo número dela aparecer nos fatos citados: o modelo explica, não inventa.
public static partial class AnswerVerifier
{
    public const int MaxStatements = 6;
    public const int MaxText = 600;

    public static readonly IReadOnlyList<(string Key, string Label)> Sections =
    [
        ("observed", "Dados observados"),
        ("calculations", "Cálculos"),
        ("inferences", "Inferências"),
        ("recommendations", "Recomendações")
    ];

    public sealed record Verified(
        string Summary,
        IReadOnlyDictionary<string, List<AssistantStatement>> Sections,
        List<string> Unavailable,
        List<RejectedStatement> Rejected,
        HashSet<string> Cited)
    {
        // rejected substitui os descartados quando quem chama soma os seus.
        public AssistantAnswer ToAnswer(
            string projectId, DateTimeOffset answeredAt, IEnumerable<EvidenceFact> facts, string model, int inputTokens, int outputTokens,
            IReadOnlyList<RejectedStatement>? rejected = null) => new(
            projectId,
            answeredAt,
            Summary,
            Sections["observed"],
            Sections["calculations"],
            Sections["inferences"],
            Sections["recommendations"],
            Unavailable,
            rejected ?? Rejected,
            facts.Where(fact => Cited.Contains(fact.Id)).ToList(),
            model,
            inputTokens,
            outputTokens);
    }

    public static Verified Verify(string modelText, EvidencePack pack)
    {
        if (Parse(modelText) is not { } reply) throw new EngineException("MODEL_INVALID_RESPONSE", "O modelo respondeu fora do formato esperado.");

        var facts = pack.Facts.ToDictionary(fact => fact.Id, StringComparer.Ordinal);
        var rejected = new List<RejectedStatement>();
        var cited = new HashSet<string>(StringComparer.Ordinal);
        var sections = new Dictionary<string, List<AssistantStatement>>(StringComparer.Ordinal);

        foreach (var (key, label) in Sections)
        {
            var accepted = new List<AssistantStatement>();
            foreach (var entry in Entries(reply[key]))
            {
                var text = Clean(entry["text"]);
                if (text.Length == 0) continue;
                var evidence = Ids(entry["evidence"]).Where(facts.ContainsKey).Distinct(StringComparer.Ordinal).ToList();
                var reason = evidence.Count == 0
                    ? "Não cita nenhum fato do pacote de evidências."
                    : UnsupportedNumber(text, evidence.Select(id => facts[id].Text)) is { } number
                        ? $"Traz o número {number}, que não está nos fatos citados."
                        : null;
                if (reason is not null || accepted.Count >= MaxStatements)
                {
                    rejected.Add(new RejectedStatement(label, text, reason ?? "Passou do limite de afirmações por seção."));
                    continue;
                }
                accepted.Add(new AssistantStatement(text, evidence));
                cited.UnionWith(evidence);
            }
            sections[key] = accepted;
        }

        // O resumo segue a mesma regra: número só com o fato que o sustenta citado. Conferir contra todos os fatos seria
        // frouxo demais, porque números pequenos aparecem em quase todo pacote.
        var summaryNode = reply["summary"] as JsonObject;
        var summary = Clean(summaryNode?["text"] ?? reply["summary"]);
        var summaryEvidence = Ids(summaryNode?["evidence"]).Where(facts.ContainsKey).ToList();
        if (summary.Length > 0 && UnsupportedNumber(summary, summaryEvidence.Select(id => facts[id].Text)) is { } summaryNumber)
        {
            rejected.Add(new RejectedStatement("Resumo", summary, $"Traz o número {summaryNumber}, que não está nos fatos citados."));
            summary = "";
        }
        else cited.UnionWith(summaryEvidence);

        var unavailable = Entries(reply["unavailable"]).Select(entry => Clean(entry["text"]))
            .Concat(reply["unavailable"] is JsonArray plain ? plain.OfType<JsonValue>().Select(Clean) : [])
            .Where(text => text.Length > 0).Distinct().Take(MaxStatements).ToList();

        return new Verified(summary, sections, unavailable, rejected, cited);
    }

    // Datas valem por partes (dia, mês, ano).
    internal static string? UnsupportedNumber(string text, IEnumerable<string> factTexts)
    {
        var known = factTexts.SelectMany(Numbers).ToHashSet(StringComparer.Ordinal);
        return Numbers(text).FirstOrDefault(number => !known.Contains(number));
    }

    /// <summary>Números normalizados: vírgula decimal vira ponto e zeros à esquerda e à direita somem ("05" e "5,0" são "5").</summary>
    private static IEnumerable<string> Numbers(string text) =>
        NumberPattern().Matches(text).Select(match =>
        {
            var raw = match.Value.Replace(',', '.');
            return decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var value)
                ? value.ToString("0.############", CultureInfo.InvariantCulture)
                : raw;
        });

    private static JsonObject? Parse(string text)
    {
        try { return JsonNode.Parse(text) as JsonObject; }
        catch (JsonException) { return null; }
    }

    private static IEnumerable<JsonObject> Entries(JsonNode? node) => node is JsonArray array ? array.OfType<JsonObject>() : [];

    private static IEnumerable<string> Ids(JsonNode? node) =>
        node is JsonArray array
            ? array.OfType<JsonValue>().Select(value => value.TryGetValue<string>(out var id) ? id : null).OfType<string>()
            : [];

    private static string Clean(JsonNode? node)
    {
        var text = node is JsonValue value && value.TryGetValue<string>(out var raw) ? raw.Trim() : "";
        return text.Length > MaxText ? text[..MaxText] : text;
    }

    [GeneratedRegex(@"\d+(?:[.,]\d+)?")]
    private static partial Regex NumberPattern();
}
