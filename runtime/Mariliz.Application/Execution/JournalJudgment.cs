namespace Mariliz.Application.Execution;

// É um registro, nunca um veredito: o resultado da execução segue determinístico.
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
