using Mariliz.Application.Integrations;

namespace Mariliz.Application.Management;

public sealed record WeekWindow(DateTimeOffset From, DateTimeOffset To, int Delivered, int Created);

/// <summary>Um indicador de contagem. <see cref="Unavailable"/> diz por que não há valor; nunca vem junto com um valor.</summary>
public sealed record CountMetric(int? Value, string? Unavailable);

public sealed record ThroughputMetric(IReadOnlyList<WeekWindow> Weeks, int? Total, double? WeeklyAverage, string? Unavailable);

public sealed record LeadTimeMetric(int Sample, double? MedianDays, double? P85Days, string? Unavailable);

/// <param name="Excluded">Entregues no período sem início conhecido, fora do cálculo.</param>
public sealed record CycleTimeMetric(int Sample, int Excluded, double? MedianDays, double? P85Days, string? Unavailable);

/// <param name="LinksRead">Sem isso, "nenhum bloqueio por vínculo" não quer dizer nada.</param>
public sealed record BlockedMetric(int? Value, int ViaLink, int ViaLabel, int LinksRead, string Basis, string? Unavailable);

public sealed record OverdueMetric(int? Value, int WithDueDate, string? Unavailable);

// Tipos diferentes não são somados: um pull request e a issue que ele fecha são o mesmo trabalho.
public sealed record KindFlow(
    string Kind,
    int Items,
    int Open,
    CountMetric Wip,
    ThroughputMetric Throughput,
    LeadTimeMetric LeadTime,
    CycleTimeMetric CycleTime,
    BlockedMetric Blocked,
    OverdueMetric Overdue);

/// <param name="Complete">Falso quando a última sincronização terminou parcial ou com falha, ou nunca houve uma.</param>
public sealed record MetricSource(
    string IntegrationId,
    string Provider,
    string ResourceName,
    SyncStatus? LastSyncStatus,
    DateTimeOffset? LastSyncedAt,
    bool Complete);

public sealed record NotCalculated(string Id, string Label, string Reason);

/// <param name="Complete">Verdadeiro só se todas as fontes estão completas. Com fontes incompletas os números valem para o que chegou.</param>
public sealed record ProjectMetrics(
    string ProjectId,
    DateTimeOffset GeneratedAt,
    int PeriodWeeks,
    DateTimeOffset PeriodFrom,
    DateTimeOffset PeriodTo,
    bool Complete,
    IReadOnlyList<MetricSource> Sources,
    IReadOnlyList<KindFlow> Kinds,
    IReadOnlyList<NotCalculated> NotCalculated);
