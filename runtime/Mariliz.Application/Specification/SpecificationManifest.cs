using Mariliz.Core.Policy;

namespace Mariliz.Application.Specification;

/// <summary>
/// O único lugar que conhece o mecanismo por trás da estrutura de especificação: a skill TLC (Tech Leads Club,
/// <c>tlc-spec-driven</c>), instalada pelo instalador oficial (<c>@tech-leads-club/agent-skills</c>). Os fatos aqui foram
/// comprovados contra a instalação real: layout de arquivos, lockfile v2 e hash. O resto da aplicação fala em
/// "estrutura de especificação" e não depende de nome, pacote ou versão.
/// </summary>
public static class SpecificationManifest
{
    public const string SkillId = "tlc-spec-driven";
    public const string SkillDirectory = ManagedScope.SpecificationDirectory;
    public const string SkillFile = SkillDirectory + "/SKILL.md";
    public const string MetaFile = SkillDirectory + "/.skill-meta.json";
    public const string LockFile = ".agents/.skill-lock.json";
    public const string InstallerPackage = "@tech-leads-club/agent-skills";

    /// <summary>
    /// Versões contra as quais esta fase foi validada: 3.3.0 (instalações de projetos de referência) e 4.0.0
    /// (instalada pelo instalador oficial em projeto de teste, com estrutura, hash e gates conferidos).
    /// Outras são tratadas como não validadas.
    /// </summary>
    public static readonly IReadOnlyList<string> TestedVersions = ["3.3.0", "4.0.0"];

    /// <summary>
    /// A versão mais recente que esta fase conhece e validou. O plano só decide "desatualizado" com base nela, porque o
    /// plano é local: não consulta a rede. Uma instalação nova sempre traz a versão atual do instalador oficial, que
    /// não fixa versão; para buscar uma versão ainda desconhecida aqui, a pessoa pede a atualização explicitamente.
    /// </summary>
    public const string LatestKnownVersion = "4.0.0";

    /// <summary>Verdadeiro só quando a versão é reconhecível e anterior à mais recente conhecida; sem versão legível, não afirma nada.</summary>
    public static bool IsOlderThanLatest(string? version) =>
        Version.TryParse(version, out var installed) && Version.TryParse(LatestKnownVersion, out var latest) && installed < latest;

    /// <summary>
    /// Arquivos que toda instalação íntegra contém, em qualquer versão testada (sem o <c>.skill-meta.json</c> do
    /// instalador). A 4.0.0 removeu code-analysis.md, context-limits.md e validate_state.py, então eles não são exigidos.
    /// </summary>
    public static readonly IReadOnlyList<string> RequiredFiles =
    [
        "SKILL.md",
        "references/coding-principles.md", "references/design.md", "references/discuss.md", "references/implement.md",
        "references/lessons.md", "references/memory.md", "references/specify.md", "references/sub-agents.md",
        "references/tasks.md", "references/validate.md",
        "scripts/check_commit.py", "scripts/lessons.py", "scripts/validate_spec.py", "scripts/validate_tasks.py"
    ];

    /// <summary>Comando oficial do instalador para o Claude Code (README do agent-skills).</summary>
    public static readonly IReadOnlyList<string> InstallArguments =
        ["-y", InstallerPackage, "install", "-s", SkillId, "-a", "claude-code"];

    public static readonly IReadOnlyList<string> ForceInstallArguments =
        ["-y", InstallerPackage, "install", "-s", SkillId, "-a", "claude-code", "--force"];
}
