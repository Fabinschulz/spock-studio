using System.Text;
using System.Text.Json.Nodes;
using Mariliz.Application.Analysis;
using Mariliz.Application.Compatibility;
using Mariliz.Application.Integrations;
using Mariliz.Application.Ports;
using Mariliz.Application.Workspace;

namespace Mariliz.Application.Mcp;

// W4-2 no modo Sessão: o agente propõe o plano a partir da avaliação (W4-1), grava no projeto e confere aqui. As aprovações
// vêm da pasta de dados do Spock, onde só a pessoa as registra pela tela; daqui o agente só as lê.
public sealed class ModernizationPlanTools(Func<WorkspaceAuthorizer> workspaces, IProjectFilesFactory files, string workingDirectory,
    IReadOnlySet<string> scope, IModernizationApprovalStore approvals, IAuditLog? audit = null, IClock? clock = null, ReleaseCatalog? catalog = null)
{
    public const string PrepareTool = "spock_modernization_plan_prepare";
    public const string CheckTool = "spock_modernization_plan_check";
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
        PrepareTool, "Preparar o plano de modernização",
        "Para o agente propor a modernização do projeto sem que o Spock chame modelo: devolve as divergências da avaliação de " +
        "arquitetura (D1, D2...), as versões desatualizadas com o caminho de saltos (V1, V2...), o formato do plano e onde gravá-lo. " +
        "Passos estruturais exigem a avaliação de spock_architecture_assessment_prepare; saltos de versão, não. Só leitura.",
        ProjectOnly.DeepClone().AsObject());

    public static readonly McpToolInfo Check = new(
        CheckTool, "Conferir o plano de modernização",
        "Confere o plano que o agente gravou (caracterização, passos reversíveis e verificáveis, reescrita só declarada e com " +
        "custo, risco e convivência) e diz quais passos a pessoa aprovou no Spock. Só leitura.",
        ProjectOnly.DeepClone().AsObject());

    public IReadOnlyList<McpTool> Tools() => [new(Prepare, PrepareAsync), new(Check, CheckAsync)];

    private Task<string> PrepareAsync(McpCall call)
    {
        var (project, modernization, root) = Open(call);
        Audit(project.ProjectId, PrepareTool, call.Client, (modernization.Assessment?.Divergences.Count ?? 0) + modernization.Versions.Count);
        var assessment = modernization.Assessment is { Valid: true } valid ? valid : null;
        if (assessment is null && modernization.Versions.Count == 0)
            return Task.FromResult(
                $"O plano de modernização parte da avaliação de arquitetura de {project.DisplayName}, e ela " +
                $"{(modernization.Assessment is null ? "ainda não existe" : "não passa nas verificações")}; nenhuma versão está atrás da recomendada. " +
                $"Faça a avaliação com {ArchitectureAssessmentTools.PrepareTool} e confira com {ArchitectureAssessmentTools.CheckTool} antes.");

        var text = new StringBuilder($"Plano de modernização de {project.DisplayName}. O Spock não chama modelo aqui: você propõe, o Spock confere e a pessoa aprova cada passo.\n");
        if (assessment is null)
            text.Append($"\nSem avaliação de arquitetura válida: o plano pode ter só saltos de versão. Passos estruturais pedem antes {ArchitectureAssessmentTools.PrepareTool}.\n");
        else
        {
            if (assessment.Summary is not null) text.Append($"\n## Avaliação de arquitetura\n\n{assessment.Summary}\n");
            text.Append("\n## Divergências (cite pelo id)\n\n");
            foreach (var divergence in assessment.Divergences)
            {
                text.Append($"- {divergence.Id} [{divergence.Classification}] {divergence.Title}: {divergence.Description}\n");
                text.Append($"  Arquivos: {string.Join(", ", divergence.Files)}. Recomendação da avaliação: {divergence.Recommendation}\n");
            }
            if (assessment.Conventions.Count > 0)
            {
                text.Append("\n## Convenções do projeto (o plano preserva)\n\n");
                foreach (var convention in assessment.Conventions) text.Append($"- {convention.Title}\n");
            }
        }
        AppendVersions(text, modernization.Versions);
        text.Append("\n## Como planejar\n\n").Append(ModernizationPlanGuide.For(FullPath(root, modernization.PlanPath)));
        if (modernization.Plan is { } plan)
        {
            text.Append($"\nJá existe um plano ({Status(plan)}). Gravar de novo o substitui; passo que mudar perde a aprovação.");
            AppendDecisions(text, modernization);
        }
        return Task.FromResult(text.ToString());
    }

    // Os saltos vêm prontos: o agente não escolhe a linha de destino, só planeja cada trecho com o que a página oficial diz.
    private static void AppendVersions(StringBuilder text, IReadOnlyList<VersionFinding> versions)
    {
        if (versions.Count == 0) return;
        text.Append("\n## Versões atrás da recomendada (cite pelo id em upgrade.finding)\n\n");
        foreach (var finding in versions)
        {
            var root = finding.Root.Length == 0 ? "raiz" : finding.Root;
            text.Append($"- {finding.Id} {root}: {finding.Name} {finding.Version ?? finding.Cycle} ({finding.StatusLabel}{(finding.Eol is null ? "" : $", fim do suporte {finding.Eol}")}). ");
            text.Append($"Recomendada: {finding.Target}. Caminho: {finding.Cycle} → {string.Join(" → ", finding.Hops.Select(hop => hop.To))}\n");
            foreach (var hop in finding.Hops)
                text.Append($"  {hop.From} → {hop.To}: {hop.Guide ?? "sem página conhecida pelo Spock; cite a da fonte oficial (https)"}\n");
        }
    }
    private Task<string> CheckAsync(McpCall call)
    {
        var (project, modernization, root) = Open(call);
        var plan = modernization.Plan;
        Audit(project.ProjectId, CheckTool, call.Client, plan?.Steps.Count ?? 0);
        if (plan is null)
            return Task.FromResult($"Nenhum plano gravado em {FullPath(root, modernization.PlanPath)}. Grave o arquivo no formato de {PrepareTool} e chame de novo.");

        var text = new StringBuilder($"Plano de modernização de {project.DisplayName}: {Status(plan)}.");
        foreach (var check in plan.Checks.Where(check => !check.Passed))
            text.Append($"\n- {check.Description}{(check.Detail is null ? "" : $" ({check.Detail})")}");
        if (!plan.Valid) return Task.FromResult(text.Append("\nCorrija o arquivo e confira de novo.").ToString());

        AppendDecisions(text, modernization);
        var approved = modernization.Steps.Where(state => state.Status == ModernizationStepStatuses.Approved).Select(state => state.StepId).ToList();
        text.Append(approved.Count == 0
            ? "\nNenhum passo aprovado ainda: a pessoa aprova cada passo no Spock, página Arquitetura. Não execute nada antes."
            : $"\nPode executar, em ordem e rodando a verificação de cada um, só: {string.Join(", ", approved)}.");
        return Task.FromResult(text.ToString());
    }

    private static void AppendDecisions(StringBuilder text, ProjectModernization modernization)
    {
        if (modernization.Steps.Count == 0) return;
        text.Append("\nDecisões da pessoa por passo:");
        foreach (var state in modernization.Steps) text.Append($"\n- {state.StepId}: {Label(state.Status)}");
    }

    private static string Label(string status) => status switch
    {
        ModernizationStepStatuses.Approved => "aprovado",
        ModernizationStepStatuses.Rejected => "recusado",
        ModernizationStepStatuses.Changed => "mudou depois da decisão; precisa de nova aprovação",
        _ => "aguardando a pessoa"
    };

    private static string Status(ModernizationPlan plan)
    {
        var failed = plan.Checks.Count(check => !check.Passed);
        return failed == 0 ? $"passa nas {plan.Checks.Count} verificações, {plan.Steps.Count} passo(s)" : $"{failed} de {plan.Checks.Count} verificações falhando";
    }

    // Caminho completo: a sessão do agente pode estar aberta em outra pasta, e um caminho relativo iria parar lá.
    private static string FullPath(string root, string relative) => Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));

    private (AuthorizedProject Project, ProjectModernization Modernization, string Root) Open(McpCall call)
    {
        var authorizer = workspaces();
        var project = new McpProjectResolver(authorizer, workingDirectory, scope).Resolve(McpArguments.Text(call.Arguments, "project", MaxArgument));
        if (!authorizer.TryGetRoot(project.ProjectId, out var root)) throw new McpToolException("O projeto não está autorizado no Spock.");
        var modernization = ModernizationService.Read(project.ProjectId, files.Open(root), clock?.UtcNow ?? DateTimeOffset.UtcNow, approvals.List(project.ProjectId), catalog);
        return (project, modernization, root);
    }

    private void Audit(string projectId, string tool, string? client, int items)
    {
        if (audit is null) return;
        var detail = new JsonObject { ["projectId"] = projectId, ["tool"] = tool, ["items"] = items, ["paths"] = new JsonArray(), ["files"] = 0 };
        if (client is not null) detail["client"] = client;
        audit.Record(ProjectContextTools.ReadAction, detail);
    }
}
