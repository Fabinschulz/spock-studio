using Mariliz.Application.Ports;
using Mariliz.Core.Policy;

namespace Mariliz.Infrastructure;

public sealed class LocalProjectFilesFactory : IProjectFilesFactory
{
    public IProjectFiles Open(string authorizedRoot) => new LocalProjectFiles(authorizedRoot);
}

/// <summary>
/// Arquivos de um projeto autorizado. Nenhum caminho sai da raiz: são normalizados, comparados com a raiz
/// canônica e recusados se passarem por um link simbólico. Escrita só no escopo pedido.
/// </summary>
public sealed class LocalProjectFiles : IProjectFiles
{
    private const long MaxReadBytes = 8 * 1024 * 1024;
    private const int MaxEnumerated = 20_000;
    private static readonly HashSet<string> IgnoredDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        "node_modules", "bin", "obj", ".git", "dist", "build", ".dart_tool", "target", ".venv", "venv", "__pycache__", ".next", ".gradle", "Pods"
    };

    private readonly string _root;
    private static readonly StringComparison PathComparison =
        OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    public LocalProjectFiles(string root)
    {
        _root = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        RootName = Path.GetFileName(_root);
    }

    public string RootName { get; }

    public bool FileExists(string path) => TryResolve(path, out var full) && File.Exists(full) && !IsReparse(full);

    public bool DirectoryExists(string path) =>
        string.IsNullOrEmpty(path) ? Directory.Exists(_root) : TryResolve(path, out var full) && Directory.Exists(full) && !IsReparse(full);

    public string? NearestAncestorFolderWith(string fileName)
    {
        try
        {
            for (var dir = Directory.GetParent(_root); dir is not null; dir = dir.Parent)
                if (File.Exists(Path.Combine(dir.FullName, fileName))) return dir.Name.Length > 0 ? dir.Name : dir.FullName;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { /* sem acesso: não há o que afirmar */ }
        return null;
    }

    public byte[]? ReadBytes(string path)
    {
        if (!TryResolve(path, out var full) || !File.Exists(full) || IsReparse(full)) return null;
        try
        {
            return new FileInfo(full).Length > MaxReadBytes ? null : File.ReadAllBytes(full);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return null; }
    }

    public string? ReadText(string path)
    {
        var bytes = ReadBytes(path);
        return bytes is null ? null : System.Text.Encoding.UTF8.GetString(bytes).TrimStart('﻿');
    }

    public IReadOnlyList<string> EnumerateFiles(string directory, int maxDepth = 8)
    {
        var start = string.IsNullOrEmpty(directory) ? _root : TryResolve(directory, out var full) ? full : null;
        var result = new List<string>();
        if (start is null || !Directory.Exists(start) || IsReparse(start)) return result;
        Walk(start, 0, maxDepth, result);
        result.Sort(StringComparer.Ordinal);
        return result;
    }

    private void Walk(string dir, int depth, int maxDepth, List<string> result)
    {
        if (result.Count >= MaxEnumerated) return;
        IEnumerable<string> entries;
        try { entries = Directory.EnumerateFileSystemEntries(dir).ToList(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return; }

        foreach (var entry in entries)
        {
            if (result.Count >= MaxEnumerated) return;
            var name = Path.GetFileName(entry);
            if (IsReparse(entry)) continue;

            if (Directory.Exists(entry))
            {
                if (depth >= maxDepth || IgnoredDirectories.Contains(name)) continue;
                // Cópias de repositórios criadas por ferramentas de IA não fazem parte do projeto.
                if (name.Equals("worktrees", StringComparison.OrdinalIgnoreCase) && Path.GetFileName(dir).Equals(".claude", StringComparison.OrdinalIgnoreCase)) continue;
                Walk(entry, depth + 1, maxDepth, result);
            }
            else
            {
                result.Add(Path.GetRelativePath(_root, entry).Replace(Path.DirectorySeparatorChar, '/'));
            }
        }
    }

    public void WriteAtomic(string path, byte[] content, WriteScope scope = WriteScope.Managed)
    {
        var (normalized, full) = ResolveForWrite(path, scope);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        EnsureNoReparseOnPath(full);

        var temp = full + ".mariliz-tmp";
        try
        {
            File.WriteAllBytes(temp, content);
            File.Move(temp, full, overwrite: true);
        }
        finally
        {
            if (File.Exists(temp)) File.Delete(temp);
        }
        _ = normalized;
    }

    public void Delete(string path, WriteScope scope = WriteScope.Managed)
    {
        var (_, full) = ResolveForWrite(path, scope);
        EnsureNoReparseOnPath(full);
        if (File.Exists(full)) File.Delete(full);
    }

    public void PruneEmptyDirectories(string directory)
    {
        if (string.IsNullOrEmpty(directory)) return;
        var current = directory;
        while (!string.IsNullOrEmpty(current))
        {
            if (!PathGuard.TryNormalize(current, out var normalized, out _)) return;
            var top = normalized.Split('/')[0];
            if (top is not (".claude" or ".mariliz" or ".agents")) return;
            if (!TryResolve(normalized, out var full) || !Directory.Exists(full) || IsReparse(full)) return;
            if (Directory.EnumerateFileSystemEntries(full).Any()) return;
            Directory.Delete(full);
            current = normalized.Contains('/') ? normalized[..normalized.LastIndexOf('/')] : "";
        }
    }

    // ------------------------------------------------------------------ Guarda de caminho

    private bool TryResolve(string path, out string full)
    {
        full = "";
        if (!PathGuard.TryNormalize(path, out var normalized, out _)) return false;
        var candidate = Path.GetFullPath(Path.Combine(_root, normalized.Replace('/', Path.DirectorySeparatorChar)));
        if (!candidate.StartsWith(_root + Path.DirectorySeparatorChar, PathComparison) && !candidate.Equals(_root, PathComparison)) return false;
        full = candidate;
        return true;
    }

    private (string Normalized, string Full) ResolveForWrite(string path, WriteScope scope)
    {
        if (!PathGuard.TryNormalize(path, out var normalized, out var error))
            throw new UnauthorizedAccessException(error ?? "Caminho inválido.");

        var allowed = scope == WriteScope.Managed ? ManagedScope.IsWritable(normalized) : ManagedScope.IsTlcOwned(normalized);
        if (!allowed) throw new UnauthorizedAccessException($"O caminho '{normalized}' está fora do escopo de escrita.");
        if (!TryResolve(normalized, out var full)) throw new UnauthorizedAccessException("O caminho sai da pasta do projeto.");
        return (normalized, full);
    }

    private void EnsureNoReparseOnPath(string full)
    {
        var current = full;
        while (current.Length > _root.Length)
        {
            if ((File.Exists(current) || Directory.Exists(current)) && IsReparse(current))
                throw new UnauthorizedAccessException("O caminho passa por um link simbólico e não será alterado.");
            current = Path.GetDirectoryName(current) ?? _root;
        }
    }

    private static bool IsReparse(string path)
    {
        try { return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return true; }
    }
}
