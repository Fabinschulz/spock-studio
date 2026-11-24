using System.Text;
using System.Text.Json.Nodes;
using Mariliz.Application.Analysis;
using Mariliz.Application.Discovery;
using Mariliz.Application.Integrations;
using Mariliz.Application.Ports;
using Mariliz.Application.Workflows;
using Mariliz.Application.Workspace;

namespace Mariliz.Application.Mcp;

// W4-1 no modo Sessão: o agente avalia a arquitetura com a análise e as referências do Spock, grava a avaliação no projeto e
// confere aqui. Nada chama modelo nem muda o código; a pessoa lê o resultado na página do projeto.
public sealed class ArchitectureAssessmentTools(Func<WorkspaceAuthorizer> workspaces, IProjectFilesFactory files, string workingDirectory,
    IReadOnlySet<string> scope, IAuditLog? audit = null)
{
    public const string PrepareTool = "spock_architecture_assessment_prepare";
    public const string CheckTool = "spock_architecture_assessment_check";
    private const int MaxArgument = 200;
    private const int MaxReferences = 60;

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
        PrepareTool, "Preparar a avaliação de arquitetura",
        "Para o agente avaliar a arquitetura do projeto sem que o Spock chame modelo: devolve a análise determinística do código " +
        "(camadas, dependências, violações, tamanho e testes), as referências REF-* das stacks do projeto, o formato da avaliação e " +
        "onde gravá-la. Só leitura.",
        ProjectOnly.DeepClone().AsObject());

    public static readonly McpToolInfo Check = new(
        CheckTool, "Conferir a avaliação de arquitetura",
        "Confere a avaliação que o agente gravou no projeto: cada divergência classificada (risk, cost, style), com arquivo que " +
        "existe e apoiada numa REF-* ou num achado da análise. Devolve o que falha. Só leitura.",
        ProjectOnly.DeepClone().AsObject());

    public IReadOnlyList<McpTool> Tools() => [new(Prepare, PrepareAsync), new(Check, CheckAsync)];

    private Task<string> PrepareAsync(McpCall call)
    {
        var (project, projectFiles, root) = Open(call);
        var report = ArchitectureAnalyzer.Analyze(project.ProjectId, projectFiles, DateTimeOffset.UnixEpoch);
        var stacks = ProjectDiscoverer.Scan(projectFiles).Stacks.Select(stack => stack.Id).Distinct().ToList();
        var references = OrganizationReferences.Patterns.Where(pattern => pattern.AppliesTo(stacks)).ToList();

        var text = new StringBuilder($"Avaliação de arquitetura de {project.DisplayName}. O Spock não chama modelo aqui: você avalia, o Spock confere e a pessoa decide.\n");
        text.Append("\n## Análise do Spock (determinística)\n\n");
        var analysis = ArchitectureAnalysisDocuments.Render(report);
        text.Append(analysis.Length == 0 ? "A análise não achou projeto .NET nem frontend que reconheça; avalie pelo código e pelas referências.\n" : analysis);
        text.Append($"\n## Referências das stacks do projeto ({(stacks.Count == 0 ? "nenhuma stack reconhecida" : string.Join(", ", stacks))})\n\n");
        foreach (var pattern in references.Take(MaxReferences))
            text.Append($"- {pattern.Id} · {pattern.Title} ({pattern.Section})\n");
        if (references.Count > MaxReferences) text.Append($"- e mais {references.Count - MaxReferences}; filtre com spock_architecture_reference.\n");
        if (references.Count == 0) text.Append("Nenhuma referência da pasta Organização vale para estas stacks; apoie cada divergência num achado da análise.\n");
        text.Append("\n## Como avaliar\n\n").Append(ArchitectureAssessmentGuide.For(FullPath(root, projectFiles)));
        if (ArchitectureAssessments.Read(projectFiles, report) is { } current)
            text.Append($"\nJá existe uma avaliação ({Status(current)}). Gravar de novo a substitui.");

        Audit(project.ProjectId, PrepareTool, call.Client, report.Findings.Count);
        return Task.FromResult(text.ToString());
    }

    private Task<string> CheckAsync(McpCall call)
    {
        var (project, projectFiles, root) = Open(call);
        var report = ArchitectureAnalyzer.Analyze(project.ProjectId, projectFiles, DateTimeOffset.UnixEpoch);
        var assessment = ArchitectureAssessments.Read(projectFiles, report);
        Audit(project.ProjectId, CheckTool, call.Client, assessment?.Divergences.Count ?? 0);
        if (assessment is null)
            return Task.FromResult($"Nenhuma avaliação gravada em {FullPath(root, projectFiles)}. Grave o arquivo no formato de {PrepareTool} e chame de novo.");

        var text = new StringBuilder($"Avaliação de {project.DisplayName}: {Status(assessment)}.");
        if (assessment.Summary is not null)
        {
            var counts = DivergenceClassifications.All.Select(kind => $"{assessment.Divergences.Count(d => d.Classification == kind)} {kind}");
            text.Append($"\n{assessment.Divergences.Count} divergência(s) ({string.Join(", ", counts)}) e {assessment.Conventions.Count} convenção(ões).");
        }
        foreach (var check in assessment.Checks.Where(check => !check.Passed))
            text.Append($"\n- {check.Description}{(check.Detail is null ? "" : $" ({check.Detail})")}");
        text.Append(assessment.Valid
            ? "\nA pessoa lê a avaliação na página do projeto no Spock, seção Arquitetura."
            : "\nCorrija o arquivo e confira de novo.");
        return Task.FromResult(text.ToString());
    }

    private static string Status(ArchitectureAssessment assessment)
    {
        var failed = assessment.Checks.Count(check => !check.Passed);
        return failed == 0 ? $"passa nas {assessment.Checks.Count} verificações" : $"{failed} de {assessment.Checks.Count} verificações falhando";
    }

    // Caminho completo: a sessão do agente pode estar aberta em outra pasta, e um caminho relativo iria parar lá.
    private static string FullPath(string root, IProjectFiles projectFiles) =>
        Path.Combine(root, ArchitectureAssessments.PathIn(projectFiles).Replace('/', Path.DirectorySeparatorChar));

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
