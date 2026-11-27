namespace Mariliz.Application.Boot;

/// <summary>
/// O fim de um Boot, escrito pelo motor a partir do que os passos fizeram: o que foi feito, o que foi consultado, o que mudou, o
/// que foi conferido, o que deu problema e o que falta. Sucesso só é dito quando um passo concluiu; o que não rodou aparece como
/// pendência ou limitação.
/// </summary>
public static class BootResults
{
    private static readonly Dictionary<string, string> Unknowns = new(StringComparer.Ordinal)
    {
        ["model"] = "os provedores de IA",
        ["integrations"] = "as integrações",
        ["cloud"] = "as conexões cloud",
        ["harness"] = "o Harness",
        ["profile"] = "o perfil tecnológico"
    };

    public static BootResult Unresolved(IntentResult intent, CapabilityRegistry registry, BootFacts facts, IReadOnlyList<CapabilityStatus> statuses)
    {
        var result = new BootResult
        {
            Headline = intent.Question ?? "Não entendi o pedido.",
            Done = ["Li o pedido e comparei com as capacidades do Spock."],
            Changes = [],
            Validations = ["Nada foi executado: o Boot prefere perguntar a escolher errado."]
        };
        result.Alternatives = [.. intent.Candidates.Select(candidate => Alternative(registry, candidate, statuses))];
        if (intent.Resolution == IntentResolution.NeedsClarification)
        {
            result.Outcome = BootOutcomes.NeedsClarification;
            result.NextStep = "Escolha uma das opções para eu continuar.";
        }
        else
        {
            result.Outcome = BootOutcomes.Unsupported;
            result.NextStep = "Descreva o resultado que você quer, ou comece por uma das atividades da tela inicial.";
        }
        return result;
    }

    public static BootResult Blocked(Capability capability, CapabilityStatus status, IntentResult intent, CapabilityRegistry registry, BootFacts facts)
    {
        var result = new BootResult
        {
            Outcome = status.Availability == CapabilityAvailability.Unavailable ? BootOutcomes.Unsupported : BootOutcomes.NeedsInput,
            Headline = $"Entendi: {capability.Name}. Mas não dá para continuar ainda.",
            Done = ["Reconheci o pedido.", "Conferi o que a capacidade precisa."],
            Problems = [status.Reason ?? "A capacidade não está disponível."],
            Changes = [],
            Validations = [$"Disponibilidade: {BootPlanner.Describe(status.Availability).ToLowerInvariant()}."]
        };
        (result.NextStep, result.Route, result.RouteLabel) = status.Availability switch
        {
            CapabilityAvailability.BlockedByPermission => ("Escolha ou abra um projeto e peça de novo.", null, null),
            CapabilityAvailability.NotConfigured => ("Configure um provedor de IA e peça de novo.", "/configuracoes/ia", "Abrir Modelos de IA"),
            CapabilityAvailability.DependsOnIntegration => ("Conecte e vincule o GitHub ou o Jira ao projeto e peça de novo.", "/integracoes", "Abrir Conexões"),
            _ => ("Esta capacidade não está disponível nesta versão do motor.", null, null)
        };
        var statuses = registry.Resolve(facts);
        result.Alternatives = [.. intent.Candidates.Where(candidate => candidate.CapabilityId != capability.Id)
            .Select(candidate => Alternative(registry, candidate, statuses))
            .Where(alternative => statuses.First(s => s.Capability.Id == alternative.CapabilityId).Availability is CapabilityAvailability.Available or CapabilityAvailability.Partial)];
        return result;
    }

    public static BootResult Build(BootRecord record, BootFacts facts, CapabilityRegistry registry, Capability? capability, PermissionDecision? decision,
        string? previousContextVersion)
    {
        // Sem intenção reconhecida é o Project Boot, também quando é a recuperação dele.
        var result = record.Intent is null ? Project(record, facts, registry) : Request(record, registry, capability, decision);

        if (previousContextVersion is not null && record.ContextVersion is not null)
            result.Validations.Add(previousContextVersion == record.ContextVersion
                ? "O contexto do projeto não mudou desde a interrupção."
                : "O contexto do projeto mudou desde a interrupção (vínculos, políticas ou Harness): o que já estava feito foi mantido e o resto foi refeito sobre o contexto novo.");
        foreach (var step in record.Steps.Where(step => step.Status == StepStatuses.Failed))
            result.Problems.Add($"{step.Title}: {step.Summary}");
        foreach (var step in record.Steps.Where(step => step.Kind == StepKinds.Context && step.Status == StepStatuses.Completed))
            result.Problems.AddRange(step.Evidence);
        return result;
    }

    private static BootResult Request(BootRecord record, CapabilityRegistry registry, Capability? capability, PermissionDecision? decision)
    {
        capability ??= registry.Find(record.Intent!.CapabilityId!)!;
        var result = new BootResult { Changes = [], Route = capability.Route, RouteLabel = capability.Route is null ? null : $"Abrir {capability.Name}" };
        result.Done.Add($"Reconheci o pedido como \"{capability.Name}\".");
        result.Done.Add("Conferi o projeto, a disponibilidade e a permissão.");

        if (capability.IsComposite)
        {
            var members = capability.Composes.Select(id => registry.Find(id)!).ToList();
            result.Outcome = BootOutcomes.Handoff;
            result.Headline = $"\"{capability.Name}\" tem {members.Count} etapas, uma de cada vez: {string.Join(", ", members.Select(member => member.Name))}.";
            result.Route = members[0].Route;
            result.RouteLabel = $"Começar por {members[0].Name}";
            result.Pending = [.. members.Skip(1).Select(member => $"Depois: {member.Name}, só quando a etapa anterior terminar e você aprovar.")];
            result.NextStep = $"Comece por \"{members[0].Name}\".";
            result.Validations.Add("Cada etapa abre a tela que já existe e pede a própria aprovação.");
            return result;
        }

        var step = record.Steps.FirstOrDefault(s => s.CapabilityId == capability.Id && s.Kind is StepKinds.Capability or StepKinds.Handoff);
        if (step?.Kind == StepKinds.Capability)
        {
            if (step.Status == StepStatuses.Completed)
            {
                result.Outcome = BootOutcomes.Answered;
                result.Headline = step.Summary;
                result.Done.Add($"Consultei \"{capability.Name}\" pelo mesmo caminho da tela.");
                result.Consulted = [.. step.Evidence];
                result.Validations.Add("O resultado veio do motor; o Boot não escreveu texto sobre ele.");
                result.Validations.Add("Nada no projeto foi alterado.");
                result.NextStep = $"Abra \"{capability.Name}\" para ver o detalhe.";
            }
            else
            {
                result.Outcome = BootOutcomes.Failed;
                result.Headline = $"Não consegui consultar \"{capability.Name}\".";
                result.NextStep = "Tente de novo; se repetir, veja o Diagnóstico.";
            }
            return result;
        }

        result.Outcome = BootOutcomes.Handoff;
        result.Headline = $"Pronto para continuar em \"{capability.Name}\".";
        result.Validations.Add("Nada foi alterado: o Boot só preparou o caminho.");
        if (decision?.NeedsApproval == true) result.Pending.Add(decision.Reason);
        result.NextStep = decision?.NeedsApproval == true ? $"Abra \"{capability.Name}\", revise o que muda e aprove." : $"Abra \"{capability.Name}\".";
        return result;
    }

    private static BootResult Project(BootRecord record, BootFacts facts, CapabilityRegistry registry)
    {
        var firstTime = facts.ProfileAnalyzedAt is null;
        var result = new BootResult
        {
            Outcome = BootOutcomes.Prepared,
            Changes = [],
            Route = facts.HarnessApplied ? "/projeto" : "/configurar"
        };
        result.RouteLabel = facts.HarnessApplied ? "Abrir a visão geral do projeto" : "Configurar o ambiente";
        var capabilitySteps = record.Steps.Where(step => step.Kind == StepKinds.Capability).ToList();

        foreach (var step in record.Steps.Where(step => step.Status == StepStatuses.Completed && step.Kind != StepKinds.Check))
            result.Done.Add(step.Title + ".");
        foreach (var step in capabilitySteps.Where(step => step.Status == StepStatuses.Completed))
        {
            result.Consulted.Add(step.Summary);
            result.Consulted.AddRange(step.Evidence);
        }
        // A análise do projeto e a leitura do perfil dizem as mesmas coisas, uma depois da outra.
        result.Consulted = [.. result.Consulted.Distinct(StringComparer.Ordinal)];
        foreach (var step in capabilitySteps.Where(step => step.Status == StepStatuses.Skipped))
            result.Problems.Add($"{registry.Find(step.CapabilityId ?? "")?.Name ?? step.Title}: {step.Summary}");
        if (capabilitySteps.Any(step => step.CapabilityId == "engineering.analyze_project" && step.Status == StepStatuses.Completed))
            result.Changes.Add("O perfil tecnológico foi gravado na pasta de dados do Spock (fora do projeto).");

        result.Problems.Add("O Spock ainda não tem usuário nem organização: o Boot trabalha por projeto autorizado neste computador.");
        foreach (var unknown in facts.Unknown) result.Problems.Add($"Não consegui conferir {Unknowns.GetValueOrDefault(unknown, unknown)} nesta sessão do motor.");

        if (!facts.HarnessApplied) result.Pending.Add("O Harness ainda não foi aplicado: o projeto não tem as regras gerenciadas pelo Spock.");
        if (firstTime && record.Autonomy < AutonomyLevel.Preparation) result.Pending.Add("O projeto ainda não foi analisado; no nível de observação o Boot não grava o perfil.");
        if (facts.IntegrationLinks == 0) result.Pending.Add("Nenhum GitHub ou Jira vinculado: a gestão não terá dados.");
        if (!facts.ModelConfigured) result.Pending.Add("Nenhum provedor de IA configurado: assistentes e pedidos com IA ficam indisponíveis.");

        var statuses = registry.Resolve(facts);
        var ready = statuses.Count(status => status.Availability is CapabilityAvailability.Available or CapabilityAvailability.Partial);
        result.Validations.Add($"{ready} de {statuses.Count} capacidades estão prontas para este projeto.");
        result.Validations.Add("Cada leitura veio do motor, pelo mesmo caminho da interface.");
        result.Validations.Add("Nenhum arquivo do projeto foi criado nem alterado.");

        result.Headline = firstTime
            ? $"{facts.ProjectName ?? "O projeto"} é novo no Spock: reuni o contexto e o que ele já sabe."
            : $"{facts.ProjectName ?? "O projeto"} já é conhecido: nada foi reinicializado nem apagado.";
        result.NextStep = !facts.HarnessApplied
            ? "Configure o ambiente para o Spock gerenciar as regras dos agentes."
            : result.Pending.Count > 0 ? result.Pending[0] : "O projeto está preparado. Faça um pedido de engenharia ou consulte a gestão.";
        return result;
    }

    private static BootAlternative Alternative(CapabilityRegistry registry, IntentCandidate candidate, IReadOnlyList<CapabilityStatus> statuses)
    {
        var capability = registry.Find(candidate.CapabilityId)!;
        var status = statuses.FirstOrDefault(s => s.Capability.Id == capability.Id);
        var reason = status is { Availability: not (CapabilityAvailability.Available or CapabilityAvailability.Partial) }
            ? status.Reason ?? BootPlanner.Describe(status.Availability)
            : capability.Description;
        return new BootAlternative { CapabilityId = capability.Id, Name = capability.Name, Reason = reason };
    }
}
