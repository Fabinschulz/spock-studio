namespace Mariliz.Core.Discovery;

// Lido das dependências declaradas de uma stack: a stack é a linguagem ou plataforma, o framework diz com o que ela é feita.
public sealed record FrameworkFinding(
    string Id,
    string Name,
    string StackId,
    string Root,
    string? Version,
    IReadOnlyList<Evidence> Evidence);
