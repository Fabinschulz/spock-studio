using Mariliz.Application.Ports;

namespace Mariliz.Infrastructure;

public sealed class LocalProjectFilesFactory : IProjectFilesFactory
{
    public IProjectFiles Open(string authorizedRoot) => new LocalProjectFiles(authorizedRoot);
}
