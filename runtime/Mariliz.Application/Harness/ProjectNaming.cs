using Mariliz.Application.Ports;
using Mariliz.Core.Policy;

namespace Mariliz.Application.Harness;

public static class ProjectNaming
{
    // Relido a cada chamada: depois da primeira aplicação a declaração e o lock existem com o mesmo nome, então a resposta não muda.
    public static HarnessNaming Naming(this IProjectFiles files) => HarnessNaming.Detect(files.FileExists, files.DirectoryExists);
}
