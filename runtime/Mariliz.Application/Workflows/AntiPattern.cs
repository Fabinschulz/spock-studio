namespace Mariliz.Application.Workflows;

/// <param name="Problem">A coluna "Problema" da tabela "Não copiar" de uma seção do catálogo.</param>
/// <param name="Remedy">A coluna "O que fazer", quando a tabela tem uma.</param>
/// <param name="Technologies">As stacks da seção; a skill de cada stack junta os dela.</param>
public sealed record AntiPattern(string Problem, string? Remedy, IReadOnlyList<string> Technologies, string Source, DateTimeOffset AnalyzedAt);
