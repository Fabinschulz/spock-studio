namespace Mariliz.Application.Iac;

// Caminhos da configuração de IaC: pastas do projeto são relativas e separadas por '/'; a cópia de uma operação é um
// caminho do disco.
public static class IacLayout
{
    // A pasta comum a todas as que têm configuração: no modelo, "infra" (raiz, bootstrap/ e modules/*).
    public static string CommonRoot(IReadOnlyList<string> folders)
    {
        var parts = folders.Select(f => f.Length == 0 ? [] : f.Split('/')).ToList();
        var common = new List<string>();
        for (var i = 0; parts.All(p => p.Length > i) && parts.Select(p => p[i]).Distinct(StringComparer.Ordinal).Count() == 1; i++)
            common.Add(parts[0][i]);
        return string.Join('/', common);
    }

    public static string Relative(string root, string folder) =>
        root.Length == 0 ? folder : folder == root ? "" : folder[(root.Length + 1)..];

    public static string Combine(string a, string b) =>
        b.Length == 0 ? a : a.Length == 0 ? b : a.Contains('\\') || Path.IsPathRooted(a) ? Path.Combine(a, b.Replace('/', Path.DirectorySeparatorChar)) : $"{a}/{b}";
}
