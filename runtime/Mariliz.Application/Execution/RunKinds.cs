namespace Mariliz.Application.Execution;

public static class RunKinds
{
    public const string Apply = "apply";
    public const string Revert = "revert";
    public const string Validate = "validate";
    public const string Workflow = "workflow";
    public const string Assistant = "assistant";
    public const string Investigation = "investigation";
    public const string Agent = "agent";
    public const string Security = "security";
    public const string Idea = "idea";
    // Arquivos de infraestrutura gravados com aprovação (bootstrap do state); validar, planejar e aplicar têm histórico próprio.
    public const string Infrastructure = "infrastructure";

    // Só execuções que gravam no projeto pelo diário podem ser desfeitas; o workflow grava só o arquivo novo da ação aprovada.
    public static bool IsRevertible(string kind) => kind is Apply or Security or Idea or Workflow or Infrastructure;
}
