using System.Text.Json;
using Mariliz.Application.Boot;
using Mariliz.Application.Integrations;
using Mariliz.Core.Text;
using Mariliz.Infrastructure.Integrations;

namespace Mariliz.Infrastructure;

// Um arquivo por Boot, em projects/<id>/boots/ (ou em boots/, para o Boot sem projeto). Gravado inteiro e de uma vez, então uma
// queda no meio de um checkpoint deixa o anterior. Um arquivo ilegível vai para .corrupt-* e some da lista, sem derrubar os outros.
public sealed class FileBootStore(string dataRoot) : IBootStore
{
    private readonly object _gate = new();

    public void Save(BootRecord record)
    {
        if (!BootIds.IsValid(record.Id)) throw new InvalidOperationException("Id de Boot inválido.");
        lock (_gate) AtomicFile.Write(PathOf(record.ProjectId, record.Id), JsonSerializer.Serialize(record, IntegrationJson.Options));
    }

    public BootRecord? Find(string? projectId, string bootId)
    {
        if (!BootIds.IsValid(bootId)) return null;
        lock (_gate) return Read(PathOf(projectId, bootId));
    }

    public IReadOnlyList<BootRecord> List(string? projectId)
    {
        lock (_gate) return ReadFolder(FolderOf(projectId));
    }

    public IReadOnlyList<BootRecord> ListAll()
    {
        lock (_gate)
        {
            var all = new List<BootRecord>(ReadFolder(FolderOf(null)));
            var projects = Path.Combine(dataRoot, "projects");
            if (Directory.Exists(projects))
                foreach (var folder in Directory.EnumerateDirectories(projects).Where(folder => Ids.IsProjectId(Path.GetFileName(folder))))
                    all.AddRange(ReadFolder(Path.Combine(folder, "boots")));
            return [.. all.OrderByDescending(record => record.CreatedAt)];
        }
    }

    public int Count(string? projectId)
    {
        lock (_gate)
        {
            var folder = FolderOf(projectId);
            return Directory.Exists(folder) ? Directory.EnumerateFiles(folder, "boot-*.json").Count() : 0;
        }
    }

    public bool Delete(string? projectId, string bootId)
    {
        if (!BootIds.IsValid(bootId)) return false;
        lock (_gate)
        {
            var path = PathOf(projectId, bootId);
            if (!File.Exists(path)) return false;
            File.Delete(path);
            return true;
        }
    }

    private string FolderOf(string? projectId)
    {
        if (projectId is null) return Path.Combine(dataRoot, "boots");
        // O id vira nome de pasta: só o formato exato de um id de projeto passa.
        if (!Ids.IsProjectId(projectId)) throw new InvalidOperationException("Id de projeto inválido.");
        return Path.Combine(dataRoot, "projects", projectId, "boots");
    }

    private string PathOf(string? projectId, string bootId) => Path.Combine(FolderOf(projectId), bootId + ".json");

    private IReadOnlyList<BootRecord> ReadFolder(string folder)
    {
        if (!Directory.Exists(folder)) return [];
        var records = new List<BootRecord>();
        foreach (var file in Directory.EnumerateFiles(folder, "boot-*.json"))
            if (Read(file) is { } record) records.Add(record);
        return [.. records.OrderByDescending(record => record.CreatedAt).ThenByDescending(record => record.Id, StringComparer.Ordinal)];
    }

    private static BootRecord? Read(string path) =>
        AtomicFile.ReadJson<BootRecord>(path, IntegrationJson.Options, record => BootIds.IsValid(record.Id) && Path.GetFileNameWithoutExtension(path) == record.Id);
}
