namespace Mariliz.Core.Policy;

public static class ManagedScope
{
    // A política de escrita é do Core; o mecanismo por trás dela só é conhecido em Application/Specification.
    public const string SpecificationDirectory = ".claude/skills/tlc-spec-driven";
    public const string SkillsLockDirectory = ".agents";

    // Ao desfazer, só pastas vazias dentro delas podem ser apagadas.
    public static readonly IReadOnlyList<string> TopLevelDirectories =
        [".claude", .. HarnessNaming.All.Select(n => n.StateDirectory), SkillsLockDirectory, ".cursor", ".codex"];

    // Os arquivos de IDE dos adaptadores (C1): neles o Spock escreve só o trecho dele (ManagedRegion) ou arquivos próprios.
    // A pasta .agents é do instalador da especificação, menos a skill que o Spock grava para o Codex e o Cursor (C3).
    private static readonly string[] SpockAgentsSkills = [.. HarnessNaming.All.Select(n => n.AgentsHarnessContractDirectory + "/")];

    private static readonly string[] AllowedPrefixes =
        [".claude/", .. HarnessNaming.All.Select(n => n.StateDirectory + "/"), ".cursor/rules/", .. SpockAgentsSkills];
    private static readonly string[] AllowedFiles =
    [
        .. HarnessNaming.All.SelectMany(n => new[] { n.Declaration, n.Lock }),
        "CLAUDE.md", "AGENTS.md", ".mcp.json", ".cursor/mcp.json", ".cursor/hooks.json", ".codex/config.toml", ".codex/hooks.json"
    ];

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

    // Infraestrutura como código aprovada (bootstrap do state e geração no modelo): arquivos .tf e .tofu, o exemplo de
    // valores (.tfvars.example, nunca o .tfvars) e o README de uma pasta, nunca dentro de .git/ nem do .terraform/ que a
    // ferramenta mantém, e o .gitignore da raiz para manter o state fora do git. O README da raiz do projeto fica de fora.
    public static bool IsInfrastructureWritable(string normalizedPath)
    {
        if (normalizedPath.Equals(GitIgnore, StringComparison.Ordinal)) return true;
        var segments = normalizedPath.Split('/');
        var allowed = normalizedPath.EndsWith(".tf", StringComparison.Ordinal) || normalizedPath.EndsWith(".tofu", StringComparison.Ordinal)
                      || normalizedPath.EndsWith(".tfvars.example", StringComparison.Ordinal)
                      || (segments.Length > 1 && segments[^1].Equals("README.md", StringComparison.Ordinal));
        if (!allowed) return false;
        return !segments.Any(segment => segment.Equals(".git", StringComparison.OrdinalIgnoreCase) || segment.Equals(".terraform", StringComparison.OrdinalIgnoreCase))
               && !IsSpecificationOwned(normalizedPath);
    }

    // Só o instalador oficial escreve estes arquivos.
    public static bool IsSpecificationOwned(string normalizedPath) =>
        normalizedPath.Equals(SpecificationDirectory, StringComparison.Ordinal)
        || normalizedPath.StartsWith(SpecificationDirectory + "/", StringComparison.Ordinal)
        || normalizedPath.Equals(SkillsLockDirectory, StringComparison.Ordinal)
        || (normalizedPath.StartsWith(SkillsLockDirectory + "/", StringComparison.Ordinal)
            && !SpockAgentsSkills.Any(p => normalizedPath.StartsWith(p, StringComparison.Ordinal)));
}
