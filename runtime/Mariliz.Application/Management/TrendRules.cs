namespace Mariliz.Application.Management;

/// <summary>
/// As duas perguntas de tendência que a previsão e as regras de risco fazem sobre as mesmas contagens semanais, com os limiares
/// num lugar só. O que muda entre elas é a janela (a previsão olha o histórico todo, de 6 a 12 semanas; a regra de risco, as
/// últimas 4), nunca a conta. Os dois níveis de escopo crescendo são decisão do dono do produto (2026-11-13): a previsão avisa
/// antes (atenção) e a lista de riscos só quando é forte (risco).
/// </summary>
public static class TrendRules
{
    /// <summary>Menos que isso de diferença é oscilação normal, em qualquer nível.</summary>
    public const int MinScopeGap = 3;

    /// <summary>
    /// Nível de atenção: chegadas 20% acima das entregas. A previsão o usa para baixar a confiança e listar o fator ("Fatores que
    /// podem atrasar"). Não é um risco: não entra na lista de riscos.
    /// </summary>
    public const double ScopeWatchRatio = 1.2;

    /// <summary>Nível de risco: chegadas 50% acima das entregas nas últimas semanas. É o que entra na lista de riscos (<c>scope-growth</c>).</summary>
    public const double ScopeRiskRatio = 1.5;

    /// <summary>Entregas mínimas antes da queda para ela valer como queda (com menos, não há de onde cair).</summary>
    public const int MinDeliveriesBeforeDrop = 4;

    public static bool ScopeGrowing(int arrivals, int deliveries, double ratio) =>
        arrivals - deliveries >= MinScopeGap && arrivals >= deliveries * ratio;

    public static bool ThroughputDropped(int before, int after) => before >= MinDeliveriesBeforeDrop && after * 2 <= before;
}
