namespace Mariliz.Application.Workspace;

public interface IWorkspaceFileSystem
{
    string? ResolveDirectory(string path);

    bool CanEnumerate(string directory);

    // Usada só para abreviar caminhos exibidos.
    string HomeDirectory { get; }
}
