using System.Text;
using System.Text.Json.Nodes;
using Mariliz.Application.Compatibility;
using Mariliz.Application.Integrations;
using Mariliz.Application.Ports;
using Mariliz.Application.Scaffold;
using Mariliz.Application.Workspace;

namespace Mariliz.Application.Mcp;

// MV-6 no modo Sessão: o Spock monta o plano de criação a partir da arquitetura aprovada e das versões da política; o agente
// executa e confere aqui. As ferramentas só leem: quem roda os comandos é o agente, com as permissões da sessão dele.
public sealed class ProjectScaffoldTools(Func<WorkspaceAuthorizer> workspaces, IProjectFilesFactory files, string workingDirectory,
    IReadOnlySet<string> scope, IClock clock, IAuditLog? audit = null, ReleaseCatalog? catalog = null)
{
    public const string PrepareTool = "spock_project_scaffold_prepare";
    public const string CheckTool = "spock_project_scaffold_check";
    private const int MaxArgument = 200;

    private static readonly JsonObject ProjectOnly = new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["project"] = new JsonObject
            {
                ["type"] = "string",
                ["description"] = "Nome ou id do projeto, como em list_projects. Sem ele, vale o projeto da pasta atual."
            }
        },
        ["additionalProperties"] = false
    };

    public static readonly McpToolInfo Prepare = new(
        PrepareTool, "Preparar a criação do projeto",
        "Para o agente criar o esqueleto do projeto a partir da arquitetura aprovada em Da ideia ao projeto: devolve as versões " +
        "escolhidas pela política do Spock, com o motivo, os comandos oficiais em ordem, o registro spock-versions.json e a " +
        "verificação. Só leitura: quem roda os comandos é o agente.",
        ProjectOnly.DeepClone().AsObject());

    public static readonly McpToolInfo Check = new(
        CheckTool, "Conferir a criação do projeto",
        "Confere o que o agente criou contra o plano: SDK, alvo e referências do .NET, Node e versões exatas do frontend, e o " +
        "registro spock-versions.json. Build e testes continuam com o agente. Só leitura.",
        ProjectOnly.DeepClone().AsObject());

    public IReadOnlyList<McpTool> Tools() => [new(Prepare, PrepareAsync), new(Check, CheckAsync)];

    private Task<string> PrepareAsync(McpCall call)
    {
        var (project, scaffold, root) = Open(call);
        Audit(project.ProjectId, PrepareTool, call.Client, scaffold.Plan.Steps.Count);
        if (!scaffold.Plan.Supported) return Task.FromResult(Problems(project.DisplayName, scaffold.Plan));
        var text = new StringBuilder($"Criação do projeto {project.DisplayName} ({scaffold.Plan.ProjectName}). O Spock não roda comando nem acessa a rede: você executa, o Spock confere.\n\n");
        text.Append(ScaffoldGuide.For(scaffold.Plan, root));
        if (scaffold.Checks.Any(check => check.Passed))
            text.Append($"\n\nParte do esqueleto já existe ({Status(scaffold)}). Siga só os passos que faltam e não apague o que já foi criado.");
        return Task.FromResult(text.ToString());
    }

    private Task<string> CheckAsync(McpCall call)
    {
        var (project, scaffold, _) = Open(call);
        Audit(project.ProjectId, CheckTool, call.Client, scaffold.Checks.Count);
        if (!scaffold.Plan.Supported) return Task.FromResult(Problems(project.DisplayName, scaffold.Plan));
        var text = new StringBuilder($"Criação do projeto {project.DisplayName}: {Status(scaffold)}.");
        foreach (var check in scaffold.Checks.Where(check => !check.Passed))
            text.Append($"\n- {check.Label}{(check.Detail is null ? "" : $" ({check.Detail})")}");
        text.Append(scaffold.Done
            ? "\nOs arquivos conferem com o plano. Rode a verificação (" + string.Join("; ", scaffold.Plan.Verification.Select(step => $"{step.Directory}: {step.Command}")) +
              ") e conte à pessoa o resultado do build e dos testes."
            : $"\nSiga os passos de {PrepareTool} que faltam e confira de novo.");
        return Task.FromResult(text.ToString());
    }

    private static string Problems(string project, ScaffoldPlan plan) =>
        $"O Spock ainda não cria o projeto {project}:\n" + string.Join("\n", plan.Problems.Select(problem => $"- {problem}"));

    private static string Status(ProjectScaffold scaffold)
    {
        var passed = scaffold.Checks.Count(check => check.Passed);
        return passed == scaffold.Checks.Count ? $"passa nas {passed} verificações" : $"{passed} de {scaffold.Checks.Count} verificações passando";
    }

    private (AuthorizedProject Project, ProjectScaffold Scaffold, string Root) Open(McpCall call)
    {
        var authorizer = workspaces();
        var project = new McpProjectResolver(authorizer, workingDirectory, scope).Resolve(McpArguments.Text(call.Arguments, "project", MaxArgument));
        if (!authorizer.TryGetRoot(project.ProjectId, out var root)) throw new McpToolException("O projeto não está autorizado no Spock.");
        return (project, ScaffoldService.Read(files.Open(root), DateOnly.FromDateTime(clock.UtcNow.UtcDateTime), catalog), root);
    }

    private void Audit(string projectId, string tool, string? client, int items)
    {
        if (audit is null) return;
        var detail = new JsonObject { ["projectId"] = projectId, ["tool"] = tool, ["items"] = items, ["paths"] = new JsonArray(), ["files"] = 0 };
        if (client is not null) detail["client"] = client;
        audit.Record(ProjectContextTools.ReadAction, detail);
    }
}
