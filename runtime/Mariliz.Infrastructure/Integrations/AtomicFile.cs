using System.Text;
using System.Text.Json;

namespace Mariliz.Infrastructure.Integrations;

internal static class AtomicFile
{
    // Arquivo travado é falha passageira: a IOException sobe, porque tratá-lo como vazio faria a próxima gravação apagar o que está lá. Conteúdo inválido vai para .corrupt-* (nada se perde) e conta como vazio.
    public static T? ReadJson<T>(string path, JsonSerializerOptions options, Func<T, bool>? isValid = null) where T : class
    {
        if (!File.Exists(path)) return null;
        var text = File.ReadAllText(path);
        try
        {
            var document = JsonSerializer.Deserialize<T>(text, options);
            if (document is not null && (isValid?.Invoke(document) ?? true)) return document;
        }
        catch (JsonException)
        {
            // Cai na quarentena abaixo.
        }
        Quarantine(path);
        return null;
    }

    // Quem precisar do conteúdo antigo o encontra ao lado.
    public static void Quarantine(string path) =>
        File.Move(path, $"{path}.corrupt-{DateTime.UtcNow:yyyyMMddHHmmssfff}", overwrite: true);

    // Quem lê nunca encontra o arquivo pela metade.
    public static void Write(string path, string content) => Write(path, new UTF8Encoding(false).GetBytes(content));

    public static void Write(string path, byte[] content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + ".tmp";
        File.WriteAllBytes(temp, content);
        File.Move(temp, path, overwrite: true);
    }
}
