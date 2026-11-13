using System.Text.Json;
using System.Text.Json.Nodes;
using Mariliz.Application.Models;

namespace Mariliz.Infrastructure;

/// <summary>
/// O modelo escolhido para cada finalidade, em <c>{dataRoot}/model-purposes.json</c>. É só preferência (nome de modelo): a chave
/// nunca passa por aqui. Arquivo ausente ou ilegível quer dizer "tudo no padrão".
/// </summary>
public sealed class FileModelPreferences(string dataRoot) : IModelPreferences
{
    private readonly object _gate = new();
    private string FilePath => Path.Combine(dataRoot, "model-purposes.json");

    public IReadOnlyDictionary<string, string> Read()
    {
        lock (_gate) return ReadFile();
    }

    public void Write(string purpose, string? model)
    {
        lock (_gate)
        {
            var current = new Dictionary<string, string>(ReadFile(), StringComparer.Ordinal);
            if (model is null) current.Remove(purpose);
            else current[purpose] = model;
            Save(current);
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            if (File.Exists(FilePath)) File.Delete(FilePath);
            var temp = FilePath + ".tmp";
            if (File.Exists(temp)) File.Delete(temp);
        }
    }

    private Dictionary<string, string> ReadFile()
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!File.Exists(FilePath)) return result;
        try
        {
            if (JsonNode.Parse(File.ReadAllText(FilePath)) is not JsonObject document) return result;
            foreach (var (purpose, node) in document)
                if (ModelPurposes.Find(purpose) is not null && node is JsonValue value && value.TryGetValue<string>(out var model) && model.Length > 0)
                    result[purpose] = model;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            result.Clear();
        }
        return result;
    }

    private void Save(Dictionary<string, string> choices)
    {
        if (choices.Count == 0)
        {
            Clear();
            return;
        }

        Directory.CreateDirectory(dataRoot);
        var document = new JsonObject();
        foreach (var (purpose, model) in choices.OrderBy(pair => pair.Key, StringComparer.Ordinal)) document[purpose] = model;
        var temp = FilePath + ".tmp";
        File.WriteAllText(temp, document.ToJsonString());
        File.Move(temp, FilePath, overwrite: true);
    }
}
