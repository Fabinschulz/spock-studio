namespace Mariliz.Application.Validation;

/// <summary>Quem acompanha os Evals um a um. Só o que o motor observou: o início de cada verificação e o seu resultado.</summary>
public interface IEvalProgress
{
    void Started(string id, string name, string kind);

    void Completed(string id, string status);
}
