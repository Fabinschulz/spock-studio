namespace Mariliz.Application.Integrations;

// Nasce fechada: sincronizar não autoriza alimentar o conhecimento do projeto.
public sealed record ContextualizationPolicy(bool FeedsKnowledge)
{
    public static readonly ContextualizationPolicy Closed = new(false);
}
