using Mariliz.Application.Integrations;

namespace Mariliz.Application.Management;

// Base comum de indicadores, previsão e riscos: todos contam semanas do mesmo jeito.
public static class WeeklyHistory
{
    public static int[] Delivered(IReadOnlyCollection<WorkItem> items, DateTimeOffset now, int weeks) =>
        Count(items.Where(DeliveryRules.IsDelivered).Select(item => item.ClosedAt!.Value), now, weeks);

    public static int[] Created(IReadOnlyCollection<WorkItem> items, DateTimeOffset now, int weeks) =>
        Count(items.Select(item => item.CreatedAt), now, weeks);

    public static int Covered(IReadOnlyCollection<WorkItem> items, DateTimeOffset now) =>
        items.Count == 0 ? 0 : (int)Math.Floor((now - items.Min(item => item.CreatedAt)).TotalDays / 7);

    private static int[] Count(IEnumerable<DateTimeOffset> dates, DateTimeOffset now, int weeks)
    {
        var counts = new int[weeks];
        foreach (var at in dates)
        {
            if (at >= now) continue;
            var index = weeks - 1 - (int)Math.Floor((now - at).TotalDays / 7);
            if (index >= 0) counts[index]++;
        }
        return counts;
    }
}
