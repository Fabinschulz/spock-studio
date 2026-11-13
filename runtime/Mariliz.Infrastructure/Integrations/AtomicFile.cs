using System.Text;
using System.Text.Json;

namespace Mariliz.Infrastructure.Integrations;

internal static class AtomicFile
{
    /// <summary>
    /// Lê um documento JSON. Sem arquivo, devolve nulo. Um arquivo que não se consegue abrir (travado por outro processo) é uma
    /// falha passageira: a <see cref="IOException"/> sobe, porque tratá-lo como vazio faria a próxima gravação apagar o que está lá.
    /// Um conteúdo que não é JSON, ou não tem a forma esperada, é posto de lado como <c>.corrupt-*</c> (nada se perde) e conta como vazio.
    /// </summary>
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

    /// <summary>Tira o arquivo do caminho sem apagá-lo: quem precisar do conteúdo antigo o encontra ao lado.</summary>
    public static void Quarantine(string path) =>
        File.Move(path, $"{path}.corrupt-{DateTime.UtcNow:yyyyMMddHHmmssfff}", overwrite: true);

    /// <summary>Escreve num temporário e troca: quem lê nunca encontra o arquivo pela metade.</summary>
    public static void Write(string path, string content) => Write(path, new UTF8Encoding(false).GetBytes(content));

    public static void Write(string path, byte[] content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + ".tmp";
        File.WriteAllBytes(temp, content);
        File.Move(temp, path, overwrite: true);
    }
}
