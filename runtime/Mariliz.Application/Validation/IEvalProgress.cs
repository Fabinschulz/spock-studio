namespace Mariliz.Application.Validation;

// Só o que o motor observou.
public interface IEvalProgress
{
    void Started(string id, string name, string kind);

    void Completed(string id, string status);
}
