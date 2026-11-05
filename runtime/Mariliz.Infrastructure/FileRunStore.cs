using System.Text.Json;
using System.Text.Json.Nodes;
using Mariliz.Application.Execution;
using Mariliz.Core.Text;

namespace Mariliz.Infrastructure;

/// <summary>Diários e backups no diretório de dados do usuário, fora do repositório do projeto.</summary>
public sealed class FileRunStore(string dataRoot) : IRunStore
{
    private readonly object _events = new();

    public static string DefaultDataRoot()
    {
        var overrideDir = Environment.GetEnvironmentVariable("MARILIZ_DATA_DIR");
        if (!string.IsNullOrWhiteSpace(overrideDir)) return overrideDir;

        if (OperatingSystem.IsWindows())
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Mariliz");
        if (OperatingSystem.IsMacOS())
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Application Support", "Mariliz");

        var xdg = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        var baseDir = string.IsNullOrWhiteSpace(xdg) ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share") : xdg;
        return Path.Combine(baseDir, "mariliz");
    }

    private string RunDir(string projectId, string runId) => Path.Combine(dataRoot, "projects", Safe(projectId), "runs", Safe(runId));

    public void SaveJournal(RunJournal journal)
    {
        var dir = RunDir(journal.ProjectId, journal.RunId);
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "journal.json");
        var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(journal, JournalJson.Options));
        File.Move(temp, path, overwrite: true);
    }

    public RunJournal? LoadJournal(string projectId, string runId)
    {
        var path = Path.Combine(RunDir(projectId, runId), "journal.json");
        if (!File.Exists(path)) return null;
        try { return JsonSerializer.Deserialize<RunJournal>(File.ReadAllText(path), JournalJson.Options); }
        catch (JsonException) { return null; }
    }

    public IReadOnlyList<RunJournal> ListJournals(string projectId)
    {
        var dir = Path.Combine(dataRoot, "projects", Safe(projectId), "runs");
        if (!Directory.Exists(dir)) return [];
        return Directory.EnumerateDirectories(dir)
            .Select(d => LoadJournal(projectId, Path.GetFileName(d)))
            .Where(j => j is not null).Cast<RunJournal>().ToList();
    }

    public IReadOnlyList<string> ListProjectIds()
    {
        var dir = Path.Combine(dataRoot, "projects");
        if (!Directory.Exists(dir)) return [];
        // Uma pasta com nome fora do padrão não é de um projeto do motor: fica de fora em vez de derrubar a varredura.
        return Directory.EnumerateDirectories(dir).Select(Path.GetFileName).OfType<string>().Where(IsSafe).Order(StringComparer.Ordinal).ToList();
    }

    public void SaveBackup(string projectId, string runId, string relativePath, byte[] content)
    {
        var dir = Path.Combine(RunDir(projectId, runId), "backup");
        Directory.CreateDirectory(dir);
        File.WriteAllBytes(Path.Combine(dir, BackupName(relativePath)), content);
    }

    public byte[]? LoadBackup(string projectId, string runId, string relativePath)
    {
        var path = Path.Combine(RunDir(projectId, runId), "backup", BackupName(relativePath));
        return File.Exists(path) ? File.ReadAllBytes(path) : null;
    }

    public void AppendEvent(string projectId, string runId, JsonObject evt)
    {
        var dir = RunDir(projectId, runId);
        lock (_events)
        {
            Directory.CreateDirectory(dir);
            File.AppendAllText(Path.Combine(dir, "events.jsonl"), evt.ToJsonString() + "\n");
        }
    }

    public IReadOnlyList<JsonObject> LoadEvents(string projectId, string runId)
    {
        var path = Path.Combine(RunDir(projectId, runId), "events.jsonl");
        string[] lines;
        lock (_events)
        {
            if (!File.Exists(path)) return [];
            lines = File.ReadAllLines(path);
        }

        var events = new List<JsonObject>();
        foreach (var line in lines)
        {
            // Uma linha cortada por uma queda no meio da escrita fica de fora; as anteriores continuam válidas.
            try { if (JsonNode.Parse(line) is JsonObject item) events.Add(item); }
            catch (JsonException) { }
        }
        return events;
    }

    private static string BackupName(string relativePath) => Hashing.Sha256(relativePath) + ".bin";

    /// <summary>Os ids vêm do motor, mas a pasta é montada a partir deles: só caracteres seguros passam.</summary>
    private static string Safe(string id) => IsSafe(id) ? id : throw new ArgumentException("Identificador inválido.");

    private static bool IsSafe(string id) => id.Length > 0 && id.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');
}
