namespace Mariliz.Application.Ports;

public sealed record ProcessSpec(
    string Executable,
    IReadOnlyList<string> Arguments,
    string WorkingDirectory,
    TimeSpan Timeout,
    string? StandardInput = null);
