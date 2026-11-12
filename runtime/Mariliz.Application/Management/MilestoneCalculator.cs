using Mariliz.Application.Integrations;

namespace Mariliz.Application.Management;

/// <summary>
/// Andamento e previsão por marco. A previsão é a mesma do restante da Gestão (simulação sobre o ritmo semanal observado), mas
/// o trabalho restante é só o que está aberto no marco. O ritmo vem do tipo de dado inteiro: o marco sozinho teria poucas
/// entregas para servir de histórico.
/// </summary>
public static class MilestoneCalculator
{
    public const int MaxMilestones = 20;

    public static readonly IReadOnlyList<string> Assumptions =
    [
        "O ritmo é o do tipo de dado inteiro, não o do marco: o marco sozinho teria poucas entregas para servir de histórico.",
        "O trabalho restante é o que está aberto no marco hoje; nada novo entra nele.",
        "A equipe trabalha nos itens do marco, e não em outros itens do projeto, no mesmo ritmo de sempre."
    ];

    public static readonly IReadOnlyList<string> Limitations =
    [
        "Não considera a ordem em que os itens serão feitos nem as dependências entre eles.",
        "O prazo é o que o provedor guarda no marco (data do milestone, data de lançamento da versão); sem ele não há comparação.",
        "Um item com mais de uma versão de correção no Jira conta em cada uma delas.",
        "O mesmo nome de marco em tipos de dado diferentes aparece separado, como nas demais visões."
    ];

    /// <summary>Marcos de um tipo de dado e os itens que não têm marco. Mesmos itens e mesmo instante dão o mesmo resultado.</summary>
    public static (List<MilestoneProgress> Milestones, UnassignedItems Unassigned) Compute(
        string kind, IReadOnlyCollection<WorkItem> items, DateTimeOffset now, bool sourcesComplete)
    {
        var groups = items
            .SelectMany(item => (item.Milestones ?? []).Select(milestone => (milestone, item)))
            .GroupBy(pair => pair.milestone.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var milestones = groups.Select(group =>
        {
            var scope = group.Select(pair => pair.item).ToList();
            var due = group.Select(pair => pair.milestone.DueDate).Where(day => day is not null).Min();
            var open = scope.Count(DeliveryRules.IsOpen);
            var forecast = ForecastCalculator.Compute(kind, items, scope, now, sourcesComplete);
            var (outlook, reason) = Outlook(due, open, forecast, DateOnly.FromDateTime(now.UtcDateTime));
            return new MilestoneProgress(
                kind, group.First().milestone.Name, due, scope.Count, scope.Count(DeliveryRules.IsDelivered),
                scope.Count(item => !DeliveryRules.IsOpen(item) && !DeliveryRules.IsDelivered(item)), open, outlook, reason, forecast);
        }).ToList();

        var unassigned = items.Where(item => item.Milestones is null || item.Milestones.Count == 0).ToList();
        return (milestones, new UnassignedItems(kind, unassigned.Count, unassigned.Count(DeliveryRules.IsOpen)));
    }

    /// <summary>
    /// Compara o prazo com a previsão do que falta. Com tudo concluído, nada a prever. Com o prazo já passado e itens abertos, o
    /// marco está vencido, haja ou não previsão. Com prazo, vale o cenário conservador (85% das simulações) para dizer
    /// que está no prazo, e o provável para dizer que está em risco; se nem o provável cabe, está atrasado em relação ao prazo.
    /// </summary>
    public static (string Outlook, string Reason) Outlook(DateOnly? due, int open, DeliveryForecast forecast, DateOnly today)
    {
        if (open == 0) return (MilestoneOutlook.Done, "Todos os itens do marco foram concluídos.");
        if (due is { } deadline && deadline < today)
            return (MilestoneOutlook.Overdue, $"O prazo do marco passou e ainda há {open} {(open == 1 ? "item aberto" : "itens abertos")}.");
        if (forecast.Unavailable is { } missing) return (MilestoneOutlook.NoForecast, missing);
        if (due is null) return (MilestoneOutlook.NoDueDate, "O marco não tem prazo no provedor; a previsão está nos cenários.");

        var likely = forecast.Scenarios.Single(scenario => scenario.Id == "likely").By;
        var conservative = forecast.Scenarios.Single(scenario => scenario.Id == "conservative").By;
        if (conservative <= due) return (MilestoneOutlook.OnTrack, "Até o cenário conservador (85% das simulações) termina antes do prazo.");
        if (likely <= due) return (MilestoneOutlook.AtRisk, "O cenário provável cabe no prazo, mas o conservador não.");
        return (MilestoneOutlook.Behind, "Nem o cenário provável cabe no prazo.");
    }
}
