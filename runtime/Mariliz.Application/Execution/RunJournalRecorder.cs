using System.Text.Json.Nodes;

namespace Mariliz.Application.Execution;

internal sealed class RunJournalRecorder(RunHandle run, RunJournal journal, IRunStore store)
{
    public void Save() => store.SaveJournal(journal);

    public JournalStep BeginStep((string Id, string Title) step) => BeginStep(step.Id, step.Title);

    public JournalStep BeginStep(string id, string title)
    {
        var step = new JournalStep { Id = id, Title = title, Status = StepStatus.Running };
        journal.Steps.Add(step);
        Save();
        run.Publish("step.started", new JsonObject { ["id"] = id, ["title"] = title });
        return step;
    }

    public void CompleteStep(JournalStep step, string status = StepStatus.Completed, string? detail = null)
    {
        step.Status = status;
        step.Detail = detail;
        Save();
        run.Publish("step.completed", new JsonObject { ["id"] = step.Id, ["status"] = status, ["detail"] = detail });
    }
}
