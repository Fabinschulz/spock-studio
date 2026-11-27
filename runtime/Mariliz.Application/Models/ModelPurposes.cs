namespace Mariliz.Application.Models;

public sealed record ModelPurpose(string Id, string Label, string Description);

// A escolha de modelo vale por finalidade (um mais forte para o juiz, um mais barato para classificar) sem que o agente decida qual usar.
public static class ModelPurposes
{
    public const string Assistant = "assistant";
    public const string Judge = "judge";
    public const string Workflow = "workflow";
    public const string Boot = "boot";

    public static IReadOnlyList<ModelPurpose> All { get; } =
    [
        new(Assistant, "Assistente gerencial", "Responde perguntas sobre os indicadores do portfólio e investiga, em etapas, o que explica um resultado, só com os fatos que o motor calculou."),
        new(Judge, "Juiz", "Dá uma segunda opinião sobre o resultado de uma execução. Não decide sozinho: quem decide é a verificação do motor."),
        new(Workflow, "Fluxos de engenharia", "Classifica o pedido, planeja, desenha a arquitetura e reformula buscas nos fluxos de engenharia."),
        new(Boot, "Entendimento do pedido", "Segunda tentativa de entender um pedido do Início quando as palavras do Spock não bastam. Recebe só o texto do pedido, sem segredos, e a lista de capacidades; só aponta qual serve. Um modelo pequeno basta e responde mais rápido.")
    ];

    public static ModelPurpose? Find(string? id) => All.FirstOrDefault(purpose => string.Equals(purpose.Id, id, StringComparison.Ordinal));

    // O runtime declara a finalidade em termos de agente (classify, plan, architecture, judge, rewrite-query, investigate). O juiz e a investigação de gestão têm finalidade própria; o resto é fluxo de engenharia.
    public static string ForBrokerPurpose(string brokerPurpose) => brokerPurpose switch
    {
        "judge" => Judge,
        "investigate" => Assistant,
        _ => Workflow
    };
}

// Só no diretório de dados do motor. Sem escolha, vale o padrão do provedor.
public interface IModelPreferences
{
    IReadOnlyDictionary<string, string> Read();

    // Nulo volta ao padrão.
    void Write(string purpose, string? model);

    void Clear();
}
