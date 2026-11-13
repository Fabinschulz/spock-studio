namespace Mariliz.Application.Ports;

// Todo caminho é relativo à raiz, com '/'.
public interface IProjectFiles
{
    string RootName { get; }
    bool FileExists(string path);
    bool DirectoryExists(string path);

    // Serve para saber onde ferramentas que procuram a "raiz do projeto" subindo pelas pastas-pai (como o instalador da estrutura de especificação com o package.json) vão gravar.
    string? NearestAncestorFolderWith(string fileName);

    byte[]? ReadBytes(string path);

    string? ReadText(string path);

    // Sem seguir links e sem pastas ignoradas.
    IReadOnlyList<string> EnumerateFiles(string directory, int maxDepth = 8);

    // Escreve por arquivo temporário e renomeação.
    void WriteAtomic(string path, byte[] content, WriteScope scope = WriteScope.Managed);

    void Delete(string path, WriteScope scope = WriteScope.Managed);

    // Sem passar da raiz.
    void PruneEmptyDirectories(string directory);
}
