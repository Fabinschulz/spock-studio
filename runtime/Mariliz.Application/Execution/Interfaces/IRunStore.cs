namespace Mariliz.Application.Execution;

/// <summary>Guarda diários e backups fora do repositório do projeto.</summary>
public interface IRunStore
{
    void SaveJournal(RunJournal journal);
    RunJournal? LoadJournal(string projectId, string runId);
    IReadOnlyList<RunJournal> ListJournals(string projectId);
    void SaveBackup(string projectId, string runId, string relativePath, byte[] content);
    byte[]? LoadBackup(string projectId, string runId, string relativePath);
}
