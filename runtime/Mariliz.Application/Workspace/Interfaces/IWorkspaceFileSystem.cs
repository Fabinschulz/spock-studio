namespace Mariliz.Application.Workspace;

public interface IWorkspaceFileSystem
{
    string? ResolveDirectory(string path);

    bool CanEnumerate(string directory);

    // Arquivos comuns (sem links) direto na pasta, só os nomes: bastam para achar o manifesto que dá o nome do projeto.
    IReadOnlyList<string> RootFiles(string directory);

    string? ReadRootFile(string directory, string fileName);

    // Usada só para abreviar caminhos exibidos.
    string HomeDirectory { get; }
}
