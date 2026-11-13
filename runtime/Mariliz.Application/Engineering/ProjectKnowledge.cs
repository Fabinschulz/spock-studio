namespace Mariliz.Application.Engineering;

/// <param name="State"><c>indexed</c>, <c>outdated</c> (mudou desde a indexação), <c>pending</c> (ainda não indexado) ou <c>unknown</c> (índice não lido).</param>
public sealed record KnowledgeFile(string Path, int Chars, string State);

// Só contagens e rótulos: o texto dos itens não sai do motor.
public sealed record KnowledgeExternalSource(
    string IntegrationId,
    string ProviderName,
    string ResourceName,
    bool Authorized,
    IReadOnlyList<string> DataKinds,
    int Items,
    int Documents,
    int Withheld,
    int Indexed,
    int Pending,
    int Outdated);

// Só contagens: o texto das decisões não sai do motor por esta resposta.
public sealed record KnowledgeMemory(int Documents, int Indexed, int Pending, int Outdated);

public sealed record KnowledgeExclusion(string Reason, string Label, int Count, IReadOnlyList<string> Examples);

// O conteúdo dos arquivos não sai do motor; aqui vão só caminhos e tamanhos.
public sealed record ProjectKnowledge(
    string ProjectId,
    DateTimeOffset InspectedAt,
    IReadOnlyList<string> Include,
    IReadOnlyList<string> Exclude,
    int Allowed,
    IReadOnlyList<KnowledgeFile> Files,
    IReadOnlyList<KnowledgeExclusion> Excluded,
    string IndexState,
    int Indexed,
    int Outdated,
    int Pending,
    int Orphaned,
    IReadOnlyList<KnowledgeExternalSource> External,
    KnowledgeMemory Memory);
