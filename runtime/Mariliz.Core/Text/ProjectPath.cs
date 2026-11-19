namespace Mariliz.Core.Text;

public static class ProjectPath
{
    public static string Parent(string path)
    {
        var index = path.LastIndexOf('/');
        return index < 0 ? "" : path[..index];
    }

    public static int Depth(string path) => path.Count(c => c == '/');

    public static string Combine(string root, string path) => root.Length == 0 ? path : root + "/" + path;

    // "src/App" + "../Lib/Lib.csproj" vira "src/Lib/Lib.csproj". Um caminho que sobe acima da raiz do projeto volta nulo.
    public static string? Resolve(string folder, string relative)
    {
        var parts = new List<string>(folder.Length == 0 ? [] : folder.Split('/'));
        foreach (var part in relative.Split('/'))
        {
            if (part is "" or ".") continue;
            if (part != "..") parts.Add(part);
            else if (parts.Count == 0) return null;
            else parts.RemoveAt(parts.Count - 1);
        }
        return parts.Count == 0 ? null : string.Join('/', parts);
    }
}
