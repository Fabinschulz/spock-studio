using Mariliz.Application.Execution;
using Mariliz.Application.Models;
using Mariliz.Application.Ports;
using Mariliz.Application.Workspace;

namespace Mariliz.Application.Management.Investigation;

/// <summary>
/// O diário da investigação: o caminho como etapas (com os ids dos fatos consultados), contagens e o código do erro, <b>nunca o
/// texto da pergunta, das hipóteses nem da resposta</b>. Não passa pelo rastreador: uma investigação só lê. Só projeto autorizado
/// tem diário, e uma falha ao gravar nunca atrapalha a resposta.
/// </summary>
internal sealed class InvestigationJournal(IRunStore? runs, WorkspaceAuthorizer workspace, IClock clock)
{
    public void Save(string projectId, string investigationId, string executionId, DateTimeOffset startedAt, ProjectInvestigation? result, string? error, ModelUsage? usage)
    {
        if (runs is null || !workspace.IsAuthorized(projectId)) return;

        var journal = new RunJournal
        {
            RunId = investigationId,
            ProjectId = projectId,
            Kind = RunKinds.Investigation,
            ExecutionId = executionId,
            StartedAt = startedAt,
            FinishedAt = clock.UtcNow
        };
        if (result is null)
        {
            journal.State = RunStates.Failed;
            journal.Error = error;
        }
        else
        {
            Describe(journal, result);
        }

        if (usage is { Calls: > 0 })
            journal.ModelUsage = new JournalModelUsage { Calls = usage.Calls, InputTokens = usage.InputTokens, OutputTokens = usage.OutputTokens };
        try
        {
            runs.SaveJournal(journal);
        }
        catch (IOException)
        {
            // A resposta já foi dada; o registro é só histórico.
        }
    }

    private static void Describe(RunJournal journal, ProjectInvestigation result)
    {
        var answer = result.Answer;
        var accepted = answer.Observed.Count + answer.Calculations.Count + answer.Inferences.Count + answer.Recommendations.Count;
        journal.State = answer.Rejected.Count > 0 || result.Limitations.Count > 0 ? RunStates.CompletedWithLimitations : RunStates.Completed;
        foreach (var (step, index) in result.Path.Select((step, index) => (step, index)))
            journal.Steps.Add(new JournalStep
            {
                Id = $"{step.Id}{(step.Round > 0 ? step.Round : "")}-{index}",
                Title = step.Title,
                Status = StepStatus.Completed,
                Detail = step.Detail + (step.Facts.Count > 0 ? $" Fatos: {string.Join(", ", step.Facts)}." : "")
            });
        journal.Steps.Add(new JournalStep
        {
            Id = "verify",
            Title = "Conferência do motor",
            Status = StepStatus.Completed,
            Detail = $"{accepted} afirmações aceitas, {answer.Rejected.Count} descartadas, {answer.Evidence.Count} fatos citados"
        });
        journal.Pending.AddRange(result.Limitations.Select(limitation => limitation.Message));
        if (answer.Rejected.Count > 0) journal.Pending.Add($"{answer.Rejected.Count} afirmações foram descartadas pela conferência do motor.");
    }
}
