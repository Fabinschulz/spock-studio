using System.Text.RegularExpressions;

namespace Mariliz.Application.Boot;

public sealed class BootStep
{
    public string Id { get; set; } = "";
    public string Kind { get; set; } = StepKinds.Check;
    public string Title { get; set; } = "";
    public string? CapabilityId { get; set; }
    public string? Method { get; set; }
    public List<string> DependsOn { get; set; } = [];
    public OperationClass Operation { get; set; } = OperationClass.Read;
    public string Status { get; set; } = StepStatuses.Pending;
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public string Summary { get; set; } = "";
    // O que o Boot tirou do pedido para a capacidade (período, quantidade). Só nomes que a ficha declara; ao executar, de novo dentro da faixa.
    public Dictionary<string, int> Arguments { get; set; } = [];
    public List<string> ArgumentNotes { get; set; } = [];
    // Linhas que o motor leu do resultado da capacidade; o texto nunca é escrito por modelo.
    public List<string> Evidence { get; set; } = [];
    public string? ErrorCode { get; set; }
}

public sealed class BootPlan
{
    public string Objective { get; set; } = "";
    public string? Domain { get; set; }
    public List<string> Capabilities { get; set; } = [];
    public OperationClass Operation { get; set; } = OperationClass.Read;
    public AutonomyLevel RequiredLevel { get; set; } = AutonomyLevel.Observation;
    public List<string> Approvals { get; set; } = [];
    public List<string> Risks { get; set; } = [];
    public List<string> SuccessCriteria { get; set; } = [];
    public string ExpectedResult { get; set; } = "";
}

public sealed class BootAlternative
{
    public string CapabilityId { get; set; } = "";
    public string Name { get; set; } = "";
    public string Reason { get; set; } = "";
}

public sealed class BootResult
{
    public string Outcome { get; set; } = BootOutcomes.Prepared;
    public string Headline { get; set; } = "";
    public List<string> Done { get; set; } = [];
    public List<string> Consulted { get; set; } = [];
    public List<string> Changes { get; set; } = [];
    public List<string> Validations { get; set; } = [];
    public List<string> Problems { get; set; } = [];
    public List<string> Pending { get; set; } = [];
    public string? NextStep { get; set; }
    public string? Route { get; set; }
    public string? RouteLabel { get; set; }
    public List<BootAlternative> Alternatives { get; set; } = [];
}

// O gasto de modelo do reconhecimento de intenção (segundo estágio); o Boot não é uma execução, então o consumo fica no registro dele.
public sealed record BootModelUsage(int Calls, int InputTokens, int OutputTokens);

public sealed class BootCheckpoint
{
    public DateTimeOffset At { get; set; }
    public string State { get; set; } = "";
    public string? StepId { get; set; }
    public string Note { get; set; } = "";
}

// Gravado a cada transição e a cada passo concluído, então sobrevive a uma queda; é o que o Boot de recuperação lê.
public sealed class BootRecord
{
    public string Id { get; set; } = "";
    // Sobe a cada gravação: quem lê sabe se o que tem é o mais novo.
    public int Version { get; set; }
    public BootMode Mode { get; set; } = BootMode.Request;
    // Quem iniciou: "ui", "mcp", "trigger"... Só o tipo, nunca a pessoa (o produto não tem identidade).
    public string Origin { get; set; } = "ui";
    public string? Source { get; set; }
    public string? ProjectId { get; set; }
    public string? StableProjectId { get; set; }
    public string State { get; set; } = BootStates.Created;
    public AutonomyLevel Autonomy { get; set; } = AutonomyLevel.Observation;
    public string? Request { get; set; }
    // O pedido tinha uma linha com cara de segredo: ela foi omitida antes de reconhecer a intenção e de gravar o registro.
    public bool RequestRedacted { get; set; }
    public string? Hint { get; set; }
    public IntentResult? Intent { get; set; }
    // Só no Boot que chamou o modelo; a recuperação não copia, para o mesmo gasto não contar duas vezes.
    public BootModelUsage? ModelUsage { get; set; }
    public BootPlan? Plan { get; set; }
    public List<BootStep> Steps { get; set; } = [];
    public List<BootCheckpoint> Checkpoints { get; set; } = [];
    public BootResult? Result { get; set; }
    // O contexto do projeto no começo: o Boot de recuperação compara com o de agora para dizer o que mudou.
    public string? ContextVersion { get; set; }
    public string? RecoversBootId { get; set; }
    public string? RecoveredByBootId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public string? ErrorCode { get; set; }
}

public interface IBootStore
{
    void Save(BootRecord record);

    // projectId nulo é o Boot sem projeto (portfólio, conexões, modelos).
    BootRecord? Find(string? projectId, string bootId);

    // Do mais novo para o mais antigo.
    IReadOnlyList<BootRecord> List(string? projectId);

    // Todos os projetos, para achar o que a queda anterior deixou em andamento.
    IReadOnlyList<BootRecord> ListAll();

    // Sem ler nenhum arquivo: é o que decide se vale podar.
    int Count(string? projectId);

    // Falso quando não havia o que apagar.
    bool Delete(string? projectId, string bootId);
}

public static partial class BootIds
{
    public const string Prefix = "boot-";

    public static string New() => Prefix + Guid.NewGuid().ToString("N")[..12];

    // O id vira nome de arquivo: só o formato exato passa.
    public static bool IsValid(string? value) => value is not null && IdPattern().IsMatch(value);

    [GeneratedRegex("^boot-[0-9a-f]{12}$")]
    private static partial Regex IdPattern();
}
