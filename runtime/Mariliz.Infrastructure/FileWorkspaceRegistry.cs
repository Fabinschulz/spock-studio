using System.Text.Json;
using System.Text.Json.Nodes;
using Mariliz.Application.Workspace;
using Mariliz.Infrastructure.Integrations;

namespace Mariliz.Infrastructure;

// Nunca dentro do projeto.
public sealed class FileWorkspaceRegistry(string dataRoot) : IWorkspaceRegistry
{
    private readonly object _gate = new();
    private string FilePath => Path.Combine(dataRoot, "workspaces.json");

    public IReadOnlyDictionary<string, string> Load()
    {
        lock (_gate) return Read();
    }

    public void Save(string projectId, string root)
    {
        lock (_gate)
        {
            var map = new Dictionary<string, string>(Read(), StringComparer.Ordinal) { [projectId] = root };
            var projects = new JsonObject();
            foreach (var (id, path) in map) projects[id] = path;
            AtomicFile.Write(FilePath, new JsonObject { ["projects"] = projects }.ToJsonString());
        }
    }

    private Dictionary<string, string> Read()
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!File.Exists(FilePath)) return map;
        // Arquivo travado: a IOException sobe (a próxima gravação reescreveria a lista inteira a partir de um vazio falso).
        var text = File.ReadAllText(FilePath);
        try
        {
            if (JsonNode.Parse(text) is not JsonObject { } document || document["projects"] is not JsonObject projects) return Corrupt(map);
            foreach (var item in projects)
            {
                if (item.Value is JsonValue value && value.TryGetValue<string>(out var path) && !string.IsNullOrWhiteSpace(path))
                    map[item.Key] = path;
            }
            return map;
        }
        catch (JsonException)
        {
            return Corrupt(map);
        }
    }

    private Dictionary<string, string> Corrupt(Dictionary<string, string> empty)
    {
        AtomicFile.Quarantine(FilePath);
        return empty;
    }
}
