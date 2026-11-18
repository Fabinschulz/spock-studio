namespace Mariliz.Application.Workflows;

/// <param name="Label">O nome da coluna na tabela do catálogo ("Onde", "Problema que resolve", "Evitar quando").</param>
public sealed record ReferenceDetail(string Label, string Text);
