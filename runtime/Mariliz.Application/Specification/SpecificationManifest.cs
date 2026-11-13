using Mariliz.Core.Policy;

namespace Mariliz.Application.Specification;

// O único lugar que conhece o mecanismo por trás da estrutura de especificação (a skill TLC tlc-spec-driven, pelo instalador oficial @tech-leads-club/agent-skills). Os fatos aqui foram comprovados contra a instalação real: layout de arquivos, lockfile v2 e hash.
public static class SpecificationManifest
{
    public const string SkillId = "tlc-spec-driven";
    public const string SkillDirectory = ManagedScope.SpecificationDirectory;
    public const string SkillFile = SkillDirectory + "/SKILL.md";
    public const string MetaFile = SkillDirectory + "/.skill-meta.json";
    public const string LockFile = ".agents/.skill-lock.json";
    public const string InstallerPackage = "@tech-leads-club/agent-skills";

    // Validadas: 3.3.0 (instalações de projetos de referência) e 4.0.0 (instalador oficial em projeto de teste, com estrutura, hash e gates conferidos). Outras são tratadas como não validadas.
    public static readonly IReadOnlyList<string> TestedVersions = ["3.3.0", "4.0.0"];

    // O plano é local e não consulta a rede, então só decide "desatualizado" com base nesta versão. Para buscar uma versão ainda desconhecida aqui, a pessoa pede a atualização explicitamente.
    public const string LatestKnownVersion = "4.0.0";

    // Sem versão legível, não afirma nada.
    public static bool IsOlderThanLatest(string? version) =>
        Version.TryParse(version, out var installed) && Version.TryParse(LatestKnownVersion, out var latest) && installed < latest;

    // Sem o .skill-meta.json do instalador. A 4.0.0 removeu code-analysis.md, context-limits.md e validate_state.py, então eles não são exigidos.
    public static readonly IReadOnlyList<string> RequiredFiles =
    [
        "SKILL.md",
        "references/coding-principles.md", "references/design.md", "references/discuss.md", "references/implement.md",
        "references/lessons.md", "references/memory.md", "references/specify.md", "references/sub-agents.md",
        "references/tasks.md", "references/validate.md",
        "scripts/check_commit.py", "scripts/lessons.py", "scripts/validate_spec.py", "scripts/validate_tasks.py"
    ];

    // README do agent-skills.
    public static readonly IReadOnlyList<string> InstallArguments =
        ["-y", InstallerPackage, "install", "-s", SkillId, "-a", "claude-code"];

    public static readonly IReadOnlyList<string> ForceInstallArguments =
        ["-y", InstallerPackage, "install", "-s", SkillId, "-a", "claude-code", "--force"];
}
