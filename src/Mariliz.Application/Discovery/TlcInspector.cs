using System.Text.Json;
using Mariliz.Application.Ports;
using Mariliz.Core.Discovery;
using Mariliz.Core.Text;
using YamlDotNet.Serialization;

namespace Mariliz.Application.Discovery;

/// <summary>
/// Fatos do TLC (Tech Leads Club, skill <c>tlc-spec-driven</c>) comprovados contra a instalação real
/// do instalador oficial (<c>@tech-leads-club/agent-skills</c>): layout de arquivos, lockfile v2 e hash.
/// </summary>
public static class TlcManifest
{
    public const string SkillId = "tlc-spec-driven";
    public const string SkillDirectory = ".claude/skills/tlc-spec-driven";
    public const string SkillFile = SkillDirectory + "/SKILL.md";
    public const string MetaFile = SkillDirectory + "/.skill-meta.json";
    public const string LockFile = ".agents/.skill-lock.json";
    public const string InstallerPackage = "@tech-leads-club/agent-skills";

    /// <summary>
    /// Versões do TLC contra as quais esta fase foi validada: 3.3.0 (instalações de projetos de referência) e 4.0.0
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

public sealed record TlcFrontmatter(string? Name, string? License, string? Version);

public static class TlcInspector
{
    public static TlcStatus Inspect(IProjectFiles files)
    {
        var problems = new List<string>();
        var lockedHash = ReadLockedHash(files, problems);
        var hasDirectory = files.DirectoryExists(TlcManifest.SkillDirectory);

        if (!hasDirectory)
        {
            return new TlcStatus(TlcState.NotInstalled, null, lockedHash, null, false,
                lockedHash is null ? [] : ["O lockfile registra o TLC, mas a pasta da skill não existe."]);
        }

        var skillFiles = ReadSkillFiles(files);
        var frontmatter = skillFiles.TryGetValue("SKILL.md", out var skillBytes)
            ? ParseFrontmatter(System.Text.Encoding.UTF8.GetString(skillBytes))
            : null;

        var missing = TlcManifest.RequiredFiles.Where(f => !skillFiles.ContainsKey(f)).ToList();
        if (missing.Count > 0) problems.Add($"Arquivos ausentes na instalação do TLC: {string.Join(", ", missing)}.");

        if (frontmatter is null || frontmatter.Name != TlcManifest.SkillId)
            problems.Add("O SKILL.md não tem o frontmatter esperado do tlc-spec-driven.");

        var computed = Hashing.SkillContentHash(skillFiles);
        var version = frontmatter?.Version;
        var tested = version is not null && TlcManifest.TestedVersions.Contains(version);

        var structurallyBroken = missing.Count > 0 || frontmatter is null || frontmatter.Name != TlcManifest.SkillId;
        if (structurallyBroken)
            return new TlcStatus(TlcState.Invalid, version, lockedHash, computed, tested, problems);

        if (lockedHash is null)
        {
            problems.Add("Não há registro do TLC no lockfile do instalador; a integridade não pode ser verificada.");
            return new TlcStatus(TlcState.Drifted, version, null, computed, tested, problems);
        }

        if (!string.Equals(lockedHash, computed, StringComparison.OrdinalIgnoreCase))
        {
            problems.Add("O conteúdo da skill difere do hash registrado pelo instalador: houve edição local ou atualização parcial.");
            return new TlcStatus(TlcState.Drifted, version, lockedHash, computed, tested, problems);
        }

        var metaHash = ReadMetaHash(files);
        if (metaHash is not null && !string.Equals(metaHash, lockedHash, StringComparison.OrdinalIgnoreCase))
        {
            problems.Add("O .skill-meta.json e o lockfile registram hashes diferentes.");
            return new TlcStatus(TlcState.Drifted, version, lockedHash, computed, tested, problems);
        }

        return new TlcStatus(TlcState.Installed, version, lockedHash, computed, tested, problems);
    }

    private static Dictionary<string, byte[]> ReadSkillFiles(IProjectFiles files)
    {
        var result = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        var prefix = TlcManifest.SkillDirectory + "/";
        foreach (var path in files.EnumerateFiles(TlcManifest.SkillDirectory))
        {
            var relative = path[prefix.Length..];
            if (relative == ".skill-meta.json") continue;
            var bytes = files.ReadBytes(path);
            if (bytes is not null) result[relative] = bytes;
        }
        return result;
    }

    private static string? ReadLockedHash(IProjectFiles files, List<string> problems)
    {
        var text = files.ReadText(TlcManifest.LockFile);
        if (text is null) return null;
        try
        {
            using var doc = JsonDocument.Parse(text);
            if (doc.RootElement.TryGetProperty("skills", out var skills)
                && skills.TryGetProperty(TlcManifest.SkillId, out var entry)
                && entry.TryGetProperty("contentHash", out var hash)
                && hash.ValueKind == JsonValueKind.String)
                return hash.GetString();
        }
        catch (JsonException)
        {
            problems.Add("O lockfile do instalador (.agents/.skill-lock.json) não é um JSON válido.");
        }
        return null;
    }

    private static string? ReadMetaHash(IProjectFiles files)
    {
        var text = files.ReadText(TlcManifest.MetaFile);
        if (text is null) return null;
        try
        {
            using var doc = JsonDocument.Parse(text);
            return doc.RootElement.TryGetProperty("contentHash", out var h) && h.ValueKind == JsonValueKind.String ? h.GetString() : null;
        }
        catch (JsonException) { return null; }
    }

    public static TlcFrontmatter? ParseFrontmatter(string markdown)
    {
        var text = markdown.Replace("\r\n", "\n");
        if (!text.StartsWith("---\n", StringComparison.Ordinal)) return null;
        var end = text.IndexOf("\n---", 4, StringComparison.Ordinal);
        if (end < 0) return null;

        try
        {
            var map = new DeserializerBuilder().Build().Deserialize<Dictionary<string, object?>>(text[4..end]);
            if (map is null) return null;
            string? Str(string key) => map.TryGetValue(key, out var v) ? v?.ToString() : null;

            string? version = null;
            if (map.TryGetValue("metadata", out var meta) && meta is Dictionary<object, object> m && m.TryGetValue("version", out var ver))
                version = ver?.ToString();

            return new TlcFrontmatter(Str("name"), Str("license"), version);
        }
        catch (YamlDotNet.Core.YamlException)
        {
            return null;
        }
    }
}
