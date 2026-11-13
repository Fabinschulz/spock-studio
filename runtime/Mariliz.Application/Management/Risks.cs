namespace Mariliz.Application.Management;

public enum RiskSeverity
{
    Low,
    Medium,
    High
}

public sealed record RiskItem(string Kind, string ExternalId, string Title);

// Origin é sempre "rule" nesta versão; risco sugerido por IA virá com outra origem, nunca misturado a estes.
public sealed record ProjectRisk(
    string Id,
    string Title,
    RiskSeverity Severity,
    string Rule,
    IReadOnlyList<string> Evidence,
    IReadOnlyList<RiskItem> Items,
    string? Trend,
    string Recommendation,
    string Origin);

public sealed record ProjectRisks(
    string ProjectId,
    DateTimeOffset GeneratedAt,
    bool Complete,
    bool HasData,
    IReadOnlyList<ProjectRisk> Risks,
    IReadOnlyList<string> RulesEvaluated);
