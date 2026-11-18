using System.Text.Json;
using Mariliz.Application.Models;
using Mariliz.Infrastructure.Integrations;

namespace Mariliz.Infrastructure;

// A chave nunca passa por aqui. Arquivo ausente ou ilegível quer dizer "nunca conferida" ao ler. Ao gravar, um arquivo
// travado faz a gravação falhar (tratá-lo como vazio apagaria as outras conferências) e um conteúdo inválido vai para .corrupt-*.
public sealed class FileModelCheckStore(string dataRoot) : IModelCheckStore
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
    private readonly object _gate = new();
    private string FilePath => Path.Combine(dataRoot, "model-checks.json");

    public ModelCheck? Read(string provider)
    {
        lock (_gate) return ReadAll(forWrite: false).GetValueOrDefault(provider);
    }

    public void Write(string provider, ModelCheck check)
    {
        lock (_gate)
        {
            var all = ReadAll(forWrite: true);
            all[provider] = check;
            Save(all);
        }
    }

    public void Clear(string provider)
    {
        lock (_gate)
        {
            var all = ReadAll(forWrite: true);
            if (all.Remove(provider)) Save(all);
        }
    }

    private Dictionary<string, ModelCheck> ReadAll(bool forWrite)
    {
        if (!File.Exists(FilePath)) return [];
        try
        {
            var stored = JsonSerializer.Deserialize<Dictionary<string, ModelCheck>>(File.ReadAllText(FilePath), Options) ?? [];
            return stored.Where(entry => entry.Value is { Status: "connected" or "error" }).ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        }
        catch (JsonException)
        {
            if (forWrite) AtomicFile.Quarantine(FilePath);
            return [];
        }
        catch (Exception ex) when (!forWrite && ex is IOException or UnauthorizedAccessException)
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

        AtomicFile.Write(FilePath, JsonSerializer.Serialize(all, Options));
    }
}
