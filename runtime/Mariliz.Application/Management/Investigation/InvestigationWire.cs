using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Mariliz.Application.Execution;
using Mariliz.Application.Management.Assistant;
using Mariliz.Application.Protocol;

namespace Mariliz.Application.Management.Investigation;

// O que volta é texto de fora: cada trecho é limitado e filtrado aqui antes de o InvestigationService conferi-lo contra os fatos.
internal static partial class InvestigationWire
{
    private const int MaxText = 400;
    private const int MaxRetrieved = 24;
    private static readonly string[] StepIds = ["frame", "gather", "hypothesize", "decide", "retrieve", "conclude"];

    public static JsonObject Request(string investigationId, string executionId, string projectId, string question, EvidencePack pack) => new()
    {
        ["schema"] = "ai.investigation.start.v2",
        ["investigationId"] = investigationId,
        ["executionId"] = executionId,
        ["projectId"] = projectId,
        ["question"] = question,
        ["facts"] = new JsonArray(pack.Facts.Select(fact => (JsonNode)new JsonObject
        {
            ["id"] = fact.Id,
            ["group"] = fact.Group,
            ["text"] = fact.Text
        }).ToArray()),
        ["groups"] = new JsonArray(FactGroups.For(pack.Facts).Select(group => (JsonNode)new JsonObject
        {
            ["name"] = group.Name,
            ["roles"] = new JsonArray(group.Roles.Select(role => (JsonNode)role).ToArray()),
            ["keywords"] = new JsonArray(group.Keywords.Select(keyword => (JsonNode)keyword).ToArray())
        }).ToArray()),
        ["policy"] = new JsonObject { ["modelAllowed"] = false },
        ["limits"] = new JsonObject { ["maxIterations"] = 12, ["maxMilliseconds"] = InvestigationService.RuntimeBudgetMilliseconds, ["maxTokens"] = 0 }
    };

    public static JsonObject MemoryRequest(string root, IReadOnlyList<MemoryDocument> documents) => new()
    {
        ["root"] = root,
        ["documents"] = new JsonArray(documents.Select(document => (JsonNode)new JsonObject
        {
            ["path"] = document.Path,
            ["hash"] = document.Hash,
            ["text"] = document.Text
        }).ToArray())
    };

    public static bool Sendable(EvidenceFact fact) => FactId().IsMatch(fact.Id) && fact.Group.Length > 0 && fact.Text.Length > 0;

    public static JsonObject ReadAnswer(JsonObject ack, string investigationId)
    {
        if (ack["schema"]?.GetValue<string>() != "ai.investigation.ack.v1" || ack.Text("investigationId") != investigationId)
            throw EngineErrors.AiRuntimeInvalidResponse();
        switch (ack.Text("status"))
        {
            case "rejected":
                throw new EngineException("INVESTIGATION_REJECTED", "O runtime de IA recusou o pedido da investigação.");
            case "failed":
                throw new EngineException("INVESTIGATION_FAILED", $"A investigação falhou ({Code(ack.Text("failure"))}).");
            case "completed" or "completed_with_limitations":
                break;
            default:
                throw EngineErrors.AiRuntimeInvalidResponse();
        }
        return ack["answer"] as JsonObject ?? throw EngineErrors.AiRuntimeInvalidResponse();
    }

    /// <summary>
    /// Os trechos da memória viram fatos escritos aqui, com o texto do documento que o motor mesmo mandou: o que o runtime diz de cada
    /// trecho vale só pelo id e pelo caminho, e um caminho que o motor não mandou não vira fato.
    /// </summary>
    public static IReadOnlyList<EvidenceFact> Retrieved(JsonNode? node, IReadOnlyList<MemoryDocument> documents)
    {
        var facts = new List<EvidenceFact>();
        if (node is not JsonArray array) return facts;
        var byPath = documents.ToDictionary(document => document.Path, StringComparer.Ordinal);
        foreach (var item in array.OfType<JsonObject>().Take(MaxRetrieved))
        {
            var id = item.Text("id");
            if (!MemoryFactId().IsMatch(id) || facts.Any(fact => fact.Id == id) || !byPath.TryGetValue(item.Text("path"), out var document)) continue;
            var kind = document.Path.StartsWith(DecisionMemory.Prefix, StringComparison.Ordinal)
                ? "decisão registrada pela equipe"
                : "item sincronizado, escrito por terceiros";
            facts.Add(new EvidenceFact(id, "memória", Clip($"Memória do projeto ({kind}): {document.Text.ReplaceLineEndings(" ")}", 1200)));
        }
        return facts;
    }

    public static List<InvestigationStep> Path(JsonNode? node, HashSet<string> known)
    {
        var steps = new List<InvestigationStep>();
        if (node is not JsonArray array) return steps;
        foreach (var item in array.OfType<JsonObject>().Take(24))
        {
            var id = item.Text("id");
            if (!StepIds.Contains(id)) continue;
            var round = item["round"] is JsonValue value && value.TryGetValue<int>(out var number) ? Math.Clamp(number, 0, 4) : 0;
            steps.Add(new InvestigationStep(
                id, Clip(item.Text("title"), 120), Clip(item.Text("detail"), MaxText), round,
                Strings(item["groups"]).Select(group => Clip(group, 40)).Take(12).ToList(),
                Strings(item["facts"]).Where(known.Contains).Distinct(StringComparer.Ordinal).ToList()));
        }
        return steps;
    }

    public static IEnumerable<RejectedStatement> Rejected(JsonNode? node)
    {
        if (node is not JsonArray array) yield break;
        foreach (var item in array.OfType<JsonObject>().Take(24))
        {
            var text = Clip(item.Text("text"), MaxText);
            if (text.Length == 0) continue;
            yield return new RejectedStatement("Investigação", text, item.Text("reason") switch
            {
                "no_valid_evidence" => "Não cita nenhum fato consultado na investigação.",
                "too_many" => "Passou do limite de afirmações por seção.",
                _ => "Veio fora do formato esperado."
            });
        }
    }

    public static List<InvestigationLimitation> Limitations(JsonNode? node)
    {
        var result = new List<InvestigationLimitation>();
        if (node is not JsonArray array) return result;
        foreach (var item in array.OfType<JsonObject>().Take(12))
        {
            var code = Code(item.Text("code"));
            var message = Clip(item.Text("message"), MaxText);
            if (message.Length > 0) result.Add(new InvestigationLimitation(code, message));
        }
        return result;
    }

    /// <summary>
    /// Quantas rodadas o ack diz que houve, dentro do teto do contrato (4). Sem o campo, conta as etapas que olharam fatos.
    /// </summary>
    public static int Rounds(JsonObject ack, IReadOnlyList<InvestigationStep> path) =>
        Math.Clamp(ack["rounds"] is JsonValue value && value.TryGetValue<int>(out var number) ? number : path.Count(step => step.Id is "gather" or "retrieve"), 0, 4);

    public static IEnumerable<string> Strings(JsonNode? node) =>
        node is JsonArray array
            ? array.OfType<JsonValue>().Select(value => value.TryGetValue<string>(out var text) ? text : null).OfType<string>()
            : [];

    public static string Clip(string text, int max)
    {
        var trimmed = text.Trim();
        return trimmed.Length > max ? trimmed[..max] : trimmed;
    }

    private static string Code(string code) => CodePattern().IsMatch(code) ? code : "DESCONHECIDO";

    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9._:-]{0,63}\z")]
    private static partial Regex FactId();

    [GeneratedRegex(@"^[A-Z][A-Z0-9_]{0,63}\z")]
    private static partial Regex CodePattern();

    [GeneratedRegex(@"^memory\.[0-9]{1,3}\z")]
    private static partial Regex MemoryFactId();
}
