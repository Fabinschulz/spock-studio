using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Mariliz.Application.Execution;
using Mariliz.Application.Models;

namespace Mariliz.Infrastructure;

/// <summary>Uma chave de provedor em <c>{dataRoot}/model.json</c>. Fora do projeto, do índice e do journal.</summary>
public sealed partial class FileModelSettings(string dataRoot) : IModelSettings
{
    private readonly object _gate = new();
    private string FilePath => Path.Combine(dataRoot, "model.json");

    public ModelStatus Status()
    {
        lock (_gate) return Read() is { } stored ? new ModelStatus(true, stored.Provider) : new ModelStatus(false, "");
    }

    public void Save(string provider, string key)
    {
        if (!string.Equals(provider, ModelGateway.Provider, StringComparison.Ordinal))
            throw new EngineException("MODEL_PROVIDER_DENIED", "O provedor não está disponível.");
        if (string.IsNullOrWhiteSpace(key) || !KeyPattern().IsMatch(key))
            throw new EngineException("MODEL_KEY_REJECTED", "A chave não tem o formato esperado.");

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
            if (!string.Equals(provider, ModelGateway.Provider, StringComparison.Ordinal) || !KeyPattern().IsMatch(key)) return null;
            return (provider, key);
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            return null;
        }
    }

    [GeneratedRegex("^[A-Za-z0-9_-]{20,200}$")]
    private static partial Regex KeyPattern();
}
