using System.Text;
using System.Text.Json.Nodes;
using Mariliz.Application.Harness;
using Mariliz.Application.Ideas;
using Mariliz.Application.Integrations;
using Mariliz.Application.Ports;
using Mariliz.Application.Workspace;

namespace Mariliz.Application.Mcp;

// W4-3 no modo Sessão: o agente escreve a especificação, a arquitetura (com a stack pelo §10 do catálogo) e o backlog num
// arquivo do projeto e confere aqui. Gravar os documentos fica com a pessoa, na tela Da ideia ao projeto.
public sealed class ProjectDesignTools(Func<WorkspaceAuthorizer> workspaces, IProjectFilesFactory files, string workingDirectory,
    IReadOnlySet<string> scope, IAuditLog? audit = null)
{
    public const string PrepareTool = "spock_project_design_prepare";
    public const string CheckTool = "spock_project_design_check";
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
        PrepareTool, "Preparar o projeto a partir da ideia",
        "Para o agente transformar a ideia da pessoa em especificação, arquitetura e backlog sem que o Spock chame modelo: devolve " +
        "o formato de cada etapa, os critérios de escolha de stack do catálogo da Organização (§10) e onde gravar. Só leitura.",
        ProjectOnly.DeepClone().AsObject());

    public static readonly McpToolInfo Check = new(
        CheckTool, "Conferir o projeto a partir da ideia",
        "Confere a especificação, a arquitetura e o backlog que o agente gravou: requisitos verificáveis, componentes e histórias " +
        "que rastreiam até eles, e cada escolha de stack com critério do catálogo, alternativa descartada e trade-off. Só leitura.",
        ProjectOnly.DeepClone().AsObject());

    public IReadOnlyList<McpTool> Tools() => [new(Prepare, PrepareAsync), new(Check, CheckAsync)];

    private Task<string> PrepareAsync(McpCall call)
    {
        var (project, projectFiles, root) = Open(call);
        var design = ProjectDesigns.Read(projectFiles);
        Audit(project.ProjectId, PrepareTool, call.Client, design.Stages.Count);
        var text = new StringBuilder($"Da ideia ao projeto em {project.DisplayName}. O Spock não chama modelo aqui: você escreve, o Spock confere e a pessoa aprova e grava.\n\n");
        text.Append(ProjectDesignGuide.For(FullPath(root, design.Path)));
        if (design.Found)
            text.Append($"\n\nJá existe um arquivo ({Status(design)}). Gravar de novo o substitui.");
        return Task.FromResult(text.ToString());
    }

    private Task<string> CheckAsync(McpCall call)
    {
        var (project, projectFiles, root) = Open(call);
        var design = ProjectDesigns.Read(projectFiles);
        Audit(project.ProjectId, CheckTool, call.Client, design.Stages.Count);
        if (!design.Found)
            return Task.FromResult($"Nenhum arquivo gravado em {FullPath(root, design.Path)}. Grave no formato de {PrepareTool} e chame de novo.");

        var text = new StringBuilder($"Da ideia ao projeto em {project.DisplayName}: {Status(design)}.");
        AppendFailed(text, design.Checks, null);
        foreach (var stage in design.Stages) AppendFailed(text, stage.Checks, StageName(stage.Stage));
        if (!design.Valid) return Task.FromResult(text.Append("\nCorrija o arquivo e confira de novo.").ToString());

        text.Append(Saved(projectFiles, design)
            ? "\nOs documentos em .spock/arquitetura já foram gravados pela pessoa com este conteúdo. Siga a especificação, a arquitetura e o backlog e cite os ids (REQ, CMP, US)."
            : "\nPeça à pessoa para abrir Da ideia ao projeto no Spock, aprovar cada etapa e gravar os documentos. Não grave você os documentos nem crie código antes.");
        return Task.FromResult(text.ToString());
    }

    // Gravado quando os três documentos do projeto são exatamente os que este arquivo daria.
    private static bool Saved(IProjectFiles projectFiles, ProjectDesign design) =>
        ProjectDesigns.Artifacts(design) is { } artifacts
        && IdeaDocuments.Render(artifacts, projectFiles.Naming()).All(document => projectFiles.ReadText(document.Path) == document.Content);

    private static void AppendFailed(StringBuilder text, IReadOnlyList<IdeaCheck> checks, string? stage)
    {
        foreach (var check in checks.Where(check => !check.Passed))
            text.Append($"\n- {(stage is null ? "" : stage + ": ")}{check.Label}{(check.Detail is null ? "" : $" ({check.Detail})")}");
    }

    private static string Status(ProjectDesign design)
    {
        var all = design.Checks.Concat(design.Stages.SelectMany(stage => stage.Checks)).ToList();
        var failed = all.Count(check => !check.Passed);
        return failed == 0 ? $"passa nas {all.Count} verificações das três etapas" : $"{failed} de {all.Count} verificações falhando";
    }

    private static string StageName(string stage) => stage switch
    {
        IdeaStages.Specification => "Especificação",
        IdeaStages.Architecture => "Arquitetura",
        _ => "Backlog"
    };

    // Caminho completo: a sessão do agente pode estar aberta em outra pasta, e um caminho relativo iria parar lá.
    private static string FullPath(string root, string relative) => Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));

    private (AuthorizedProject Project, IProjectFiles Files, string Root) Open(McpCall call)
    {
        var authorizer = workspaces();
        var project = new McpProjectResolver(authorizer, workingDirectory, scope).Resolve(McpArguments.Text(call.Arguments, "project", MaxArgument));
        if (!authorizer.TryGetRoot(project.ProjectId, out var root)) throw new McpToolException("O projeto não está autorizado no Spock.");
        return (project, files.Open(root), root);
    }

    private void Audit(string projectId, string tool, string? client, int items)
    {
        if (audit is null) return;
        var detail = new JsonObject { ["projectId"] = projectId, ["tool"] = tool, ["items"] = items, ["paths"] = new JsonArray(), ["files"] = 0 };
        if (client is not null) detail["client"] = client;
        audit.Record(ProjectContextTools.ReadAction, detail);
    }
}
