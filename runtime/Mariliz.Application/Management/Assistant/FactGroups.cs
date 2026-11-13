namespace Mariliz.Application.Management.Assistant;

public static class FactGroupRoles
{
    /// <summary>Entra sempre no começo da investigação.</summary>
    public const string Base = "base";

    /// <summary>Por onde começar quando a pergunta não indica nenhum grupo.</summary>
    public const string Default = "default";

    /// <summary>Sinal de atenção: sem modelo, a resposta lista estes fatos como o motor os calculou.</summary>
    public const string Sign = "sign";
}

public sealed record FactGroup(string Name, IReadOnlyList<string> Roles, IReadOnlyList<string> Keywords);

/// <summary>
/// Os grupos de fatos que o motor manda à investigação. O runtime de IA não conhece nomes de grupo: recebe estes descritores
/// no pedido (<c>ai.investigation.start.v2</c>). As palavras-chave vão sem acento e em minúsculas, como a pergunta é comparada.
/// </summary>
public static class FactGroups
{
    public const string Context = "contexto";
    public const string Sources = "fontes";
    public const string NotCalculated = "não calculado";
    public const string Flow = "indicadores";
    public const string Forecast = "previsão";
    public const string Risks = "riscos";
    public const string Milestones = "marcos";
    public const string Runs = "execuções";
    public const string Changes = "mudanças";

    public static readonly IReadOnlyList<FactGroup> All =
    [
        new(Context, [FactGroupRoles.Base], []),
        new(Sources, [FactGroupRoles.Base], ["sincroniz", "fonte", "dados", "atualizad"]),
        new(NotCalculated, [FactGroupRoles.Base], []),
        new(Flow, [FactGroupRoles.Default],
            ["vazao", "throughput", "entreg", "lead time", "cycle", "fluxo", "andamento", "wip", "caiu", "queda", "ritmo", "velocidade", "lento", "devagar"]),
        new(Forecast, [], ["previsao", "prazo", "quando", "terminar", "estimativa", "atras"]),
        new(Risks, [FactGroupRoles.Default, FactGroupRoles.Sign], ["risco", "bloque", "parad", "atras", "vencid", "problema", "gargalo"]),
        new(Milestones, [], ["marco", "release", "versao", "milestone"]),
        new(Runs, [FactGroupRoles.Sign], ["execucao", "execucoes", "falha", "falhou", "erro", "agente", "agentes", "pipeline"]),
        new(Changes, [], ["mudou", "mudanca", "alteracao", "desde", "passou a", "depois de"])
    ];

    /// <summary>Os descritores dos grupos que aparecem nos fatos, na ordem do catálogo.</summary>
    public static IReadOnlyList<FactGroup> For(IEnumerable<EvidenceFact> facts)
    {
        var present = facts.Select(fact => fact.Group).ToHashSet(StringComparer.Ordinal);
        return All.Where(group => present.Contains(group.Name)).ToList();
    }
}
