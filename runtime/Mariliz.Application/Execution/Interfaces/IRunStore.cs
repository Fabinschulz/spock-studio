using System.Text.Json.Nodes;

namespace Mariliz.Application.Execution;

/// <summary>Guarda diários, eventos e backups fora do repositório do projeto.</summary>
public interface IRunStore
{
    void SaveJournal(RunJournal journal);
    RunJournal? LoadJournal(string projectId, string runId);
    IReadOnlyList<RunJournal> ListJournals(string projectId);

    /// <summary>Projetos que já têm algum diário, para varrer o que uma queda deixou pela metade.</summary>
    IReadOnlyList<string> ListProjectIds();
    void SaveBackup(string projectId, string runId, string relativePath, byte[] content);
    byte[]? LoadBackup(string projectId, string runId, string relativePath);

    /// <summary>Acrescenta um evento publicado (<c>{seq, type, data}</c>) ao histórico da execução.</summary>
    void AppendEvent(string projectId, string runId, JsonObject evt);

    /// <summary>Eventos gravados, na ordem em que foram publicados.</summary>
    IReadOnlyList<JsonObject> LoadEvents(string projectId, string runId);
}
