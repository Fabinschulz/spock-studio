namespace Mariliz.Application.Ports;

public sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError, bool TimedOut, bool NotFound);
