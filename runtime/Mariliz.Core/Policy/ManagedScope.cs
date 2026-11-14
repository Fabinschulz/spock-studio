namespace Mariliz.Core.Policy;

public static class ManagedScope
{
    // A política de escrita é do Core; o mecanismo por trás dela só é conhecido em Application/Specification.
    public const string SpecificationDirectory = ".claude/skills/tlc-spec-driven";
    public const string SkillsLockDirectory = ".agents";

    // Ao desfazer, só pastas vazias dentro delas podem ser apagadas.
    public static readonly IReadOnlyList<string> TopLevelDirectories = [".claude", ".mariliz", SkillsLockDirectory];

    private static readonly string[] AllowedPrefixes = [".claude/", ".mariliz/"];
    private static readonly string[] AllowedFiles = ["mariliz.yaml", "mariliz.lock"];

    // O único lugar onde um arquivo gerenciado pode estar.
    public static bool IsEnvironmentFile(string normalizedPath) =>
        AllowedFiles.Contains(normalizedPath, StringComparer.Ordinal)
        || AllowedPrefixes.Any(p => normalizedPath.StartsWith(p, StringComparison.Ordinal));

    public static bool IsWritable(string normalizedPath)
    {
        if (IsSpecificationOwned(normalizedPath)) return false;
        if (normalizedPath.StartsWith(".git/", StringComparison.OrdinalIgnoreCase) || normalizedPath.Equals(".git", StringComparison.OrdinalIgnoreCase)) return false;
        return IsEnvironmentFile(normalizedPath);
    }

    public const string GitIgnore = ".gitignore";

    // Uma correção de segurança aprovada pode tocar o .gitignore da raiz; fora do ambiente gerenciado, só ele.
    public static bool IsSecurityFixWritable(string normalizedPath) =>
        normalizedPath.Equals(GitIgnore, StringComparison.Ordinal) || IsWritable(normalizedPath);

    // Só o instalador oficial escreve estes arquivos.
    public static bool IsSpecificationOwned(string normalizedPath) =>
        normalizedPath.Equals(SpecificationDirectory, StringComparison.Ordinal)
        || normalizedPath.StartsWith(SpecificationDirectory + "/", StringComparison.Ordinal)
        || normalizedPath.Equals(SkillsLockDirectory, StringComparison.Ordinal)
        || normalizedPath.StartsWith(SkillsLockDirectory + "/", StringComparison.Ordinal);
}
