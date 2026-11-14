using System.Text.Json;
using Mariliz.Application.Models;

namespace Mariliz.Infrastructure;

// A chave nunca passa por aqui. Arquivo ausente ou ilegível quer dizer "nunca conferida".
public sealed class FileModelCheckStore(string dataRoot) : IModelCheckStore
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
    private readonly object _gate = new();
    private string FilePath => Path.Combine(dataRoot, "model-checks.json");

    public ModelCheck? Read(string provider)
    {
        lock (_gate) return ReadAll().GetValueOrDefault(provider);
    }

    public void Write(string provider, ModelCheck check)
    {
        lock (_gate)
        {
            var all = ReadAll();
            all[provider] = check;
            Save(all);
        }
    }

    public void Clear(string provider)
    {
        lock (_gate)
        {
            var all = ReadAll();
            if (all.Remove(provider)) Save(all);
        }
    }

    private Dictionary<string, ModelCheck> ReadAll()
    {
        if (!File.Exists(FilePath)) return [];
        try
        {
            var stored = JsonSerializer.Deserialize<Dictionary<string, ModelCheck>>(File.ReadAllText(FilePath), Options) ?? [];
            return stored.Where(entry => entry.Value is { Status: "connected" or "error" }).ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    private void Save(Dictionary<string, ModelCheck> all)
    {
        if (all.Count == 0)
        {
            if (File.Exists(FilePath)) File.Delete(FilePath);
            return;
        }

        Directory.CreateDirectory(dataRoot);
        var temp = FilePath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(all, Options));
        File.Move(temp, FilePath, overwrite: true);
    }
}
