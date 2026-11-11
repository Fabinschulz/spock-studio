using Mariliz.Application.Integrations;

namespace Mariliz.Application.Management;

/// <summary>Uma janela de 7 dias: quantos itens foram entregues e quantos chegaram nela.</summary>
public sealed record WeekWindow(DateTimeOffset From, DateTimeOffset To, int Delivered, int Created);

/// <summary>Um indicador de contagem. <see cref="Unavailable"/> diz por que não há valor; nunca vem junto com um valor.</summary>
public sealed record CountMetric(int? Value, string? Unavailable);

public sealed record ThroughputMetric(IReadOnlyList<WeekWindow> Weeks, int? Total, double? WeeklyAverage, string? Unavailable);

/// <param name="Sample">Quantos itens entregues no período entraram no cálculo.</param>
public sealed record LeadTimeMetric(int Sample, double? MedianDays, double? P85Days, string? Unavailable);

/// <param name="WithDueDate">Quantos itens abertos têm prazo informado: a base do indicador.</param>
public sealed record OverdueMetric(int? Value, int WithDueDate, string? Unavailable);

/// <summary>O fluxo de um tipo de dado. Tipos diferentes não são somados: um pull request e a issue que ele fecha são o mesmo trabalho.</summary>
public sealed record KindFlow(
    string Kind,
    int Items,
    int Open,
    CountMetric Wip,
    ThroughputMetric Throughput,
    LeadTimeMetric LeadTime,
    OverdueMetric Overdue);

/// <param name="Complete">Falso quando a última sincronização terminou parcial ou com falha, ou nunca houve uma.</param>
public sealed record MetricSource(
    string IntegrationId,
    string Provider,
    string ResourceName,
    SyncStatus? LastSyncStatus,
    DateTimeOffset? LastSyncedAt,
    bool Complete);

/// <summary>Um indicador pedido que esta versão não calcula, e por quê.</summary>
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
