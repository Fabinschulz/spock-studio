using Mariliz.Application.Execution;

namespace Mariliz.Application.Ports;

public sealed record ActionOutcome(string Status);

// Só o motor executa; o runtime de IA nunca grava no projeto.
public interface IActionExecutor
{
    ActionOutcome Execute(string projectId, JournalAction action);
}
