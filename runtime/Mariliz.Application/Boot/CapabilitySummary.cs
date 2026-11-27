namespace Mariliz.Application.Boot;

/// <param name="Headline">A frase que resume o resultado.</param>
/// <param name="Evidence">Linhas de apoio, só com contagens, estados e nomes de tecnologia lidos do JSON.</param>
public sealed record CapabilitySummary(string Headline, IReadOnlyList<string> Evidence);
