using System.Text.Json;
using Mariliz.Application.Models;

namespace Mariliz.Infrastructure;

// A chave nunca passa por aqui. Arquivo ausente ou ilegível quer dizer "nunca conferida".
public sealed class FileModelCheckStore(string dataRoot) : IModelCheckStore
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
    private readonly object _gate = new();
    private string FilePath => Path.Combine(dataRoot, "model-check.json");

    public ModelCheck? Read()
    {
        lock (_gate)
        {
            if (!File.Exists(FilePath)) return null;
            try
            {
                var check = JsonSerializer.Deserialize<ModelCheck>(File.ReadAllText(FilePath), Options);
                return check is { Status: "connected" or "error" } ? check : null;
            }
            catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
            {
                return null;
            }
        }
    }

    public void Write(ModelCheck check)
    {
        lock (_gate)
        {
            Directory.CreateDirectory(dataRoot);
            var temp = FilePath + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(check, Options));
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
}
