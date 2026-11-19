namespace Mariliz.Application.Workflows;

/// <param name="Section">A seção do arquivo da pasta Organização onde o padrão está ("Frontend React").</param>
/// <param name="Technologies">As stacks a que a seção vale; vazio quando vale para todas.</param>
/// <param name="Source">O título do arquivo da pasta Organização (frontmatter titulo).</param>
/// <param name="Details">As demais colunas da linha do padrão, na ordem da tabela.</param>
public sealed record ReferencePattern(
    string Id,
    string Title,
    string Section,
    IReadOnlyList<string> Technologies,
    string Source,
    DateTimeOffset AnalyzedAt,
    IReadOnlyList<ReferenceDetail> Details)
{
    public bool AppliesTo(IReadOnlyCollection<string> stacks) =>
        stacks.Count == 0 || Technologies.Count == 0 || Technologies.Any(stacks.Contains);
}
