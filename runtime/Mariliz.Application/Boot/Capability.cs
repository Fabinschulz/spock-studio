namespace Mariliz.Application.Boot;

/// <summary>
/// Um termo que sugere a capacidade. <paramref name="Text"/> vem sem acento e em minúsculas; uma palavra pode terminar em
/// "*" (prefixo) e uma frase casa quando as palavras aparecem em ordem, com até duas no meio.
/// </summary>
public sealed record IntentTerm(string Text, int Weight);

/// <summary>
/// O que o Spock sabe fazer. A ficha descreve; quem autoriza é o <see cref="PermissionEvaluator"/> e quem executa é o serviço
/// do domínio que já existe: estar aqui não libera nada.
/// </summary>
public sealed record Capability(
    string Id,
    string Name,
    string Description,
    string Domain,
    IReadOnlyList<string> Goals,
    IReadOnlyList<IntentTerm> Terms,
    IReadOnlyList<string> Hints,
    CapabilityNeeds Needs,
    OperationClass Operation,
    ExecutionMode Mode,
    // Direct: o método executado agora. Handoff: o que a tela chamará (informativo). Composite: nenhum.
    string? Method,
    // Todos os métodos do motor que a capacidade usa; é o que prova que ela existe de verdade.
    IReadOnlyList<string> Methods,
    // Caminho da tela que já existe para esta capacidade.
    string? Route,
    IReadOnlyList<string> Agents,
    IReadOnlyList<string> Skills,
    IReadOnlyList<string> Workflows,
    string ExpectedResult,
    IReadOnlyList<string> Validations,
    IReadOnlyList<string> Preconditions,
    IReadOnlyList<string> Composes)
{
    public bool IsComposite => Mode == ExecutionMode.Composite;
}
