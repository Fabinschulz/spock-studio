using Mariliz.Application.Integrations;

namespace Mariliz.Application.Management;

/// <summary>
/// Contagens por janela de 7 dias terminando agora, da mais antiga para a mais recente. Base comum de indicadores,
/// previsão e riscos: todos contam semanas do mesmo jeito.
/// </summary>
public static class WeeklyHistory
{
    /// <summary>Entregues por semana, nas últimas <paramref name="weeks"/> janelas.</summary>
    public static int[] Delivered(IReadOnlyCollection<WorkItem> items, DateTimeOffset now, int weeks) =>
        Count(items.Where(DeliveryRules.IsDelivered).Select(item => item.ClosedAt!.Value), now, weeks);

    /// <summary>Criados por semana, nas últimas <paramref name="weeks"/> janelas.</summary>
    public static int[] Created(IReadOnlyCollection<WorkItem> items, DateTimeOffset now, int weeks) =>
        Count(items.Select(item => item.CreatedAt), now, weeks);

    /// <summary>Quantas semanas completas os dados cobrem, contando da criação do item mais antigo até agora.</summary>
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
