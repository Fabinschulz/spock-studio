using System.Text.Json;
using Mariliz.Application.Integrations;

namespace Mariliz.Infrastructure.Integrations;

/// <summary>
/// Conexões e vínculos em <c>{dataRoot}/integrations/connections.json</c>. O arquivo não tem credencial: ela fica no cofre.
/// A gravação troca o arquivo inteiro de uma vez. Um arquivo travado falha (não vira lista vazia); um corrompido vai para `.corrupt-*`.
/// </summary>
public sealed class FileIntegrationStore(string dataRoot) : IIntegrationStore
{
    private readonly object _gate = new();
    private string FilePath => Path.Combine(IntegrationPaths.Root(dataRoot), "connections.json");

    public IReadOnlyList<IntegrationConnection> Connections()
    {
        lock (_gate) return Read().Connections;
    }

    public void SaveConnection(IntegrationConnection connection) =>
        Change(document => document with { Connections = Upsert(document.Connections, connection, c => c.Id == connection.Id) });

    public void RemoveConnection(string connectionId) =>
        Change(document => document with { Connections = document.Connections.Where(c => c.Id != connectionId).ToList() });

    public IReadOnlyList<ProjectIntegration> Integrations()
    {
        lock (_gate) return Read().Integrations;
    }

    public void SaveIntegration(ProjectIntegration integration) =>
        Change(document => document with { Integrations = Upsert(document.Integrations, integration, i => i.Id == integration.Id) });

    public void RemoveIntegration(string integrationId) =>
        Change(document => document with { Integrations = document.Integrations.Where(i => i.Id != integrationId).ToList() });

    private void Change(Func<Document, Document> change)
    {
        lock (_gate) AtomicFile.Write(FilePath, JsonSerializer.Serialize(change(Read()), IntegrationJson.Options));
    }

    private Document Read() =>
        AtomicFile.ReadJson<Document>(FilePath, IntegrationJson.Options, document => document is { Connections: not null, Integrations: not null })
        ?? Document.Empty;

    private static List<T> Upsert<T>(List<T> items, T item, Func<T, bool> same)
    {
        var copy = items.ToList();
        var index = copy.FindIndex(existing => same(existing));
        if (index >= 0) copy[index] = item;
        else copy.Add(item);
        return copy;
    }

    private sealed record Document(List<IntegrationConnection> Connections, List<ProjectIntegration> Integrations)
    {
        public static Document Empty => new([], []);
    }
}
