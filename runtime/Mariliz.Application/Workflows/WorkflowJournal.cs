using Mariliz.Application.Execution;
using Mariliz.Application.Models;
using Mariliz.Application.Ports;

namespace Mariliz.Application.Workflows;

// Não conhece o runtime de IA nem o RunHandle; quem orquestra é o WorkflowService.
internal sealed class WorkflowJournal(IRunStore store, IClock clock)
{
    public RunJournal Begin(string runId, string projectId, string executionId)
    {
        var journal = new RunJournal
        {
            RunId = runId,
            ProjectId = projectId,
            Kind = WorkflowService.Kind,
            ExecutionId = executionId,
            State = RunStates.Running,
            StartedAt = clock.UtcNow
        };
        store.SaveJournal(journal);
        return journal;
    }

    public void Mark(RunJournal journal, string state)
    {
        journal.State = state;
        store.SaveJournal(journal);
    }

    public void Approve(RunJournal journal)
    {
        journal.ApprovedAt = clock.UtcNow;
        store.SaveJournal(journal);
    }

    public void RecordModelUsage(RunJournal journal, ModelUsage usage)
    {
        journal.ModelUsage = new JournalModelUsage { Calls = usage.Calls, InputTokens = usage.InputTokens, OutputTokens = usage.OutputTokens };
        store.SaveJournal(journal);
    }

    public void RecordJudgment(RunJournal journal, JournalJudgment judgment)
    {
        journal.Judgment = judgment;
        store.SaveJournal(journal);
    }

    // Se a execução falhou, nada aprovado chega a ser executado.
    public string Conclude(RunJournal journal, WorkflowAck ack, IActionExecutor? actions)
    {
        if (ack.Status == AckStatus.Failed) ActionGovernance.SkipPending(journal);
        else ActionGovernance.ExecuteApproved(journal, journal.ProjectId, actions, () => store.SaveJournal(journal));

        var outcome = ack.Status == AckStatus.Failed ? RunStates.Failed : RunStates.CompletedWithLimitations;
        journal.State = outcome;
        journal.FinishedAt = clock.UtcNow;
        journal.Error = outcome == RunStates.Failed ? (ack.Errors.FirstOrDefault() ?? ack.Limitations.FirstOrDefault())?.Code : null;
        journal.Evals = ack.Limitations.Select(item => new EvalSummary
        {
            Id = item.Code,
            Status = item.Code == "JUDGE_NOT_RUN" ? "not_run" : "recorded",
            Required = false
        }).ToList();
        if (journal.Judgment is { } judged)
            journal.Evals.Add(new EvalSummary { Id = JournalJudgment.EvalId, Status = judged.Outcome, Required = false });
        store.SaveJournal(journal);
        return outcome;
    }

    public void Fail(RunJournal journal, string code)
    {
        ActionGovernance.SkipPending(journal);
        journal.State = RunStates.Failed;
        journal.FinishedAt = clock.UtcNow;
        journal.Error = code;
        store.SaveJournal(journal);
    }

    public void Cancel(RunJournal journal)
    {
        ActionGovernance.SkipPending(journal);
        journal.State = RunStates.Cancelled;
        journal.FinishedAt = clock.UtcNow;
        store.SaveJournal(journal);
    }
}
