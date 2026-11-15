using System.Text.RegularExpressions;
using Mariliz.Application.Iac;

namespace Mariliz.Infrastructure.Terraform;

// Lê required_version dos .tf do projeto como texto. Não roda o Terraform nem interpreta HCL além dessa atribuição.
public sealed partial class TerraformConfigReader : ITerraformConfigReader
{
    private const int MaxDepth = 6;
    private const int MaxFiles = 2000;
    private const long MaxFileBytes = 512 * 1024;

    private static readonly HashSet<string> Skipped = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git", ".terraform", "node_modules", "bin", "obj", "dist", "build", "vendor", ".venv", "target"
    };

    public IReadOnlyList<TerraformRequirement> Requirements(string projectRoot)
    {
        var found = new List<TerraformRequirement>();
        foreach (var file in TerraformFiles(projectRoot).Take(MaxFiles))
        {
            string text;
            try
            {
                if (new FileInfo(file).Length > MaxFileBytes) continue;
                text = File.ReadAllText(file);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                continue;
            }
            var relative = Path.GetRelativePath(projectRoot, file).Replace('\\', '/');
            foreach (Match match in RequiredVersion().Matches(text))
            {
                var constraint = match.Groups[1].Value.Trim();
                found.Add(new TerraformRequirement(relative, constraint, VersionConstraint.TryParse(constraint) is not null));
            }
        }
        return found.OrderBy(r => r.File, StringComparer.Ordinal).ToList();
    }

    private static IEnumerable<string> TerraformFiles(string root)
    {
        var pending = new Stack<(string Path, int Depth)>();
        pending.Push((root, 0));
        while (pending.Count > 0)
        {
            var (directory, depth) = pending.Pop();
            IEnumerable<string> files, children;
            try
            {
                files = Directory.EnumerateFiles(directory, "*.tf").ToList();
                children = depth < MaxDepth ? Directory.EnumerateDirectories(directory).ToList() : [];
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                continue;
            }
            foreach (var file in files) yield return file;
            foreach (var child in children)
            {
                var name = Path.GetFileName(child);
                if (Skipped.Contains(name) || new DirectoryInfo(child).LinkTarget is not null) continue;
                pending.Push((child, depth + 1));
            }
        }
    }

    // Só a linha da atribuição, fora de comentário de linha.
    [GeneratedRegex(@"^[ \t]*required_version[ \t]*=[ \t]*""([^""\r\n]*)""", RegexOptions.Multiline)]
    private static partial Regex RequiredVersion();
}
