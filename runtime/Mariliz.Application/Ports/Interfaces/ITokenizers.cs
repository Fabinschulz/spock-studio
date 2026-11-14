namespace Mariliz.Application.Ports;

// Exata só quando o tokenizador é o do próprio modelo; o resto é estimativa e aparece assim para a pessoa.
public sealed record TokenCounter(Func<string, int> Count, bool Exact, string Method);

public interface ITokenizers
{
    TokenCounter For(string? provider, string? model);
}
