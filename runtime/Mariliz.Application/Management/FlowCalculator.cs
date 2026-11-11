using Mariliz.Application.Integrations;

namespace Mariliz.Application.Management;

/// <summary>
/// Indicadores de fluxo de um tipo de dado, calculados só com os itens sincronizados e o relógio. Mesmos itens e mesmo
/// instante dão sempre o mesmo resultado. Nenhum indicador é por pessoa.
/// </summary>
public static class FlowCalculator
{
    /// <summary>Abaixo disso, mediana e percentil de lead time variam demais para serem lidos como tendência.</summary>
    public const int MinLeadTimeSample = 5;

    public const string NoItems = "Ainda não há itens sincronizados deste tipo.";

    public static KindFlow Compute(string kind, IReadOnlyCollection<WorkItem> items, DateTimeOffset now, int weeks)
    {
        var open = items.Count(DeliveryRules.IsOpen);
        if (items.Count == 0)
            return new KindFlow(kind, 0, 0, new CountMetric(null, NoItems), new ThroughputMetric([], null, null, NoItems),
                new LeadTimeMetric(0, null, null, NoItems), new OverdueMetric(null, 0, NoItems));

        return new KindFlow(
            kind,
            items.Count,
            open,
            new CountMetric(items.Count(DeliveryRules.IsInProgress), null),
            Throughput(items, now, weeks),
            LeadTime(items, now.AddDays(-7 * weeks), now),
            Overdue(items, now));
    }

    /// <summary>Janelas de 7 dias terminando agora, da mais antiga para a mais recente. A data que conta é a de conclusão.</summary>
    private static ThroughputMetric Throughput(IReadOnlyCollection<WorkItem> items, DateTimeOffset now, int weeks)
    {
        var windows = Enumerable.Range(0, weeks)
            .Select(index =>
            {
                var to = now.AddDays(-7 * (weeks - 1 - index));
                var from = to.AddDays(-7);
                return new WeekWindow(
                    from,
                    to,
                    items.Count(item => DeliveryRules.IsDelivered(item) && Within(item.ClosedAt!.Value, from, to)),
                    items.Count(item => Within(item.CreatedAt, from, to)));
            })
            .ToList();
        var total = windows.Sum(window => window.Delivered);
        return new ThroughputMetric(windows, total, Round((double)total / weeks), null);
    }

    /// <summary>Dias corridos entre a criação e a conclusão, dos itens entregues no período. Mediana e percentil 85 por posição.</summary>
    private static LeadTimeMetric LeadTime(IReadOnlyCollection<WorkItem> items, DateTimeOffset from, DateTimeOffset to)
    {
        var days = items
            .Where(item => DeliveryRules.IsDelivered(item) && Within(item.ClosedAt!.Value, from, to))
            .Select(item => Math.Max(0, (item.ClosedAt!.Value - item.CreatedAt).TotalDays))
            .Order()
            .ToList();

        if (days.Count < MinLeadTimeSample)
            return new LeadTimeMetric(days.Count, null, null,
                $"São precisos ao menos {MinLeadTimeSample} itens entregues no período; há {days.Count}.");

        return new LeadTimeMetric(days.Count, Round(Percentile(days, 0.5)), Round(Percentile(days, 0.85)), null);
    }

    private static OverdueMetric Overdue(IReadOnlyCollection<WorkItem> items, DateTimeOffset now)
    {
        var withDueDate = items.Count(item => DeliveryRules.IsOpen(item) && item.DueDate is not null);
        if (items.All(item => item.DueDate is null))
            return new OverdueMetric(null, 0, "A fonte não informa prazo para estes itens.");
        return new OverdueMetric(items.Count(item => DeliveryRules.IsOverdue(item, now)), withDueDate, null);
    }

    /// <summary>Método da posição mais próxima: o valor na posição ⌈p·n⌉ da lista ordenada. É um valor observado, não interpolado.</summary>
    internal static double Percentile(IReadOnlyList<double> sorted, double p) =>
        sorted[Math.Clamp((int)Math.Ceiling(p * sorted.Count) - 1, 0, sorted.Count - 1)];

    /// <summary>Uma casa decimal, com o meio arredondado para cima (0,75 vira 0,8), como se lê numa planilha.</summary>
    private static double Round(double value) => Math.Round(value, 1, MidpointRounding.AwayFromZero);

    private static bool Within(DateTimeOffset at, DateTimeOffset from, DateTimeOffset to) => at >= from && at < to;
}
