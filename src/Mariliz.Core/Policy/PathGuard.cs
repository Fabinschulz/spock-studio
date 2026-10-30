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
