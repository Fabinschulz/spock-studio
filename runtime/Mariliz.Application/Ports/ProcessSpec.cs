namespace Mariliz.Application.Ports;

/// <param name="OnOutputLine">Recebe cada linha da saída (padrão e de erro) assim que chega, para quem acompanha ao vivo.</param>
/// <param name="Environment">Variáveis que o motor define para o filho, por cima das herdadas; valem mesmo com cara de segredo.</param>
/// <param name="InheritedSecrets">
/// Variáveis com cara de segredo que o filho pode herdar do motor. As demais ficam de fora (R-10); só se nomeia aqui a
/// credencial que a própria ferramenta lê para o seu login.
/// </param>
public sealed record ProcessSpec(
    string Executable,
    IReadOnlyList<string> Arguments,
    string WorkingDirectory,
    TimeSpan Timeout,
    string? StandardInput = null,
    Action<string>? OnOutputLine = null,
    IReadOnlyDictionary<string, string>? Environment = null,
    IReadOnlyCollection<string>? InheritedSecrets = null);
