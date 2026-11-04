namespace Mariliz.Application.Protocol;

public static class ProtocolMethods
{
    public const string Hello = "engine.hello";
    public const string Shutdown = "engine.shutdown";
    public const string AuthorizeWorkspace = "workspace.authorize";

    public const string Discover = "project.discover";
    public const string Plan = "harness.plan";
    public const string Apply = "harness.apply";
    public const string Validate = "harness.validate";
    public const string Revert = "harness.revert";
    public const string Runs = "harness.runs";
    public const string Snapshot = "run.snapshot";
    public const string Cancel = "run.cancel";

    public const string WorkflowStart = "workflow.start";
    public const string WorkflowResume = "workflow.resume";
    public const string RuntimeHealth = "runtime.health";

    public const string ModelStatus = "model.status";
    public const string ModelSave = "model.save";
    public const string ModelClear = "model.clear";
    public const string ModelComplete = "model.complete";

    public static readonly IReadOnlyList<string> Configure =
        [Discover, Plan, Apply, Validate, Revert, Runs];

    /// <summary>Consulta e cancelamento valem para execuções de qualquer jornada.</summary>
    public static readonly IReadOnlyList<string> RunControl = [Snapshot, Cancel];

    public static readonly IReadOnlyList<string> Workflow = [WorkflowStart, WorkflowResume];

    public static readonly IReadOnlyList<string> Model = [ModelStatus, ModelSave, ModelClear, ModelComplete];
}
