namespace Mariliz.Application.Models;

/// <summary>Para que o modelo é chamado, como a pessoa entende: o que ele faz na prática, não o nome interno do agente.</summary>
public sealed record ModelPurpose(string Id, string Label, string Description);

/// <summary>
/// As finalidades de modelo que a pessoa pode escolher. Cada chamada do motor declara a sua, e a escolha de modelo vale por
/// finalidade: um modelo mais forte para o juiz, um mais barato para classificar, sem que o agente decida qual usar.
/// </summary>
public static class ModelPurposes
{
    public const string Assistant = "assistant";
    public const string Judge = "judge";
    public const string Workflow = "workflow";

    public static IReadOnlyList<ModelPurpose> All { get; } =
    [
        new(Assistant, "Assistente gerencial", "Responde perguntas sobre os indicadores do portfólio, só com os fatos que o motor calculou."),
        new(Judge, "Juiz", "Dá uma segunda opinião sobre o resultado de uma execução. Não decide sozinho: quem decide é a verificação do motor."),
        new(Workflow, "Fluxos de engenharia", "Classifica o pedido, planeja, desenha a arquitetura e reformula buscas nos fluxos de engenharia.")
    ];

    public static ModelPurpose? Find(string? id) => All.FirstOrDefault(purpose => string.Equals(purpose.Id, id, StringComparison.Ordinal));

    /// <summary>
    /// A finalidade de uma chamada do runtime de IA, que declara a sua em termos de agente (<c>classify</c>, <c>plan</c>,
    /// <c>architecture</c>, <c>judge</c>, <c>rewrite-query</c>). Só o juiz tem finalidade própria; o resto é fluxo de engenharia.
    /// </summary>
    public static string ForBrokerPurpose(string brokerPurpose) => brokerPurpose == "judge" ? Judge : Workflow;
}

/// <summary>O modelo que a pessoa escolheu para cada finalidade, só no diretório de dados do motor. Sem escolha, vale o padrão do provedor.</summary>
public interface IModelPreferences
{
    /// <summary>Finalidade e modelo escolhido. Só o que foi escolhido: o que não está aqui usa o padrão.</summary>
    IReadOnlyDictionary<string, string> Read();

    /// <summary>Guarda a escolha de uma finalidade; nulo volta ao padrão.</summary>
    void Write(string purpose, string? model);

    /// <summary>Volta tudo ao padrão.</summary>
    void Clear();
}
