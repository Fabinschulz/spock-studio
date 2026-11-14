using System.Text.Json.Nodes;
using Mariliz.Application.Engineering;
using Mariliz.Application.Execution;
using Mariliz.Application.Management;
using Mariliz.Application.Ports;
using Mariliz.Application.Workspace;

namespace Mariliz.Application.Mcp;

// O registro de pastas é relido a cada chamada: um projeto aberto no Spock depois de a IDE iniciar o servidor já aparece.
public sealed class ProjectContextTools(
    Func<WorkspaceAuthorizer> workspaces,
    IProjectFilesFactory files,
    IClock clock,
    ITechnologyProfileStore profiles,
    IDecisionStore decisions,
    string workingDirectory)
{
    public const int DefaultBriefingChars = 6000;
    public const int MinChars = 1000;
    public const int MaxChars = 20000;
    public const int DefaultDecisions = 10;
    public const int MaxDecisions = 50;
    private const int MaxProjectArgument = 200;
    private const int MaxQuery = 200;

    private static readonly JsonObject ProjectArgument = new()
    {
        ["type"] = "string",
        ["description"] = "Nome ou id do projeto, como em list_projects. Sem ele, vale o projeto da pasta atual."
    };

    public static readonly McpToolInfo ListProjects = new(
        "list_projects", "Projetos abertos no Spock",
        "Lista os projetos que a pessoa abriu no Spock Studio e marca o da pasta atual. Só eles têm contexto disponível.",
        new JsonObject { ["type"] = "object", ["properties"] = new JsonObject(), ["additionalProperties"] = false });

    public static readonly McpToolInfo Briefing = new(
        "project_briefing", "Resumo do projeto",
        "Resumo compacto do projeto: tecnologias e versões, comandos de build e teste, estado do Harness e decisões recentes. " +
        "Use antes de planejar uma mudança.",
        new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject
            {
                ["project"] = ProjectArgument.DeepClone(),
                ["max_chars"] = new JsonObject
                {
                    ["type"] = "integer", ["minimum"] = MinChars, ["maximum"] = MaxChars,
                    ["description"] = $"Tamanho máximo da resposta, em caracteres (padrão {DefaultBriefingChars})."
                }
            },
            ["additionalProperties"] = false
        });

    public static readonly McpToolInfo Decisions = new(
        "decisions", "Decisões registradas",
        "Decisões que a equipe registrou no Spock, das mais recentes para as mais antigas, com o que se decidiu e por quê. " +
        "Filtre por texto com 'query'.",
        new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject
            {
                ["project"] = ProjectArgument.DeepClone(),
                ["query"] = new JsonObject { ["type"] = "string", ["maxLength"] = MaxQuery, ["description"] = "Texto que a decisão precisa conter." },
                ["limit"] = new JsonObject
                {
                    ["type"] = "integer", ["minimum"] = 1, ["maximum"] = MaxDecisions,
                    ["description"] = $"Quantas decisões trazer (padrão {DefaultDecisions})."
                }
            },
            ["additionalProperties"] = false
        });

    public static IReadOnlyList<McpToolInfo> Catalog { get; } = [ListProjects, Briefing, Decisions];

    public IReadOnlyList<McpTool> Tools() =>
    [
        new(ListProjects, _ => RenderProjects()),
        new(Briefing, RenderBriefing),
        new(Decisions, RenderDecisions)
    ];

    private string RenderProjects()
    {
        var resolver = new McpProjectResolver(workspaces(), workingDirectory);
        var projects = resolver.Projects();
        if (projects.Count == 0)
            return "Nenhum projeto aberto no Spock ainda. Abra a pasta do projeto no aplicativo para liberar o contexto dele.";
        var current = resolver.Current()?.ProjectId;
        return string.Join("\n", projects
            .Select(project => $"- {project.DisplayName} · id {project.ProjectId} · {project.DisplayPath}{(project.ProjectId == current ? " (pasta atual)" : "")}")
            .Prepend("Projetos abertos no Spock:"));
    }

    private string RenderBriefing(JsonObject args)
    {
        var maxChars = McpArguments.Number(args, "max_chars", DefaultBriefingChars, MinChars, MaxChars);
        var workspace = workspaces();
        var project = new McpProjectResolver(workspace, workingDirectory).Resolve(McpArguments.Text(args, "project", MaxProjectArgument));
        var sections = ProjectBriefing.Sections(project, profiles.Read(project.ProjectId), Harness(workspace, project.ProjectId),
            new DecisionMemory(workspace, decisions).Documents(project.ProjectId));
        return McpText.Fit(sections, maxChars, "Peça de novo com max_chars maior se precisar do resto.");
    }

    private string RenderDecisions(JsonObject args)
    {
        var limit = McpArguments.Number(args, "limit", DefaultDecisions, 1, MaxDecisions);
        var query = McpArguments.Text(args, "query", MaxQuery);
        var workspace = workspaces();
        var project = new McpProjectResolver(workspace, workingDirectory).Resolve(McpArguments.Text(args, "project", MaxProjectArgument));
        var found = new DecisionMemory(workspace, decisions).Documents(project.ProjectId)
            .Where(item => query is null || item.Text.Contains(query, StringComparison.OrdinalIgnoreCase))
            .Take(limit)
            .Select(item => item.Text)
            .ToList();
        if (found.Count == 0)
            return query is null ? $"Nenhuma decisão registrada em {project.DisplayName}." : $"Nenhuma decisão de {project.DisplayName} fala de '{query}'.";
        return McpText.Fit(found, MaxChars, "Use 'query' ou um 'limit' menor para trazer o que falta.");
    }

    private HarnessStatus? Harness(WorkspaceAuthorizer workspace, string projectId)
    {
        try
        {
            return new HarnessStatusService(workspace, files, clock).Inspect(projectId);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
