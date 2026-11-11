namespace Mariliz.Application.Management;

public enum ForecastConfidence
{
    Low,
    Medium,
    High
}

/// <summary>
/// Um cenário da previsão: em <see cref="Percentile"/>% das simulações o trabalho restante termina em até
/// <see cref="Weeks"/> semanas, ou seja, até <see cref="By"/>.
/// </summary>
public sealed record ForecastScenario(string Id, string Label, int Percentile, int Weeks, DateOnly By);

/// <summary>
/// Previsão de conclusão do trabalho aberto de um tipo de dado. Sem base suficiente, <see cref="Unavailable"/> diz por
/// quê e não há cenários: a previsão nunca é inventada.
/// </summary>
/// <param name="WeeklyThroughput">As semanas sorteadas pela simulação, da mais antiga para a mais recente.</param>
/// <param name="RiskFactors">O que, nos próprios dados, pode tornar a previsão otimista demais.</param>
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

/// <param name="Assumptions">Premissas do método, iguais para todos os tipos.</param>
/// <param name="Limitations">O que o método não considera.</param>
public sealed record ProjectForecast(
    string ProjectId,
    DateTimeOffset GeneratedAt,
    string Method,
    bool Complete,
    IReadOnlyList<string> Assumptions,
    IReadOnlyList<string> Limitations,
    IReadOnlyList<DeliveryForecast> Kinds);
