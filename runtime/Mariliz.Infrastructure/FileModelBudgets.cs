using System.Text.Json;
using System.Text.Json.Nodes;
using Mariliz.Application.Models;
using Mariliz.Infrastructure.Integrations;

namespace Mariliz.Infrastructure;

// Arquivo ausente ou ilegível quer dizer "tudo no padrão" ao ler. Ao gravar, um arquivo travado faz a gravação falhar
// (tratá-lo como vazio apagaria os outros tetos) e um conteúdo inválido vai para .corrupt-*. Remover a chave do modelo não mexe aqui: o teto não depende do provedor.
public sealed class FileModelBudgets(string dataRoot) : IModelBudgetStore
{
    private readonly object _gate = new();
    private string FilePath => Path.Combine(dataRoot, "model-budgets.json");

    public IReadOnlyDictionary<string, int> Read()
    {
        lock (_gate) return ReadFile(forWrite: false);
    }

    public void Write(string purpose, int? tokens)
    {
        lock (_gate)
        {
            var current = new Dictionary<string, int>(ReadFile(forWrite: true), StringComparer.Ordinal);
            if (tokens is { } value) current[purpose] = value;
            else current.Remove(purpose);
            Save(current);
        }
    }

    private Dictionary<string, int> ReadFile(bool forWrite)
    {
        var result = new Dictionary<string, int>(StringComparer.Ordinal);
        if (!File.Exists(FilePath)) return result;
        try
        {
            if (JsonNode.Parse(File.ReadAllText(FilePath)) is not JsonObject document) return result;
            foreach (var (purpose, node) in document)
                if (ModelBudgets.Find(purpose) is not null && node is JsonValue value && value.TryGetValue<int>(out var tokens))
                    result[purpose] = tokens;
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            if (forWrite) AtomicFile.Quarantine(FilePath);
            result.Clear();
        }
        catch (Exception ex) when (!forWrite && ex is IOException or UnauthorizedAccessException)
        {
            result.Clear();
        }
        return result;
    }

    private void Save(Dictionary<string, int> budgets)
    {
        if (budgets.Count == 0)
        {
            if (File.Exists(FilePath)) File.Delete(FilePath);
            return;
        }

        var document = new JsonObject();
        foreach (var (purpose, tokens) in budgets.OrderBy(pair => pair.Key, StringComparer.Ordinal)) document[purpose] = tokens;
        AtomicFile.Write(FilePath, document.ToJsonString());
    }
}
