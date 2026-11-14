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

    // Só execuções que gravam no projeto com backup podem ser desfeitas.
    public static bool IsRevertible(string kind) => kind is Apply or Security;
}
