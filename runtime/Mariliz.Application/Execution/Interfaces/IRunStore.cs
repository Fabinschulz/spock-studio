using System.Text.Json.Nodes;

namespace Mariliz.Application.Execution;

// Fora do repositório do projeto.
public interface IRunStore
{
    void SaveJournal(RunJournal journal);
    RunJournal? LoadJournal(string projectId, string runId);
    IReadOnlyList<RunJournal> ListJournals(string projectId);

    // Para varrer o que uma queda deixou pela metade.
    IReadOnlyList<string> ListProjectIds();
    void SaveBackup(string projectId, string runId, string relativePath, byte[] content);
    byte[]? LoadBackup(string projectId, string runId, string relativePath);

    void AppendEvent(string projectId, string runId, JsonObject evt);

    IReadOnlyList<JsonObject> LoadEvents(string projectId, string runId);
}
