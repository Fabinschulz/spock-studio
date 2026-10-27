namespace Mariliz.Core.Policy;

/// <summary>
/// Caminhos de projeto são sempre relativos à raiz autorizada, com '/'. Qualquer coisa que escape
/// da raiz ou toque áreas que a aplicação não gerencia é recusada antes de chegar ao disco.
/// </summary>
public static class PathGuard
{
    /// <summary>Normaliza para o formato relativo com '/'. Falha em caminho absoluto, com '..' ou inválido.</summary>
    public static bool TryNormalize(string? path, out string normalized, out string? error)
    {
        normalized = "";
        error = null;

        if (string.IsNullOrWhiteSpace(path)) { error = "Caminho vazio."; return false; }
        if (path.Contains('\0')) { error = "Caminho inválido."; return false; }

        var unified = path.Replace('\\', '/');
        if (unified.StartsWith('/') || unified.StartsWith("//")) { error = "Caminho absoluto não é permitido."; return false; }
        if (unified.Length >= 2 && char.IsAsciiLetter(unified[0]) && unified[1] == ':') { error = "Caminho com unidade não é permitido."; return false; }

        var parts = new List<string>();
        foreach (var segment in unified.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment == ".") continue;
            if (segment == "..") { error = "Caminho que sai da pasta do projeto não é permitido."; return false; }
            if (segment.EndsWith('.') || segment.EndsWith(' ')) { error = "Nome de arquivo inválido."; return false; }
            parts.Add(segment);
        }

        if (parts.Count == 0) { error = "Caminho vazio."; return false; }
        normalized = string.Join('/', parts);
        return true;
    }
}

/// <summary>Quais caminhos a aplicação pode escrever por conta própria.</summary>
public static class ManagedScope
{
    public const string TlcSkillDirectory = ".claude/skills/tlc-spec-driven";
    public const string SkillsLockDirectory = ".agents";

    private static readonly string[] AllowedPrefixes = [".claude/", ".mariliz/"];
    private static readonly string[] AllowedFiles = ["mariliz.yaml", "mariliz.lock"];

    /// <summary>Arquivos de configuração que a aplicação escreve diretamente.</summary>
    public static bool IsWritable(string normalizedPath)
    {
        if (IsTlcOwned(normalizedPath)) return false;
        if (normalizedPath.StartsWith(".git/", StringComparison.OrdinalIgnoreCase) || normalizedPath.Equals(".git", StringComparison.OrdinalIgnoreCase)) return false;
        if (AllowedFiles.Contains(normalizedPath, StringComparer.Ordinal)) return true;
        return AllowedPrefixes.Any(p => normalizedPath.StartsWith(p, StringComparison.Ordinal));
    }

    /// <summary>O TLC é dono dos próprios arquivos: só o instalador oficial os escreve.</summary>
    public static bool IsTlcOwned(string normalizedPath) =>
        normalizedPath.Equals(TlcSkillDirectory, StringComparison.Ordinal)
        || normalizedPath.StartsWith(TlcSkillDirectory + "/", StringComparison.Ordinal)
        || normalizedPath.Equals(SkillsLockDirectory, StringComparison.Ordinal)
        || normalizedPath.StartsWith(SkillsLockDirectory + "/", StringComparison.Ordinal);
}
