namespace Mariliz.Application.Workspace;

public sealed record WorkspaceResult(AuthorizedProject? Project, WorkspaceError? Error)
{
    public static WorkspaceResult Ok(AuthorizedProject project) => new(project, null);
    public static WorkspaceResult Fail(WorkspaceError error) => new(null, error);
}
