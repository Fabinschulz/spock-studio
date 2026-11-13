using Mariliz.Application.Integrations;

namespace Mariliz.Application.Management;

// Mesmos itens e mesmo instante dão sempre o mesmo resultado. Nenhum indicador é por pessoa.
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
                new LeadTimeMetric(0, null, null, NoItems), new CycleTimeMetric(0, 0, null, null, NoItems), new BlockedMetric(null, 0, 0, 0, BlockedBasis(0, 0), NoItems),
                new OverdueMetric(null, 0, NoItems));

        return new KindFlow(
            kind,
            items.Count,
            open,
            new CountMetric(items.Count(DeliveryRules.IsInProgress), null),
            Throughput(items, now, weeks),
            LeadTime(items, now.AddDays(-7 * weeks), now),
            CycleTime(items, now.AddDays(-7 * weeks), now),
            Blocked(items),
            Overdue(items, now));
    }

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

    public const string NoStartInformation =
        "A fonte não informa quando o trabalho começou: sem histórico de status, não há como saber a data de início.";

    // O início é a primeira mudança para um status da categoria "em andamento". A categoria de um nome vem dos itens que estão nele agora e só vale se todos concordam, então um status por onde só passaram itens concluídos, ou com categorias conflitantes, não é reconhecido.
    private static CycleTimeMetric CycleTime(IReadOnlyCollection<WorkItem> items, DateTimeOffset from, DateTimeOffset to)
    {
        if (items.All(item => item.Transitions.Count == 0))
            return new CycleTimeMetric(0, 0, null, null, NoStartInformation);

        var categories = StatusCategoriesByName(items);
        var delivered = items.Where(item => DeliveryRules.IsDelivered(item) && Within(item.ClosedAt!.Value, from, to)).ToList();
        var days = new List<double>();
        foreach (var item in delivered)
        {
            if (StartOf(item, categories) is { } start)
                days.Add(Math.Max(0, (item.ClosedAt!.Value - start).TotalDays));
        }

        days.Sort();
        var excluded = delivered.Count - days.Count;
        if (days.Count < MinLeadTimeSample)
            return new CycleTimeMetric(days.Count, excluded, null, null,
                $"São precisos ao menos {MinLeadTimeSample} itens entregues com início conhecido no período; há {days.Count}"
                + (excluded > 0 ? $" ({excluded} sem início conhecido)." : "."));

        return new CycleTimeMetric(days.Count, excluded, Round(Percentile(days, 0.5)), Round(Percentile(days, 0.85)), null);
    }

    private static DateTimeOffset? StartOf(WorkItem item, IReadOnlyDictionary<string, string> categories) =>
        item.Transitions
            .Where(transition => categories.GetValueOrDefault(transition.To) == StatusCategories.InProgress)
            .Select(transition => (DateTimeOffset?)transition.At)
            .Min();

    private static Dictionary<string, string> StatusCategoriesByName(IReadOnlyCollection<WorkItem> items) =>
        items.Where(item => item.Status.Length > 0)
            .GroupBy(item => item.Status, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Select(item => item.StatusCategory).Distinct().Count() == 1)
            .ToDictionary(group => group.Key, group => group.First().StatusCategory, StringComparer.OrdinalIgnoreCase);

    // O vínculo "is blocked by" só o Jira informa.
    private static BlockedMetric Blocked(IReadOnlyCollection<WorkItem> items)
    {
        var linksRead = items.Count(item => item.BlockedBy is not null);
        var blocked = items.Count(DeliveryRules.IsBlocked);
        return new BlockedMetric(blocked, items.Count(DeliveryRules.IsBlockedByLink), items.Count(DeliveryRules.IsBlockedByLabel),
            linksRead, BlockedBasis(linksRead, items.Count), null);
    }

    public static string BlockedBasis(int linksRead, int items) =>
        "Rótulo \"blocked\" ou \"bloqueado\" e vínculo \"is blocked by\" do Jira com item ainda aberto. "
        + (linksRead == 0
            ? "Os vínculos de bloqueio não foram lidos nesta fonte (só o Jira os informa; sincronize de novo se ele já está vinculado)."
            : $"Vínculos lidos em {linksRead} de {items} itens; um tipo de vínculo diferente do padrão \"Blocks\" não é lido.");

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
