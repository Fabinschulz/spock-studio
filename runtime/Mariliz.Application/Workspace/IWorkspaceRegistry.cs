namespace Mariliz.Application.Workspace;

// O caminho não volta para a interface; serve só para o motor reconhecer o projeto depois de reiniciar.
public interface IWorkspaceRegistry
{
    IReadOnlyDictionary<string, string> Load();

    void Save(string projectId, string root);
}
