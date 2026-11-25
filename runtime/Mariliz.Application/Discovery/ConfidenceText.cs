using Mariliz.Core.Discovery;

namespace Mariliz.Application.Discovery;

/// <summary>A confiança da detecção como a pessoa lê, em português, nos textos que o motor escreve.</summary>
internal static class ConfidenceText
{
    public static string Of(Confidence confidence) => confidence switch
    {
        Confidence.High => "alta",
        Confidence.Medium => "média",
        _ => "baixa"
    };
}
