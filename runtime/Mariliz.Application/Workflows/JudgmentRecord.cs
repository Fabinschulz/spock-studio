using System.Text.Json.Nodes;
using Mariliz.Application.Execution;

namespace Mariliz.Application.Workflows;

/// <summary>O registro em disco (<c>engineering.eval.record.v1</c>) e o bloco <c>evaluation</c> do resultado, a partir da opinião conferida.</summary>
internal static class JudgmentRecord
{
    public static JsonObject ForDisk(JournalJudgment judgment, string runId, string projectId, string executionId, DateTimeOffset at) => new()
    {
        ["schema"] = "engineering.eval.record.v1",
        ["evalId"] = JournalJudgment.EvalId,
        ["kind"] = "model",
        ["executionId"] = executionId,
        ["runId"] = runId,
        ["projectId"] = projectId,
        ["criteriaVersion"] = judgment.CriteriaVersion,
        ["model"] = judgment.Model,
        ["modelVersion"] = judgment.ModelVersion,
        ["promptHash"] = judgment.PromptHash,
        ["outcome"] = judgment.Outcome,
        ["scores"] = Scores(judgment),
        ["rationale"] = judgment.Rationale,
        ["evidence"] = Evidence(judgment),
        ["inputTokens"] = judgment.InputTokens,
        ["outputTokens"] = judgment.OutputTokens,
        ["recordedAt"] = at.ToString("O")
    };

    /// <summary>O veredito segue determinístico; a opinião do juiz, quando houve, vai ao lado, nunca no lugar.</summary>
    public static JsonObject Evaluation(JournalJudgment? judgment)
    {
        var evaluation = new JsonObject
        {
            ["schema"] = "engineering.evaluation.v1",
            ["verdict"] = "deterministic",
            ["judge"] = judgment?.Outcome ?? "not_run",
            ["tokens"] = judgment is null ? 0 : judgment.InputTokens + judgment.OutputTokens
        };
        if (judgment is not null)
            evaluation["judgment"] = new JsonObject
            {
                ["schema"] = "engineering.judgment.v1",
                ["criteriaVersion"] = judgment.CriteriaVersion,
                ["scores"] = Scores(judgment),
                ["rationale"] = judgment.Rationale,
                ["evidence"] = Evidence(judgment),
                ["model"] = judgment.Model,
                ["modelVersion"] = judgment.ModelVersion
            };
        return evaluation;
    }

    private static JsonObject Scores(JournalJudgment judgment)
    {
        var scores = new JsonObject();
        foreach (var (name, score) in judgment.Scores) scores[name] = score;
        return scores;
    }

    private static JsonArray Evidence(JournalJudgment judgment) =>
        new(judgment.Evidence.Select(item => (JsonNode)item).ToArray());
}
