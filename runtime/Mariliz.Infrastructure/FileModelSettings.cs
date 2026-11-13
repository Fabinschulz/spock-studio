using System.Text.Json;
using System.Text.Json.Nodes;
using Mariliz.Application.Execution;
using Mariliz.Application.Models;

namespace Mariliz.Infrastructure;

/// <summary>
/// Uma chave de provedor em <c>{dataRoot}/model.json</c>, em texto puro. Fora do projeto, do índice e do journal. É o
/// armazenamento de onde não há cofre e de onde a chave é migrada; onde há cofre, quem manda é <see cref="LocalModelSettings"/>.
/// </summary>
public sealed class FileModelSettings(string dataRoot) : IModelSettings
{
    private readonly object _gate = new();
    private string FilePath => Path.Combine(dataRoot, "model.json");

    public ModelStatus Status()
    {
        lock (_gate) return Read() is { } stored ? new ModelStatus(true, stored.Provider) : new ModelStatus(false, "");
    }

    public void Validate(string provider, string key) => ModelKeyRules.Validate(provider, key);

    public string? Protection() => TryGetKey(out _) ? ModelKeyProtection.File : null;

    public void Save(string provider, string key)
    {
        ModelKeyRules.Validate(provider, key);

        lock (_gate)
        {
            Directory.CreateDirectory(dataRoot);
            var temp = FilePath + ".tmp";
            File.WriteAllText(temp, new JsonObject { ["provider"] = provider, ["key"] = key }.ToJsonString());
            File.Move(temp, FilePath, overwrite: true);
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

    public bool TryGetKey(out string key)
    {
        lock (_gate)
        {
            if (Read() is not { } stored)
            {
                key = "";
                return false;
            }

            key = stored.Key;
            return true;
        }
    }

    private (string Provider, string Key)? Read()
    {
        if (!File.Exists(FilePath)) return null;
        try
        {
            if (JsonNode.Parse(File.ReadAllText(FilePath)) is not JsonObject document) return null;
            if (document["provider"] is not JsonValue providerNode || !providerNode.TryGetValue<string>(out var provider)) return null;
            if (document["key"] is not JsonValue keyNode || !keyNode.TryGetValue<string>(out var key)) return null;
            if (!string.Equals(provider, ModelGateway.Provider, StringComparison.Ordinal) || !ModelKeyRules.IsValidKey(key)) return null;
            return (provider, key);
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            return null;
        }
    }
}
