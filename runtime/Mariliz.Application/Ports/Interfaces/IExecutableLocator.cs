namespace Mariliz.Application.Ports;

public interface IExecutableLocator
{
    /// <summary>O caminho absoluto do executável, ou null quando ele não está instalado.</summary>
    string? Find(string name);
}
