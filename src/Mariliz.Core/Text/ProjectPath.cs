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
}
