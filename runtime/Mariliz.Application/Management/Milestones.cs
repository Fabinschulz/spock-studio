namespace Mariliz.Application.Management;

/// <summary>A leitura do prazo de um marco contra a previsão do que falta nele. Texto fixo, usado pelo motor e pela interface.</summary>
public static class MilestoneOutlook
{
    public const string Done = "done";
    public const string Overdue = "overdue";
    public const string NoForecast = "no_forecast";
    public const string NoDueDate = "no_due_date";
    public const string OnTrack = "on_track";
    public const string AtRisk = "at_risk";
    public const string Behind = "behind";
}

/// <summary>
/// O andamento de um marco (milestone do GitHub ou versão de correção do Jira) dentro de um tipo de dado.
/// </summary>
/// <param name="Items">Todos os itens do marco.</param>
/// <param name="Delivered">Os que contam como entregues.</param>
/// <param name="ClosedWithoutDelivery">Concluídos que não contam como entrega: descartados, duplicados, pull request fechado sem merge ou sem data de conclusão.</param>
/// <param name="Open">Os que ainda não foram concluídos.</param>
/// <param name="Outlook">Um de <see cref="MilestoneOutlook"/>.</param>
/// <param name="Forecast">A previsão do que está aberto no marco, com o ritmo do tipo de dado inteiro.</param>
public sealed record MilestoneProgress(
    string Kind,
    string Name,
    DateOnly? DueDate,
    int Items,
    int Delivered,
    int ClosedWithoutDelivery,
    int Open,
    string Outlook,
    string OutlookReason,
    DeliveryForecast Forecast);

/// <summary>Itens de um tipo de dado que não pertencem a nenhum marco.</summary>
public sealed record UnassignedItems(string Kind, int Items, int Open);

/// <param name="Omitted">Marcos que ficaram de fora da lista por passar do limite de <see cref="MilestoneCalculator.MaxMilestones"/>.</param>
public sealed record ProjectMilestones(
    string ProjectId,
    DateTimeOffset GeneratedAt,
    bool Complete,
    IReadOnlyList<MilestoneProgress> Milestones,
    int Omitted,
    IReadOnlyList<UnassignedItems> Unassigned,
    IReadOnlyList<string> Assumptions,
    IReadOnlyList<string> Limitations);
