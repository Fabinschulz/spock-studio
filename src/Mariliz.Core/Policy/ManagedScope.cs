namespace Mariliz.Core.Policy;

/// <summary>Quais caminhos a aplicação pode escrever por conta própria.</summary>
public static class ManagedScope
{
    /// <summary>Pasta da estrutura de especificação. A política de escrita é do Core; o mecanismo por trás dela só é conhecido em Application/Specification.</summary>
    public const string SpecificationDirectory = ".claude/skills/tlc-spec-driven";
    public const string SkillsLockDirectory = ".agents";

    private static readonly string[] AllowedPrefixes = [".claude/", ".mariliz/"];
    private static readonly string[] AllowedFiles = ["mariliz.yaml", "mariliz.lock"];

    /// <summary>Arquivos de configuração que a aplicação escreve diretamente.</summary>
    public static bool IsWritable(string normalizedPath)
    {
        if (IsSpecificationOwned(normalizedPath)) return false;
        if (normalizedPath.StartsWith(".git/", StringComparison.OrdinalIgnoreCase) || normalizedPath.Equals(".git", StringComparison.OrdinalIgnoreCase)) return false;
        if (AllowedFiles.Contains(normalizedPath, StringComparer.Ordinal)) return true;
        return AllowedPrefixes.Any(p => normalizedPath.StartsWith(p, StringComparison.Ordinal));
    }

    /// <summary>A estrutura de especificação é dona dos próprios arquivos: só o instalador oficial os escreve.</summary>
    public static bool IsSpecificationOwned(string normalizedPath) =>
        normalizedPath.Equals(SpecificationDirectory, StringComparison.Ordinal)
        || normalizedPath.StartsWith(SpecificationDirectory + "/", StringComparison.Ordinal)
        || normalizedPath.Equals(SkillsLockDirectory, StringComparison.Ordinal)
        || normalizedPath.StartsWith(SkillsLockDirectory + "/", StringComparison.Ordinal);
}
