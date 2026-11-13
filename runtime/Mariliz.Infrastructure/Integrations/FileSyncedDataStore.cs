using System.Collections.Concurrent;
using System.Text.Json;
using Mariliz.Application.Integrations;

namespace Mariliz.Infrastructure.Integrations;

// A pasta do projeto é a fronteira física do isolamento: não existe leitura que atravesse projetos.
public sealed class FileSyncedDataStore(string dataRoot) : ISyncedDataStore
{
    private readonly ConcurrentDictionary<string, object> _gates = new(StringComparer.Ordinal);

    public SyncedData Read(string projectId, string integrationId)
    {
        var path = IntegrationPaths.SyncedData(dataRoot, projectId, integrationId);
        lock (Gate(path))
        {
            return AtomicFile.ReadJson<SyncedData>(path, IntegrationJson.Options, data => data is { Items: not null, History: not null })
                ?? new SyncedData();
        }
    }

    public void Write(string projectId, string integrationId, SyncedData data)
    {
        var path = IntegrationPaths.SyncedData(dataRoot, projectId, integrationId);
        lock (Gate(path)) AtomicFile.Write(path, JsonSerializer.Serialize(data, IntegrationJson.Options));
    }

    public void Delete(string projectId, string integrationId)
    {
        var path = IntegrationPaths.SyncedData(dataRoot, projectId, integrationId);
        lock (Gate(path))
        {
            if (File.Exists(path)) File.Delete(path);
            if (File.Exists(path + ".tmp")) File.Delete(path + ".tmp");
        }
    }

    private object Gate(string path) => _gates.GetOrAdd(path, _ => new object());
}
