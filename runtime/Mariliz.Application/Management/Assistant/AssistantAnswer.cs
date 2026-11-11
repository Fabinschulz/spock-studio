namespace Mariliz.Application.Management.Assistant;

/// <summary>Uma afirmação da resposta e os fatos do pacote que a sustentam.</summary>
public sealed record AssistantStatement(string Text, IReadOnlyList<string> Evidence);

/// <summary>Uma afirmação que o modelo escreveu e a verificação do motor descartou, com o motivo.</summary>
public sealed record RejectedStatement(string Section, string Text, string Reason);

/// <summary>
/// A resposta do assistente, já separada como o pedido exige: dados observados, cálculos, inferências, recomendações e
/// o que não está disponível. Só passa o que cita fatos do pacote e não traz número que os fatos não tenham.
/// </summary>
public sealed record AssistantAnswer(
    string ProjectId,
    DateTimeOffset AnsweredAt,
    string Summary,
    IReadOnlyList<AssistantStatement> Observed,
    IReadOnlyList<AssistantStatement> Calculations,
    IReadOnlyList<AssistantStatement> Inferences,
    IReadOnlyList<AssistantStatement> Recommendations,
    IReadOnlyList<string> Unavailable,
    IReadOnlyList<RejectedStatement> Rejected,
    IReadOnlyList<EvidenceFact> Evidence,
    string Model,
    int InputTokens,
    int OutputTokens);
