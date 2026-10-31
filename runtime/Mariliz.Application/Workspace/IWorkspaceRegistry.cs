namespace Mariliz.Application.Workspace;

/// <summary>
/// Pastas já autorizadas, fora do protocolo. O caminho não volta para a interface;
/// serve só para o motor reconhecer o projeto depois de reiniciar.
/// </summary>
public interface IWorkspaceRegistry
{
    IReadOnlyDictionary<string, string> Load();

    void Save(string projectId, string root);
}
