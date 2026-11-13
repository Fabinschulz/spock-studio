namespace Mariliz.Application.Management;

public sealed record PortfolioSignal(string Id, string Message);

/// <param name="Overdue">Nulo quando a fonte não informa prazo.</param>
public sealed record KindSnapshot(string Kind, int Items, int Open, int InProgress, int DeliveredInPeriod, int? Overdue);

/// <param name="OldestReadAt">A leitura mais antiga entre as fontes com dados: a linha é tão atual quanto ela.</param>
public sealed record PortfolioProject(
    string ProjectId,
    string DisplayName,
    string DisplayPath,
    int Sources,
    IReadOnlyList<KindSnapshot> Kinds,
    DateTimeOffset? OldestReadAt,
    IReadOnlyList<PortfolioSignal> Signals);

public sealed record Portfolio(DateTimeOffset GeneratedAt, int PeriodWeeks, IReadOnlyList<PortfolioProject> Projects);
