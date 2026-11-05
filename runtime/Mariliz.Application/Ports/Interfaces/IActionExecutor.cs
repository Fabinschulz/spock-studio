using Mariliz.Application.Execution;

namespace Mariliz.Application.Ports;

/// <summary>Resultado de executar uma ação aprovada: um dos estados finais de <see cref="JournalAction"/>.</summary>
public sealed record ActionOutcome(string Status);

/// <summary>Quem executa as ações aprovadas. Só o motor executa; o runtime de IA nunca grava no projeto.</summary>
public interface IActionExecutor
{
    ActionOutcome Execute(string projectId, JournalAction action);
}
