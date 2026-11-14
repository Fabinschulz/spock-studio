using Mariliz.Application.Engineering;

namespace Mariliz.Application.Context;

public sealed record ContextModel(bool Configured, string? Provider, string? Model);

public sealed record ContextCounting(bool Exact, string Method);

public sealed record ContextSection(string Id, string Label, int Tokens);

/// <param name="WithinBudget">Somando as chamadas anteriores, esta ainda cabe no orçamento da execução.</param>
public sealed record ContextCall(string AgentId, string Label, int InputTokens, int OutputTokens, bool WithinBudget, IReadOnlyList<ContextSection> Sections);

/// <param name="Included">Entrou no bloco de trechos; os que não cabem ficam fora do pedido ao modelo.</param>
/// <param name="Truncated">O trecho passou do tamanho máximo e entrou cortado.</param>
public sealed record ContextEvidence(string Path, int Tokens, bool Included, bool Truncated);

public sealed record ContextLeftOut(int CutBySize, int Truncated, int Pending, IReadOnlyList<KnowledgeExclusion> Excluded);

// Só caminhos e números: o texto do projeto não sai do motor por esta resposta.
public sealed record ContextReview(
    string ProjectId,
    ContextModel Model,
    ContextCounting Counting,
    int Budget,
    int PlannedTokens,
    IReadOnlyList<ContextCall> Calls,
    IReadOnlyList<ContextEvidence> Evidence,
    ContextLeftOut LeftOut);
