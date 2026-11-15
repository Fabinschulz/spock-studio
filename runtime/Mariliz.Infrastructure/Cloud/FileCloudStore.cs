using System.Text.Json;
using Mariliz.Application.Cloud;
using Mariliz.Application.Integrations;
using Mariliz.Infrastructure.Integrations;

namespace Mariliz.Infrastructure.Cloud;

// Só nomes e estados: nenhuma credencial passa por aqui. Arquivo travado falha; corrompido vai para .corrupt-*.
public sealed class FileCloudStore(string dataRoot) : ICloudStore
{
    private readonly object _gate = new();
    private string FilePath => Path.Combine(dataRoot, "cloud", "connections.json");

    public IReadOnlyList<CloudConnection> Connections()
    {
        lock (_gate) return Read().Connections;
    }

    public void SaveConnection(CloudConnection connection) =>
        Change(document => document with { Connections = Upsert(document.Connections, connection, c => c.Id == connection.Id) });

    public void RemoveConnection(string connectionId) =>
        Change(document => document with { Connections = document.Connections.Where(c => c.Id != connectionId).ToList() });

    public IReadOnlyList<CloudProjectLink> Links()
    {
        lock (_gate) return Read().Links;
    }

    public void SaveLink(CloudProjectLink link) =>
        Change(document => document with { Links = Upsert(document.Links, link, l => l.Id == link.Id) });

    public void RemoveLink(string linkId) =>
        Change(document => document with { Links = document.Links.Where(l => l.Id != linkId).ToList() });

    private void Change(Func<Document, Document> change)
    {
        lock (_gate) AtomicFile.Write(FilePath, JsonSerializer.Serialize(change(Read()), IntegrationJson.Options));
    }

    private Document Read() =>
        AtomicFile.ReadJson<Document>(FilePath, IntegrationJson.Options, document => document is { Connections: not null, Links: not null })
        ?? Document.Empty;

    private static List<T> Upsert<T>(List<T> items, T item, Func<T, bool> same)
    {
        var copy = items.ToList();
        var index = copy.FindIndex(existing => same(existing));
        if (index >= 0) copy[index] = item;
        else copy.Add(item);
        return copy;
    }

    private sealed record Document(List<CloudConnection> Connections, List<CloudProjectLink> Links)
    {
        public static Document Empty => new([], []);
    }
}
