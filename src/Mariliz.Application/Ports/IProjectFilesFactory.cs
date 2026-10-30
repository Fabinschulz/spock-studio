namespace Mariliz.Application.Ports;

public interface IProjectFilesFactory
{
    IProjectFiles Open(string authorizedRoot);
}
