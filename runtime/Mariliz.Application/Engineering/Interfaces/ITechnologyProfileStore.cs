namespace Mariliz.Application.Engineering;

/// <summary>Guarda e lê o perfil tecnológico, sempre no diretório de dados do motor, um arquivo por projeto.</summary>
public interface ITechnologyProfileStore
{
    TechnologyProfile? Read(string projectId);

    void Write(TechnologyProfile profile);
}
