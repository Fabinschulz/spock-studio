namespace Mariliz.Application.Management;

/// <summary>Um ponto de atenção detectado por regra fixa. Não é previsão nem opinião: cada um tem a condição escrita em <see cref="PortfolioSignals"/>.</summary>
public sealed record PortfolioSignal(string Id, string Message);

/// <summary>A situação de um tipo de dado num projeto, para comparar projetos lado a lado.</summary>
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

/// <summary>Todos os projetos autorizados neste computador. Cada linha é calculada só com os dados do próprio projeto.</summary>
public sealed record Portfolio(DateTimeOffset GeneratedAt, int PeriodWeeks, IReadOnlyList<PortfolioProject> Projects);
