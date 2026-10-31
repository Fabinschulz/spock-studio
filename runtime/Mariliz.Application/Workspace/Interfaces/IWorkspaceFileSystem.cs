namespace Mariliz.Application.Workspace;

/// <summary>Porta para o sistema de arquivos usada na autorização de pastas.</summary>
public interface IWorkspaceFileSystem
{
    /// <summary>Caminho absoluto canônico (links resolvidos) ou <c>null</c> se a pasta não existir.</summary>
    string? ResolveDirectory(string path);

    bool CanEnumerate(string directory);

    /// <summary>Pasta do usuário, usada só para abreviar caminhos exibidos.</summary>
    string HomeDirectory { get; }
}
