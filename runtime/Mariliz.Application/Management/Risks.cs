namespace Mariliz.Application.Management;

public enum RiskSeverity
{
    Low,
    Medium,
    High
}

/// <summary>Um item citado como evidência de um risco, como o provedor o identifica.</summary>
public sealed record RiskItem(string Kind, string ExternalId, string Title);

/// <summary>
/// Um risco detectado por regra fixa sobre os dados sincronizados. <see cref="Origin"/> é sempre <c>rule</c> nesta
/// versão; quando houver risco sugerido por análise de IA, ele virá com outra origem e nunca misturado a estes.
/// </summary>
/// <param name="Evidence">Os números observados que dispararam a regra.</param>
/// <param name="Items">Até <see cref="RiskRules.MaxItems"/> itens que sustentam o risco.</param>
/// <param name="Trend">Como o indicador por trás do risco evoluiu, quando a regra compara períodos.</param>
public sealed record ProjectRisk(
    string Id,
    string Title,
    RiskSeverity Severity,
    string Rule,
    IReadOnlyList<string> Evidence,
    IReadOnlyList<RiskItem> Items,
    string? Trend,
    string Recommendation,
    string Origin);

/// <param name="RulesEvaluated">Todas as regras conferidas, inclusive as que não dispararam.</param>
public sealed record ProjectRisks(
    string ProjectId,
    DateTimeOffset GeneratedAt,
    bool Complete,
    bool HasData,
    IReadOnlyList<ProjectRisk> Risks,
    IReadOnlyList<string> RulesEvaluated);
