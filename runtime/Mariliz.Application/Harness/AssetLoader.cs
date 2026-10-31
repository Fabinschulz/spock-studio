using System.Reflection;

namespace Mariliz.Application.Harness;

/// <summary>Conteúdo embutido no motor (ativos originais ou adaptados de fonte autorizada, MIT).</summary>
public static class AssetLoader
{
    private static readonly Assembly Assembly = typeof(AssetLoader).Assembly;

    public static string Read(string name)
    {
        var normalized = name.Replace('\\', '/');
        var resource = Assembly.GetManifestResourceNames()
            .FirstOrDefault(r => r.Replace('\\', '/').EndsWith("Assets/" + normalized, StringComparison.Ordinal)
                                 || r.Replace('\\', '/').EndsWith("." + normalized.Replace('/', '.'), StringComparison.Ordinal))
            ?? throw new InvalidOperationException($"Ativo embutido não encontrado: {name}");

        using var stream = Assembly.GetManifestResourceStream(resource)!;
        using var reader = new StreamReader(stream, System.Text.Encoding.UTF8);
        return reader.ReadToEnd().Replace("\r\n", "\n");
    }
}
