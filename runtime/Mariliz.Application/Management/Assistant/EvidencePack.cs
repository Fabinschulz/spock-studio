namespace Mariliz.Application.Management.Assistant;

public sealed record EvidenceFact(string Id, string Group, string Text);

// A interface mostra este pacote antes do envio.
public sealed record EvidencePack(
    string ProjectId,
    string ProjectName,
    DateTimeOffset GeneratedAt,
    IReadOnlyList<EvidenceFact> Facts,
    IReadOnlyList<string> Withheld);
