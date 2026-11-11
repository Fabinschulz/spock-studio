namespace Mariliz.Application.Integrations;

/// <summary>
/// O que do dado sincronizado pode virar contexto de agentes. Nasce fechada: sincronizar não autoriza alimentar o conhecimento
/// do projeto. Quem for usar dado sincronizado como contexto precisa conferir esta política antes.
/// </summary>
public sealed record ContextualizationPolicy(bool FeedsKnowledge)
{
    public static readonly ContextualizationPolicy Closed = new(false);
}
