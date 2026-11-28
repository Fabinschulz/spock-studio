using System.Text.Json;
using Mariliz.Application.Integrations;
using Mariliz.Application.Quality;
using Mariliz.Core.Text;
using Mariliz.Infrastructure.Integrations;

namespace Mariliz.Infrastructure;

// Um arquivo por revisão, em projects/<id>/reviews/. Gravado inteiro e de uma vez; um arquivo ilegível vai para .corrupt-* e some
// da lista, sem derrubar os outros (o mesmo cuidado dos registros do Boot).
public sealed class FileReviewStore(string dataRoot) : IReviewStore
{
    private readonly object _gate = new();

    public void Save(ReviewRecord record)
    {
        if (!ReviewIds.IsValid(record.Id)) throw new InvalidOperationException("Id de revisão inválido.");
        lock (_gate) AtomicFile.Write(PathOf(record.ProjectId, record.Id), JsonSerializer.Serialize(record, IntegrationJson.Options));
    }

    public ReviewRecord? Find(string projectId, string reviewId)
    {
        if (!ReviewIds.IsValid(reviewId)) return null;
        lock (_gate) return Read(PathOf(projectId, reviewId));
    }

    public IReadOnlyList<ReviewRecord> List(string projectId)
    {
        lock (_gate)
        {
            var folder = FolderOf(projectId);
            if (!Directory.Exists(folder)) return [];
            var records = new List<ReviewRecord>();
            foreach (var file in Directory.EnumerateFiles(folder, "rev-*.json"))
                if (Read(file) is { } record) records.Add(record);
            return [.. records.OrderByDescending(record => record.CreatedAt).ThenByDescending(record => record.Id, StringComparer.Ordinal)];
        }
    }

    public bool Delete(string projectId, string reviewId)
    {
        if (!ReviewIds.IsValid(reviewId)) return false;
        lock (_gate)
        {
            var path = PathOf(projectId, reviewId);
            if (!File.Exists(path)) return false;
            File.Delete(path);
            return true;
        }
    }

    public DecisionLog LoadDecisions(string projectId)
    {
        lock (_gate)
            return AtomicFile.ReadJson<DecisionLog>(Path.Combine(FolderOf(projectId), "decisions.json"), IntegrationJson.Options, log => log.Findings is not null && log.Deliveries is not null) ?? DecisionLog.Empty;
    }

    public void SaveDecisions(string projectId, DecisionLog log)
    {
        lock (_gate) AtomicFile.Write(Path.Combine(FolderOf(projectId), "decisions.json"), JsonSerializer.Serialize(log, IntegrationJson.Options));
    }

    private string FolderOf(string projectId)
    {
        // O id vira nome de pasta: só o formato exato de um id de projeto passa.
        if (!Ids.IsProjectId(projectId)) throw new InvalidOperationException("Id de projeto inválido.");
        return Path.Combine(dataRoot, "projects", projectId, "reviews");
    }

    private string PathOf(string projectId, string reviewId) => Path.Combine(FolderOf(projectId), reviewId + ".json");

    private static ReviewRecord? Read(string path) =>
        AtomicFile.ReadJson<ReviewRecord>(path, IntegrationJson.Options,
            record => ReviewIds.IsValid(record.Id) && record.Contract == ReviewContract.Version && Path.GetFileNameWithoutExtension(path) == record.Id);
}
