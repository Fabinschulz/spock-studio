namespace Mariliz.Application.Engineering;

public interface ITechnologyProfileStore
{
    TechnologyProfile? Read(string projectId);

    void Write(TechnologyProfile profile);
}
