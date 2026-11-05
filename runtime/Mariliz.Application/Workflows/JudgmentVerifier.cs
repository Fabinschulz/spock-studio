using System.Text.Json.Nodes;
using Mariliz.Application.Execution;
using Mariliz.Application.Models;
using Mariliz.Application.Ports;
using Mariliz.Application.Protocol;
using Mariliz.Core.Policy;

namespace Mariliz.Application.Workflows;

/// <summary>
/// Confere a opinião do juiz por modelo antes de o motor a registrar. O runtime de IA afirma; quem vale é o proxy: sem uma
/// chamada de juiz que o proxy viu, a opinião não existe. Modelo, versão, hash do pedido e tokens vêm do proxy; notas,
/// justificativa e citações vêm do runtime depois de validadas. Qualquer falha devolve <c>null</c>, nunca uma opinião parcial.
/// </summary>
internal static class JudgmentVerifier
{
    public const string JudgeAgent = "judge";
    private static readonly string[] BaseCriteria = ["plan_quality", "context_fit", "request_fit"];
    private const string ImpactCriterion = "architecture_coherence";
    private const int MaxRationale = 300;
    private const int MaxEvidence = 8;

    public static JournalJudgment? Verify(WorkflowAck ack, IReadOnlyList<ModelCallRecord> calls)
    {
        if (ack.Output["evaluation"] is not JsonObject evaluation || evaluation.Text("verdict") != "deterministic") return null;
        var declared = evaluation.Text("judge");
        if (declared is not ("passed" or "failed") || evaluation["judgment"] is not JsonObject judgment) return null;
        if (judgment.Text("schema") != "engineering.judgment.v1") return null;

        var criteria = judgment.Text("criteriaVersion");
        if (criteria.Length is 0 or > 9 || !criteria.All(char.IsAsciiDigit)) return null;
        if (!TryScores(judgment["scores"], ack.Output["impact"] is JsonObject, out var scores)) return null;
        if (!TryEvidence(judgment["evidence"], ack.Output, out var evidence)) return null;

        var rationale = SecretScanner.RedactLines(judgment.Text("rationale").Trim());
        if (rationale.Length is 0 or > MaxRationale || rationale.Any(c => char.IsControl(c) && c != '\n' && c != '\t')) return null;

        var outcome = scores.Values.All(score => score >= JournalJudgment.PassingScore) ? "passed" : "failed";
        if (outcome != declared) return null;

        var call = calls.LastOrDefault(c => c.AgentId == JudgeAgent && c.Purpose == "judge");
        if (call is null) return null;

        return new JournalJudgment
        {
            Outcome = outcome,
            CriteriaVersion = criteria,
            Scores = scores,
            Rationale = rationale,
            Evidence = evidence,
            Model = call.Model,
            ModelVersion = call.ModelVersion,
            PromptHash = call.PromptHash,
            InputTokens = call.InputTokens,
            OutputTokens = call.OutputTokens
        };
    }

    private static bool TryScores(JsonNode? node, bool withImpact, out Dictionary<string, int> scores)
    {
        scores = [];
        if (node is not JsonObject source) return false;
        string[] expected = withImpact ? [.. BaseCriteria, ImpactCriterion] : BaseCriteria;
        if (source.Count != expected.Length) return false;
        foreach (var name in expected)
        {
            if (source[name] is not JsonValue value || !value.TryGetValue<int>(out var score) || score is < 1 or > 5) return false;
            scores[name] = score;
        }
        return true;
    }

    /// <summary>Só trechos que a recuperação desta execução devolveu: citar outro é inventar proveniência.</summary>
    private static bool TryEvidence(JsonNode? node, JsonObject output, out List<string> evidence)
    {
        evidence = [];
        if (node is not JsonArray refs || refs.Count > MaxEvidence) return false;
        var known = output["evidence"] is JsonArray items
            ? items.OfType<JsonObject>().Select(item => item.Text("chunkId")).ToHashSet(StringComparer.Ordinal)
            : [];
        foreach (var item in refs)
        {
            if (item is not JsonValue value || !value.TryGetValue<string>(out var chunk) || !known.Contains(chunk)) return false;
            if (!evidence.Contains(chunk)) evidence.Add(chunk);
        }
        return known.Count == 0 || evidence.Count > 0;
    }
}
