namespace Mariliz.Application.Execution;

/// <summary>
/// A opinião do juiz por modelo (EV-JUDGE-001) já conferida pelo motor: o formato, a escala, as citações e a chamada ao
/// modelo que o proxy viu. É um registro, nunca um veredito: o resultado da execução segue determinístico.
/// </summary>
public sealed class JournalJudgment
{
    public const string EvalId = "EV-JUDGE-001";
    public const int PassingScore = 3;

    public string Outcome { get; set; } = "";
    public string CriteriaVersion { get; set; } = "";
    public Dictionary<string, int> Scores { get; set; } = [];
    public string Rationale { get; set; } = "";
    public List<string> Evidence { get; set; } = [];
    public string Model { get; set; } = "";
    public string ModelVersion { get; set; } = "";
    public string PromptHash { get; set; } = "";
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
}
