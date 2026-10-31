using System.Text.Json;
using System.Text.Json.Nodes;
using Mariliz.Application.Workspace;

namespace Mariliz.Infrastructure;

/// <summary>Lista de pastas autorizadas no diretório de dados, nunca dentro do projeto.</summary>
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
            Directory.CreateDirectory(dataRoot);
            var temp = FilePath + ".tmp";
            File.WriteAllText(temp, new JsonObject { ["projects"] = projects }.ToJsonString());
            File.Move(temp, FilePath, overwrite: true);
        }
    }

    private Dictionary<string, string> Read()
    {
        if (!File.Exists(FilePath)) return new Dictionary<string, string>(StringComparer.Ordinal);
        try
        {
            if (JsonNode.Parse(File.ReadAllText(FilePath)) is not JsonObject document) return new Dictionary<string, string>(StringComparer.Ordinal);
            if (document["projects"] is not JsonObject projects) return new Dictionary<string, string>(StringComparer.Ordinal);
            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var item in projects)
            {
                if (item.Value is JsonValue value && value.TryGetValue<string>(out var path) && !string.IsNullOrWhiteSpace(path))
                    map[item.Key] = path;
            }
            return map;
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }
    }
}
