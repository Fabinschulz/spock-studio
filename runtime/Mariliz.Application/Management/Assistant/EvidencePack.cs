namespace Mariliz.Application.Management.Assistant;

/// <summary>Um fato que o assistente pode citar. O texto é escrito pelo motor a partir do que ele mesmo calculou.</summary>
/// <param name="Group">Fontes, indicadores, previsão, riscos ou não calculado.</param>
public sealed record EvidenceFact(string Id, string Group, string Text);

/// <summary>
/// Tudo o que vai para o provedor de modelo numa pergunta, além da própria pergunta. A interface mostra este pacote
/// antes do envio. <see cref="Withheld"/> diz o que ficou de fora de propósito.
/// </summary>
public sealed record EvidencePack(
    string ProjectId,
    string ProjectName,
    DateTimeOffset GeneratedAt,
    IReadOnlyList<EvidenceFact> Facts,
    IReadOnlyList<string> Withheld);
