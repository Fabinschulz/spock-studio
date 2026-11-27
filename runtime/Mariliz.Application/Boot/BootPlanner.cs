namespace Mariliz.Application.Boot;

public sealed record PlannedBoot(BootPlan Plan, List<BootStep> Steps);

/// <summary>
/// Monta o plano a partir do que já foi decidido (capacidade, disponibilidade, permissão). Não executa nada e não consulta nada:
/// o que o plano promete é exatamente o que o <see cref="BootService"/> vai fazer, passo a passo.
/// </summary>
public sealed class BootPlanner(CapabilityRegistry registry)
{
    public const string ContextStepId = "context";

    // O Project Boot lê estas, nesta ordem. A análise do projeto só entra quando falta perfil e o nível permite preparar.
    public static readonly IReadOnlyList<string> ProjectReads =
    [
        "engineering.project_overview", "engineering.harness_status", "connections.list", "management.milestones", "platform.runs"
    ];

    public PlannedBoot ForCapability(IntentResult intent, Capability capability, CapabilityStatus status, PermissionDecision decision, BootFacts facts)
    {
        var steps = new List<BootStep>();
        var previous = Check(steps, "project", "Conferir o projeto", facts, null);
        if (capability.Needs.HasFlag(CapabilityNeeds.Project) && facts.ProjectAuthorized)
            previous = Add(steps, Step(ContextStepId, StepKinds.Context, "Reunir o contexto do projeto", null, null, OperationClass.Read, previous));

        previous = Add(steps, new BootStep
        {
            Id = "permission",
            Kind = StepKinds.Check,
            Title = "Conferir disponibilidade e permissão",
            CapabilityId = capability.Id,
            Operation = capability.Operation,
            DependsOn = previous is null ? [] : [previous],
            Status = decision.Verdict == PermissionVerdict.Denied ? StepStatuses.Blocked : StepStatuses.Completed,
            Summary = decision.Verdict == PermissionVerdict.Denied ? decision.Reason : $"{Describe(status.Availability)}. {decision.Reason}",
            Evidence = [$"Operação: {OperationLabel(capability.Operation)}; nível mínimo {(int)decision.RequiredLevel}."]
        });

        if (decision.Verdict == PermissionVerdict.Denied) return new PlannedBoot(PlanFor(intent, [capability], decision), steps);

        if (capability.IsComposite)
        {
            string? before = previous;
            foreach (var memberId in capability.Composes)
            {
                var member = registry.Find(memberId)!;
                before = Add(steps, Step($"handoff:{member.Id}", StepKinds.Handoff, $"Abrir: {member.Name}", member.Id, member.Method, member.Operation, before));
            }
            return new PlannedBoot(PlanFor(intent, [.. capability.Composes.Select(id => registry.Find(id)!)], decision, capability.Id), steps);
        }

        var kind = decision.Verdict == PermissionVerdict.Allowed ? StepKinds.Capability : StepKinds.Handoff;
        var title = kind == StepKinds.Capability ? $"Consultar: {capability.Name}" : $"Abrir: {capability.Name}";
        Add(steps, Step(kind + ":" + capability.Id, kind, title, capability.Id, capability.Method, capability.Operation, previous));
        return new PlannedBoot(PlanFor(intent, [capability], decision), steps);
    }

    public PlannedBoot ForProject(BootFacts facts, AutonomyLevel autonomy)
    {
        var steps = new List<BootStep>();
        var previous = Check(steps, "project", "Identificar o projeto", facts, null);
        var plan = new BootPlan
        {
            Objective = facts.ProjectName is null ? "Preparar o projeto" : $"Preparar o projeto {facts.ProjectName}",
            Domain = BootDomains.Engineering,
            Operation = OperationClass.Read,
            RequiredLevel = AutonomyLevel.Observation,
            SuccessCriteria =
            [
                "Cada leitura vem do motor e fica no registro com o que ela encontrou.",
                "O que não está pronto aparece como limitação, não como sucesso.",
                "Nenhum arquivo do projeto é criado nem alterado."
            ],
            ExpectedResult = "Um relatório de inicialização: o que está preparado, o que está indisponível e o que pede a sua intervenção."
        };
        if (!facts.ProjectAuthorized) return new PlannedBoot(plan, steps);

        previous = Add(steps, Step(ContextStepId, StepKinds.Context, "Reunir o contexto do projeto", null, null, OperationClass.Read, previous));
        // Projeto que já tem perfil não é reanalisado: o Boot de um projeto existente não refaz o que está feito nem apaga histórico.
        var firstTime = facts.ProfileAnalyzedAt is null;
        if (firstTime && autonomy >= AutonomyLevel.Preparation)
        {
            var analyze = registry.Find("engineering.analyze_project")!;
            plan.Operation = OperationClass.Preparation;
            plan.RequiredLevel = AutonomyLevel.Preparation;
            plan.Risks.Add("A análise grava o perfil só na pasta de dados do Spock; o projeto não é tocado.");
            previous = Add(steps, Step($"capability:{analyze.Id}", StepKinds.Capability, "Analisar o projeto pela primeira vez", analyze.Id, analyze.Method, analyze.Operation, previous));
            plan.Capabilities.Add(analyze.Id);
        }

        foreach (var id in ProjectReads)
        {
            var capability = registry.Find(id)!;
            previous = Add(steps, Step($"capability:{id}", StepKinds.Capability, $"Consultar: {capability.Name}", id, capability.Method, capability.Operation, previous));
            plan.Capabilities.Add(id);
        }
        return new PlannedBoot(plan, steps);
    }

    private BootPlan PlanFor(IntentResult intent, IReadOnlyList<Capability> capabilities, PermissionDecision decision, string? compositeId = null)
    {
        var worst = capabilities.Max(capability => capability.Operation);
        var plan = new BootPlan
        {
            Objective = intent.Objective.Length == 0 ? capabilities[0].Name : intent.Objective,
            Domain = intent.Domain ?? capabilities[0].Domain,
            Capabilities = compositeId is null ? [.. capabilities.Select(capability => capability.Id)] : [compositeId, .. capabilities.Select(capability => capability.Id)],
            Operation = worst,
            RequiredLevel = PermissionEvaluator.RequiredLevel(worst),
            ExpectedResult = capabilities.Count == 1 ? capabilities[0].ExpectedResult : string.Join(" ", capabilities.Select(capability => capability.ExpectedResult)),
            SuccessCriteria = [.. capabilities.SelectMany(capability => capability.Validations).Distinct()]
        };
        if (decision.NeedsApproval) plan.Approvals.Add(decision.Reason);
        if (worst >= OperationClass.InternalChange) plan.Risks.Add($"Operação de tipo \"{OperationLabel(worst)}\": o Boot só prepara; a execução é da tela.");
        return plan;
    }

    private static string? Check(List<BootStep> steps, string id, string title, BootFacts facts, string? previous)
    {
        var step = new BootStep
        {
            Id = id,
            Kind = StepKinds.Check,
            Title = title,
            DependsOn = previous is null ? [] : [previous],
            Status = StepStatuses.Completed,
            Summary = facts.ProjectAuthorized ? $"Projeto {facts.ProjectName}, autorizado neste computador." : facts.ProjectId is null ? "Nenhum projeto escolhido." : "O projeto não está autorizado neste computador.",
            Evidence = ["O Spock não tem usuário nem organização: o escopo é o projeto autorizado nesta máquina."]
        };
        steps.Add(step);
        return step.Id;
    }

    private static string? Add(List<BootStep> steps, BootStep step)
    {
        steps.Add(step);
        return step.Id;
    }

    private static BootStep Step(string id, string kind, string title, string? capabilityId, string? method, OperationClass operation, string? after) => new()
    {
        Id = id,
        Kind = kind,
        Title = title,
        CapabilityId = capabilityId,
        Method = method,
        Operation = operation,
        DependsOn = after is null ? [] : [after]
    };

    public static string Describe(CapabilityAvailability availability) => availability switch
    {
        CapabilityAvailability.Available => "Disponível",
        CapabilityAvailability.Partial => "Parcialmente disponível",
        CapabilityAvailability.Unavailable => "Indisponível",
        CapabilityAvailability.NotConfigured => "Não configurada",
        CapabilityAvailability.BlockedByPermission => "Bloqueada por permissão",
        CapabilityAvailability.DependsOnIntegration => "Depende de integração",
        _ => "Em manutenção"
    };

    public static string OperationLabel(OperationClass operation) => operation switch
    {
        OperationClass.Read => "leitura",
        OperationClass.Query => "consulta",
        OperationClass.Analysis => "análise",
        OperationClass.Preparation => "preparação",
        OperationClass.Configuration => "configuração",
        OperationClass.InternalChange => "alteração interna",
        OperationClass.ExternalChange => "alteração externa",
        OperationClass.Sensitive => "operação sensível",
        _ => "operação destrutiva"
    };
}
