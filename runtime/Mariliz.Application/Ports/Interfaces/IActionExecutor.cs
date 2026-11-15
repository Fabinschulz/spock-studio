using Mariliz.Application.Execution;

namespace Mariliz.Application.Ports;

// CreatedPath: o arquivo que esta execução criou, para entrar no diário como operação e poder ser desfeito.
public sealed record ActionOutcome(string Status, string? CreatedPath = null);

// Só o motor executa; o runtime de IA nunca grava no projeto.
public interface IActionExecutor
{
    ActionOutcome Execute(string projectId, JournalAction action);
}
