namespace Mariliz.Application.Ports;

/// <summary>Acesso a arquivos de UM projeto autorizado. Todo caminho é relativo à raiz, com '/'.</summary>
public interface IProjectFiles
{
    string RootName { get; }
    bool FileExists(string path);
    bool DirectoryExists(string path);

    /// <summary>
    /// Nome da pasta mais próxima, <b>acima</b> da raiz do projeto, que contém <paramref name="fileName"/>; <c>null</c> se não há.
    /// Somente leitura e só devolve o nome da pasta. Serve para saber onde ferramentas que procuram a "raiz do projeto"
    /// subindo pelas pastas-pai (como o instalador do TLC com o package.json) vão gravar.
    /// </summary>
    string? NearestAncestorFolderWith(string fileName);

    /// <summary><c>null</c> quando o arquivo não existe.</summary>
    byte[]? ReadBytes(string path);

    string? ReadText(string path);

    /// <summary>Arquivos sob <paramref name="directory"/> ("" = raiz), sem seguir links e sem pastas ignoradas.</summary>
    IReadOnlyList<string> EnumerateFiles(string directory, int maxDepth = 8);

    /// <summary>Escreve por arquivo temporário e renomeação. Recusa caminhos fora do escopo do <paramref name="scope"/>.</summary>
    void WriteAtomic(string path, byte[] content, WriteScope scope = WriteScope.Managed);

    void Delete(string path, WriteScope scope = WriteScope.Managed);

    /// <summary>Remove pastas vazias a partir de <paramref name="directory"/> para cima, sem passar da raiz.</summary>
    void PruneEmptyDirectories(string directory);
}

/// <summary>
/// <c>Managed</c>: arquivos que a aplicação gera (.claude/, .mariliz/, mariliz.yaml, mariliz.lock).
/// <c>TlcRestore</c>: só para desfazer o efeito do instalador do TLC, que é dono dos próprios arquivos.
/// </summary>
public enum WriteScope { Managed, TlcRestore }

public interface IProjectFilesFactory
{
    IProjectFiles Open(string authorizedRoot);
}

public sealed record ProcessSpec(
    string Executable,
    IReadOnlyList<string> Arguments,
    string WorkingDirectory,
    TimeSpan Timeout,
    string? StandardInput = null);

public sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError, bool TimedOut, bool NotFound);

/// <summary>Executa processos sem shell: executável e argumentos separados, nunca uma linha de comando.</summary>
public interface IProcessRunner
{
    Task<ProcessResult> RunAsync(ProcessSpec spec, CancellationToken ct);
}

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
