namespace Mariliz.Application.Management;

public enum ForecastConfidence
{
    Low,
    Medium,
    High
}

public sealed record ForecastScenario(string Id, string Label, int Percentile, int Weeks, DateOnly By);

// Sem base suficiente não há cenários: a previsão nunca é inventada.
public sealed record DeliveryForecast(
    string Kind,
    int Remaining,
    int HistoryWeeks,
    IReadOnlyList<int> WeeklyThroughput,
    int Trials,
    IReadOnlyList<ForecastScenario> Scenarios,
    ForecastConfidence? Confidence,
    IReadOnlyList<string> ConfidenceReasons,
    IReadOnlyList<string> RiskFactors,
    string? Unavailable);

public sealed record ProjectForecast(
    string ProjectId,
    DateTimeOffset GeneratedAt,
    string Method,
    bool Complete,
    IReadOnlyList<string> Assumptions,
    IReadOnlyList<string> Limitations,
    IReadOnlyList<DeliveryForecast> Kinds);
