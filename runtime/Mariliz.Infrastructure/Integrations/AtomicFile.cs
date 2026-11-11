using System.Text;

namespace Mariliz.Infrastructure.Integrations;

internal static class AtomicFile
{
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
