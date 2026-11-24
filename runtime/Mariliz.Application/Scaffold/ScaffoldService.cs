using System.Text.Json.Nodes;
using Mariliz.Application.Compatibility;
using Mariliz.Application.Integrations;
using Mariliz.Application.Ports;
using Mariliz.Application.Workspace;

namespace Mariliz.Application.Scaffold;

/// <param name="Done">Todos os arquivos conferem com o plano; build e testes continuam com o agente.</param>
public sealed record ProjectScaffold(ScaffoldPlan Plan, IReadOnlyList<ScaffoldCheck> Checks, bool Done);

/// <param name="catalog">Nulo é o catálogo embutido; os testes passam um fixo, para o plano não mudar a cada geração do catálogo.</param>
public sealed class ScaffoldService(WorkspaceAuthorizer workspace, IProjectFilesFactory filesFactory, IClock clock, ReleaseCatalog? catalog = null)
{
    public ProjectScaffold Read(string projectId) =>
        Read(filesFactory.Open(workspace.RequireRoot(projectId)), DateOnly.FromDateTime(clock.UtcNow.UtcDateTime), catalog);

    public static ProjectScaffold Read(IProjectFiles files, DateOnly today, ReleaseCatalog? catalog = null)
    {
        var plan = ScaffoldPlans.Plan(files, files.RootName, catalog ?? ReleaseCatalog.Embedded, today);
        var checks = ScaffoldChecks.Check(files, plan);
        return new(plan, checks, plan.Supported && checks.All(check => check.Passed));
    }

    public static JsonObject Json(ProjectScaffold scaffold) => IntegrationJson.Node(scaffold)!.AsObject();
}
