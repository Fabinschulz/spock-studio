using Mariliz.Application.Workspace;

namespace Mariliz.Infrastructure;

public sealed class LocalWorkspaceFileSystem : IWorkspaceFileSystem
{
    public string HomeDirectory { get; } = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    public string? ResolveDirectory(string path)
    {
        try
        {
            var full = Path.GetFullPath(path);
            var info = new DirectoryInfo(full);
            if (!info.Exists) return null;
            // Segue links simbólicos até o destino real para que a pasta autorizada seja a verdadeira.
            // Só consulta o destino quando é um link: a raiz de uma unidade não aceita a consulta.
            if (info.LinkTarget is null) return info.FullName;
            return (info.ResolveLinkTarget(returnFinalTarget: true) as DirectoryInfo)?.FullName;
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return null;
        }
    }

    public bool CanEnumerate(string directory)
    {
        try
        {
            using var e = Directory.EnumerateFileSystemEntries(directory).GetEnumerator();
            e.MoveNext();
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private const long MaxManifestBytes = 256 * 1024;
    private const int MaxRootFiles = 500;

    public IReadOnlyList<string> RootFiles(string directory)
    {
        try
        {
            return new DirectoryInfo(directory).EnumerateFiles()
                .Where(file => !file.Attributes.HasFlag(FileAttributes.ReparsePoint))
                .Take(MaxRootFiles)
                .Select(file => file.Name)
                .ToList();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    public string? ReadRootFile(string directory, string fileName)
    {
        if (Path.GetFileName(fileName) != fileName) return null;
        try
        {
            var file = new FileInfo(Path.Combine(directory, fileName));
            if (!file.Exists || file.Attributes.HasFlag(FileAttributes.ReparsePoint) || file.Length > MaxManifestBytes) return null;
            return File.ReadAllText(file.FullName);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
