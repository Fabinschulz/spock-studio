namespace Mariliz.Application.Engineering;

/// <summary>Um arquivo que entra no conhecimento do projeto e como ele está no índice.</summary>
/// <param name="State"><c>indexed</c>, <c>outdated</c> (mudou desde a indexação), <c>pending</c> (ainda não indexado) ou <c>unknown</c> (índice não lido).</param>
public sealed record KnowledgeFile(string Path, int Chars, string State);

/// <summary>Um motivo pelo qual arquivos ficaram fora do conhecimento, com quantos e alguns exemplos.</summary>
public sealed record KnowledgeExclusion(string Reason, string Label, int Count, IReadOnlyList<string> Examples);

/// <summary>
/// O conhecimento do projeto como o índice o recebe: as regras, os arquivos que entram, o que ficou de fora e por quê, e
/// o estado do índice. O conteúdo dos arquivos não sai do motor; aqui vão só caminhos e tamanhos.
/// </summary>
/// <param name="IndexState"><c>read</c>, <c>runtimeStopped</c>, <c>runtimeUnavailable</c> ou <c>failed</c>.</param>
/// <param name="Orphaned">Arquivos no índice que não são mais fonte (apagados ou excluídos desde a indexação).</param>
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
    int Orphaned);
