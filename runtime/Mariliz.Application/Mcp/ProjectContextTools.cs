using System.Text.Json.Nodes;
using Mariliz.Application.Engineering;
using Mariliz.Application.Execution;
using Mariliz.Application.Integrations;
using Mariliz.Application.Management;
using Mariliz.Application.Ports;
using Mariliz.Application.Protocol;
using Mariliz.Application.Workflows;
using Mariliz.Application.Workspace;

namespace Mariliz.Application.Mcp;

// O registro de pastas é relido a cada chamada: um projeto aberto no Spock depois de a IDE iniciar o servidor já aparece.
public sealed class ProjectContextTools(
    Func<WorkspaceAuthorizer> workspaces,
    IProjectFilesFactory files,
    IClock clock,
    ITechnologyProfileStore profiles,
    IDecisionStore decisions,
    string workingDirectory,
    IKnowledgeSearchRuntime? search = null,
    string? indexRoot = null,
    IAuditLog? audit = null,
    IReadOnlySet<string>? scope = null,
    Func<WorkspaceAuthorizer, ManagementService>? management = null)
{
    public const int DefaultBriefingChars = 6000;
    public const int MinChars = 1000;
    public const int MaxChars = 20000;
    public const int DefaultDecisions = 10;
    public const int MaxDecisions = 50;
    public const int DefaultResults = 5;
    public const int MaxResults = 10;
    public const string ReadAction = "ide.context_read";
    // A trilha guarda os caminhos lidos, não o conteúdo; acima disso, só a contagem.
    public const int MaxAuditedPaths = 20;
    private const int MaxProjectArgument = 200;
    private const int MaxQuery = 200;
    private const int MaxSearchQuery = 300;
    private const string SearchSchema = "ai.knowledge.search.v1";
    private const string SearchAck = "ai.knowledge.search.ack.v1";

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
        "Resumo compacto do projeto: tecnologias e versões, comandos de build e teste, estado do Harness, integrações vinculadas, " +
        "indicadores de entrega, riscos e marcos calculados pelo Spock, e decisões recentes. " +
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

    public static readonly McpToolInfo Search = new(
        "search_project", "Busca no índice do projeto",
        "Trechos do código, da documentação e das decisões do projeto que falam do assunto, do mais ao menos relevante, com arquivo e linha. " +
        "Use nomes de classes, métodos, ids de requisito ou termos do domínio. Mostra o que o Spock indexou da última vez e avisa quando o arquivo mudou depois.",
        new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject
            {
                ["query"] = new JsonObject { ["type"] = "string", ["maxLength"] = MaxSearchQuery, ["description"] = "O que procurar." },
                ["project"] = ProjectArgument.DeepClone(),
                ["limit"] = new JsonObject
                {
                    ["type"] = "integer", ["minimum"] = 1, ["maximum"] = MaxResults,
                    ["description"] = $"Quantos trechos trazer (padrão {DefaultResults})."
                }
            },
            ["required"] = new JsonArray("query"),
            ["additionalProperties"] = false
        });

    public static IReadOnlyList<McpToolInfo> Catalog { get; } = [ListProjects, Briefing, Decisions, Search];

    public IReadOnlyList<McpTool> Tools() =>
    [
        new(ListProjects, _ => RenderProjects()),
        new(Briefing, call => Task.FromResult(RenderBriefing(call))),
        new(Decisions, call => Task.FromResult(RenderDecisions(call))),
        new(Search, RenderSearchAsync)
    ];

    private string RenderProjects()
    {
        var resolver = new McpProjectResolver(workspaces(), workingDirectory, scope);
        var projects = resolver.Projects();
        if (projects.Count == 0)
            return scope is null
                ? "Nenhum projeto aberto no Spock ainda. Abra a pasta do projeto no aplicativo para liberar o contexto dele."
                : "Nenhum dos projetos desta conexão está aberto no Spock. Abra a pasta no aplicativo ou ajuste o --project da configuração.";
        var current = resolver.Current()?.ProjectId;
        return string.Join("\n", projects
            .Select(project => $"- {project.DisplayName} · id {project.ProjectId} · {project.DisplayPath}{(project.ProjectId == current ? " (pasta atual)" : "")}")
            .Prepend(scope is null ? "Projetos abertos no Spock:" : "Projetos disponíveis para esta conexão:"));
    }

    private string RenderBriefing(McpCall call)
    {
        var maxChars = McpArguments.Number(call.Arguments, "max_chars", DefaultBriefingChars, MinChars, MaxChars);
        var workspace = workspaces();
        var project = Resolve(workspace, call.Arguments);
        var sections = ProjectBriefing.Sections(project, profiles.Read(project.ProjectId), Harness(workspace, project.ProjectId),
            new DecisionMemory(workspace, decisions).Documents(project.ProjectId), Management(workspace, project.ProjectId),
            management is not null).ToList();
        Audit(project.ProjectId, Briefing.Name, call.Client, sections.Count, []);
        return McpText.Fit(sections, maxChars, "Peça de novo com max_chars maior se precisar do resto.");
    }

    private string RenderDecisions(McpCall call)
    {
        var limit = McpArguments.Number(call.Arguments, "limit", DefaultDecisions, 1, MaxDecisions);
        var query = McpArguments.Text(call.Arguments, "query", MaxQuery);
        var workspace = workspaces();
        var project = Resolve(workspace, call.Arguments);
        var found = new DecisionMemory(workspace, decisions).Documents(project.ProjectId)
            .Where(item => query is null || item.Text.Contains(query, StringComparison.OrdinalIgnoreCase))
            .Take(limit)
            .Select(item => item.Text)
            .ToList();
        if (found.Count == 0)
            return query is null ? $"Nenhuma decisão registrada em {project.DisplayName}." : $"Nenhuma decisão de {project.DisplayName} fala de '{query}'.";
        Audit(project.ProjectId, Decisions.Name, call.Client, found.Count, []);
        return McpText.Fit(found, MaxChars, "Use 'query' ou um 'limit' menor para trazer o que falta.");
    }

    // O índice pode ter texto de quando as regras eram outras: cada trecho é conferido de novo contra as regras de conhecimento de agora
    // antes de sair. Fontes externas (Jira, GitHub) ficam de fora: a política delas vale para os agentes do Spock, não para a IDE.
    private async Task<string> RenderSearchAsync(McpCall call)
    {
        var query = McpArguments.Text(call.Arguments, "query", MaxSearchQuery) ?? throw new McpToolException("Informe 'query' com o que procurar no projeto.");
        var limit = McpArguments.Number(call.Arguments, "limit", DefaultResults, 1, MaxResults);
        var workspace = workspaces();
        var project = Resolve(workspace, call.Arguments);
        if (search is null || indexRoot is null) throw new McpToolException("A busca no índice não está disponível nesta instalação do Spock.");
        if (!workspace.TryGetRoot(project.ProjectId, out var root)) throw EngineErrors.ProjectNotAuthorized();

        var reply = await search.SearchKnowledgeAsync(new JsonObject
        {
            ["schema"] = SearchSchema,
            ["projectId"] = project.ProjectId,
            ["query"] = query,
            ["knowledgeRoot"] = indexRoot,
            ["limit"] = limit
        }, call.Ct);
        if (reply.Text("schema") != SearchAck || reply["hits"] is not JsonArray rawHits)
            throw EngineErrors.AiRuntimeInvalidResponse();

        var hits = rawHits.OfType<JsonObject>().Select(Hit.From).OfType<Hit>().ToList();
        if (hits.Count == 0)
        {
            return reply["indexed"]?.GetValue<bool>() is true
                ? $"Nada no índice de {project.DisplayName} casa com '{query}'. Tente nomes de classes, métodos, ids de requisito ou termos do domínio."
                : $"O índice de {project.DisplayName} ainda não foi montado. O Spock monta o índice ao revisar o contexto ou rodar uma execução de engenharia.";
        }

        var memory = new DecisionMemory(workspace, decisions).Documents(project.ProjectId).ToDictionary(item => item.Path, item => item.Hash, StringComparer.Ordinal);
        var current = KnowledgeSources.EvaluatePaths(files.Open(root),
                hits.Select(hit => hit.Path).Where(path => !path.StartsWith(DecisionMemory.Prefix, StringComparison.Ordinal) && !path.StartsWith(ExternalKnowledge.Prefix, StringComparison.Ordinal)))
            .Where(item => item.Reason == KnowledgeSources.Exclusion.None)
            .ToDictionary(item => item.Path, item => item.Hash!, StringComparer.Ordinal);

        var blocks = new List<string>();
        var delivered = new List<string>();
        var withheld = 0;
        foreach (var hit in hits)
        {
            string? hash;
            if (hit.Path.StartsWith(DecisionMemory.Prefix, StringComparison.Ordinal)) hash = memory.GetValueOrDefault(hit.Path);
            else if (hit.Path.StartsWith(ExternalKnowledge.Prefix, StringComparison.Ordinal)) hash = null;
            else hash = current.GetValueOrDefault(hit.Path);
            if (hash is null)
            {
                withheld++;
                continue;
            }
            blocks.Add(Render(hit, stale: hash != hit.Hash));
            delivered.Add(hit.Path);
        }

        if (blocks.Count == 0)
            return $"Os {hits.Count} trechos que casam com '{query}' ficaram de fora: os arquivos saíram das regras de conhecimento, não existem mais ou vêm de uma fonte externa.";

        var header = $"{Count(blocks.Count, "trecho", "trechos")} do índice de {project.DisplayName} para '{query}', do mais ao menos relevante.";
        if (withheld > 0)
            header += $" {Count(withheld, "trecho ficou", "trechos ficaram")} de fora: o arquivo saiu das regras de conhecimento, não existe mais ou vem de uma fonte externa.";
        Audit(project.ProjectId, Search.Name, call.Client, blocks.Count, delivered);
        return McpText.Fit(blocks.Prepend(header), MaxChars, "Peça um 'limit' menor ou uma busca mais específica.");
    }

    private static string Render(Hit hit, bool stale)
    {
        var title = hit.Path.StartsWith(DecisionMemory.Prefix, StringComparison.Ordinal)
            ? "## Decisão registrada no Spock"
            : $"## {hit.Path}:{hit.StartLine}{(hit.Heading.Length > 0 ? $" · {hit.Heading}" : "")}";
        var warning = stale ? "\n(O arquivo mudou depois da indexação: confira no disco antes de confiar neste trecho.)" : "";
        return $"{title}{warning}\n{hit.Body.TrimEnd()}";
    }

    private void Audit(string projectId, string tool, string? client, int items, IReadOnlyList<string> paths)
    {
        if (audit is null) return;
        var unique = paths.Distinct(StringComparer.Ordinal).ToList();
        var detail = new JsonObject
        {
            ["projectId"] = projectId,
            ["tool"] = tool,
            ["items"] = items,
            ["paths"] = new JsonArray(unique.Take(MaxAuditedPaths).Select(path => (JsonNode)path).ToArray()),
            ["files"] = unique.Count
        };
        if (client is not null) detail["client"] = client;
        audit.Record(ReadAction, detail);
    }

    private AuthorizedProject Resolve(WorkspaceAuthorizer workspace, JsonObject args) =>
        new McpProjectResolver(workspace, workingDirectory, scope).Resolve(McpArguments.Text(args, "project", MaxProjectArgument));

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

    private ManagementSnapshot? Management(WorkspaceAuthorizer workspace, string projectId)
    {
        if (management is null) return null;
        try
        {
            var service = management(workspace);
            return new ManagementSnapshot(service.Metrics(projectId, null), service.Risks(projectId), service.Milestones(projectId));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            return null;
        }
    }

    private static string Count(int value, string one, string many) => $"{value} {(value == 1 ? one : many)}";

    private sealed record Hit(string Path, string Hash, string Heading, int StartLine, string Body)
    {
        public static Hit? From(JsonObject item)
        {
            var path = item.Text("filePath");
            var body = item.Text("body");
            if (path.Length == 0 || body.Length == 0) return null;
            var line = item["startLine"] is JsonValue value && value.TryGetValue<int>(out var number) ? number : 1;
            return new Hit(path, item.Text("hash"), item.Text("heading"), line, body);
        }
    }
}
