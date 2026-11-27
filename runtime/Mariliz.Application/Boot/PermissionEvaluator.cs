namespace Mariliz.Application.Boot;

public enum PermissionVerdict
{
    // O Boot pode executar agora.
    Allowed,
    // O Boot só prepara e abre a tela; a aprovação e a execução são dela.
    HandoffOnly,
    Denied
}

public sealed record PermissionDecision(PermissionVerdict Verdict, string Reason, AutonomyLevel RequiredLevel, bool NeedsApproval);

/// <summary>
/// Uma só avaliação de permissão para toda capacidade. É conservadora de propósito: o teto da fundação é o nível 1, então nada
/// que altere o projeto ou a nuvem passa de "preparar e abrir a tela". O serviço que executa de verdade continua conferindo o
/// que sempre conferiu (pasta autorizada, aprovação presa ao hash, confirmações de destruição); o Boot não enfraquece nenhum.
/// </summary>
public static class PermissionEvaluator
{
    // Níveis 3 e 4 existem no vocabulário, mas só entram com política explícita da organização, que o produto ainda não tem.
    public const AutonomyLevel Ceiling = AutonomyLevel.Preparation;

    public static AutonomyLevel RequiredLevel(OperationClass operation) => operation switch
    {
        OperationClass.Read or OperationClass.Query or OperationClass.Analysis => AutonomyLevel.Observation,
        OperationClass.Preparation => AutonomyLevel.Preparation,
        OperationClass.Configuration or OperationClass.InternalChange or OperationClass.ExternalChange or OperationClass.Sensitive => AutonomyLevel.Assisted,
        _ => AutonomyLevel.Governed
    };

    public static PermissionDecision Evaluate(Capability capability, CapabilityStatus status, AutonomyLevel requested)
    {
        var required = RequiredLevel(capability.Operation);
        if (status.Availability is not (CapabilityAvailability.Available or CapabilityAvailability.Partial))
            return new PermissionDecision(PermissionVerdict.Denied, status.Reason ?? "A capacidade não está disponível.", required, false);

        // O pedido nunca passa do teto, mesmo que a tela peça mais.
        var level = requested > Ceiling ? Ceiling : requested;
        var needsApproval = required >= AutonomyLevel.Assisted;

        if (capability.Mode == ExecutionMode.Direct && required <= level)
            return new PermissionDecision(PermissionVerdict.Allowed, "Leitura ou análise que não altera o projeto.", required, false);

        if (capability.Mode == ExecutionMode.Direct)
            return new PermissionDecision(PermissionVerdict.HandoffOnly, "No nível de observação o Boot só lê: a preparação fica para você pedir na tela.", required, false);

        var reason = capability.Operation switch
        {
            OperationClass.Destructive => "Pode destruir recursos: só a tela executa, com a aprovação do plano e as confirmações extras.",
            OperationClass.ExternalChange or OperationClass.Sensitive => "Altera algo fora do projeto: só a tela executa, depois da sua aprovação.",
            OperationClass.InternalChange => "Altera o projeto: a tela mostra o que muda e só grava o que você aprovar.",
            OperationClass.Configuration => "Altera uma configuração: a tela mostra o que muda e só aplica o que você aprovar.",
            _ => "A tela que já existe faz o trabalho."
        };
        return new PermissionDecision(PermissionVerdict.HandoffOnly, reason, required, needsApproval);
    }
}
