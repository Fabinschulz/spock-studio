using System.Text.Json;
using System.Text.Json.Nodes;
using Mariliz.Application.Workspace;
using Mariliz.Infrastructure.Integrations;

namespace Mariliz.Infrastructure;

// Nunca dentro do projeto. "projects" segue no formato de antes (id -> caminho): uma versão anterior do aplicativo
// continua lendo o arquivo; a seção nova só some se ela regravar.
public sealed class FileWorkspaceRegistry(string dataRoot) : IWorkspaceRegistry
{
    private const string ProjectsKey = "projects";
    private const string ProposalsKey = "proposedStableIds";

    private readonly object _gate = new();
    private string FilePath => Path.Combine(dataRoot, "workspaces.json");

    public IReadOnlyDictionary<string, string> Load()
    {
        lock (_gate) return Read().Projects;
    }

    public void Save(string projectId, string root)
    {
        lock (_gate)
        {
            var (projects, proposals) = Read();
            projects[projectId] = root;
            Write(projects, proposals);
        }
    }

    public IReadOnlyDictionary<string, string> LoadProposedStableIds()
    {
        lock (_gate) return Read().Proposals;
    }

    public void SaveProposedStableId(string projectId, string stableId)
    {
        lock (_gate)
        {
            var (projects, proposals) = Read();
            proposals[projectId] = stableId;
            Write(projects, proposals);
        }
    }

    private void Write(Dictionary<string, string> projects, Dictionary<string, string> proposals)
    {
        var document = new JsonObject { [ProjectsKey] = Section(projects) };
        if (proposals.Count > 0) document[ProposalsKey] = Section(proposals);
        AtomicFile.Write(FilePath, document.ToJsonString());
    }

    private static JsonObject Section(Dictionary<string, string> map)
    {
        var section = new JsonObject();
        foreach (var (key, value) in map) section[key] = value;
        return section;
    }

    private (Dictionary<string, string> Projects, Dictionary<string, string> Proposals) Read()
    {
        var projects = new Dictionary<string, string>(StringComparer.Ordinal);
        var proposals = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!File.Exists(FilePath)) return (projects, proposals);
        // Arquivo travado: a IOException sobe (a próxima gravação reescreveria a lista inteira a partir de um vazio falso).
        var text = File.ReadAllText(FilePath);
        try
        {
            if (JsonNode.Parse(text) is not JsonObject { } document || document[ProjectsKey] is not JsonObject section) return Corrupt(projects, proposals);
            Fill(projects, section);
            if (document[ProposalsKey] is JsonObject proposed) Fill(proposals, proposed);
            return (projects, proposals);
        }
        catch (JsonException)
        {
            return Corrupt(projects, proposals);
        }
    }

    private static void Fill(Dictionary<string, string> map, JsonObject section)
    {
        foreach (var item in section)
        {
            if (item.Value is JsonValue value && value.TryGetValue<string>(out var text) && !string.IsNullOrWhiteSpace(text))
                map[item.Key] = text;
        }
    }

    private (Dictionary<string, string>, Dictionary<string, string>) Corrupt(Dictionary<string, string> projects, Dictionary<string, string> proposals)
    {
        AtomicFile.Quarantine(FilePath);
        return (projects, proposals);
    }
}
