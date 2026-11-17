namespace Mariliz.Application.Workspace;

// O caminho não volta para a interface; serve só para o motor reconhecer o projeto depois de reiniciar.
public interface IWorkspaceRegistry
{
    IReadOnlyDictionary<string, string> Load();

    void Save(string projectId, string root);

    // F1: o id que o plano propõe para uma cópia ainda sem project.id. Guardado para replanejar (e reiniciar) não trocá-lo.
    IReadOnlyDictionary<string, string> LoadProposedStableIds() => new Dictionary<string, string>();

    void SaveProposedStableId(string projectId, string stableId)
    {
    }
}
