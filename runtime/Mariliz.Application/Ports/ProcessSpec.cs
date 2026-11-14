namespace Mariliz.Application.Ports;

/// <param name="OnOutputLine">Recebe cada linha da saída (padrão e de erro) assim que chega, para quem acompanha ao vivo.</param>
public sealed record ProcessSpec(
    string Executable,
    IReadOnlyList<string> Arguments,
    string WorkingDirectory,
    TimeSpan Timeout,
    string? StandardInput = null,
    Action<string>? OnOutputLine = null);
