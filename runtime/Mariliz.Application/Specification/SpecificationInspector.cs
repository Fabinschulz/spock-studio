using System.Text.Json;
using Mariliz.Application.Ports;
using Mariliz.Core.Discovery;
using Mariliz.Core.Text;
using YamlDotNet.Serialization;

namespace Mariliz.Application.Specification;

/// <summary>Confere se a estrutura de especificação está instalada e íntegra: arquivos, versão e hash registrado pelo instalador.</summary>
public static class SpecificationInspector
{
    public static SpecificationStatus Inspect(IProjectFiles files)
    {
        var problems = new List<string>();
        var lockedHash = ReadLockedHash(files, problems);
        var hasDirectory = files.DirectoryExists(SpecificationManifest.SkillDirectory);

        if (!hasDirectory)
        {
            return new SpecificationStatus(SpecificationState.NotInstalled, null, lockedHash, null, false,
                lockedHash is null ? [] : ["O lockfile do instalador registra a estrutura de especificação, mas a pasta dela não existe."]);
        }

        var skillFiles = ReadSkillFiles(files);
        var frontmatter = skillFiles.TryGetValue("SKILL.md", out var skillBytes)
            ? ParseFrontmatter(System.Text.Encoding.UTF8.GetString(skillBytes))
            : null;

        var missing = SpecificationManifest.RequiredFiles.Where(f => !skillFiles.ContainsKey(f)).ToList();
        if (missing.Count > 0) problems.Add($"Arquivos ausentes na estrutura de especificação: {string.Join(", ", missing)}.");

        if (frontmatter is null || frontmatter.Name != SpecificationManifest.SkillId)
            problems.Add("O SKILL.md não tem o frontmatter esperado da estrutura de especificação.");

        var computed = Hashing.SkillContentHash(skillFiles);
        var version = frontmatter?.Version;
        var tested = version is not null && SpecificationManifest.TestedVersions.Contains(version);

        var structurallyBroken = missing.Count > 0 || frontmatter is null || frontmatter.Name != SpecificationManifest.SkillId;
        if (structurallyBroken)
            return new SpecificationStatus(SpecificationState.Invalid, version, lockedHash, computed, tested, problems);

        if (lockedHash is null)
        {
            problems.Add("Não há registro da estrutura de especificação no lockfile do instalador; a integridade não pode ser verificada.");
            return new SpecificationStatus(SpecificationState.Drifted, version, null, computed, tested, problems);
        }

        if (!string.Equals(lockedHash, computed, StringComparison.OrdinalIgnoreCase))
        {
            problems.Add("O conteúdo da skill difere do hash registrado pelo instalador: houve edição local ou atualização parcial.");
            return new SpecificationStatus(SpecificationState.Drifted, version, lockedHash, computed, tested, problems);
        }

        var metaHash = ReadMetaHash(files);
        if (metaHash is not null && !string.Equals(metaHash, lockedHash, StringComparison.OrdinalIgnoreCase))
        {
            problems.Add("O .skill-meta.json e o lockfile registram hashes diferentes.");
            return new SpecificationStatus(SpecificationState.Drifted, version, lockedHash, computed, tested, problems);
        }

        return new SpecificationStatus(SpecificationState.Installed, version, lockedHash, computed, tested, problems);
    }

    private static Dictionary<string, byte[]> ReadSkillFiles(IProjectFiles files)
    {
        var result = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        var prefix = SpecificationManifest.SkillDirectory + "/";
        foreach (var path in files.EnumerateFiles(SpecificationManifest.SkillDirectory))
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
        var text = files.ReadText(SpecificationManifest.LockFile);
        if (text is null) return null;
        try
        {
            using var doc = JsonDocument.Parse(text);
            if (doc.RootElement.TryGetProperty("skills", out var skills)
                && skills.TryGetProperty(SpecificationManifest.SkillId, out var entry)
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
        var text = files.ReadText(SpecificationManifest.MetaFile);
        if (text is null) return null;
        try
        {
            using var doc = JsonDocument.Parse(text);
            return doc.RootElement.TryGetProperty("contentHash", out var h) && h.ValueKind == JsonValueKind.String ? h.GetString() : null;
        }
        catch (JsonException) { return null; }
    }

    public static SpecificationFrontmatter? ParseFrontmatter(string markdown)
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

            return new SpecificationFrontmatter(Str("name"), Str("license"), version);
        }
        catch (YamlDotNet.Core.YamlException)
        {
            return null;
        }
    }
}
