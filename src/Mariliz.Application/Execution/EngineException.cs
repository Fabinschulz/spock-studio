namespace Mariliz.Application.Execution;

public sealed class EngineException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
