using System.Reflection;
using Mariliz.Application.Iac;

namespace Mariliz.Runtime;

// A edição vem do build (-p:SpockEdition=commercial) e vale igual para o sidecar e para o modo MCP; a comercial não instala o
// Terraform (D-02).
internal static class BuildEdition
{
    public static IacDistributionPolicy IacPolicy() =>
        IacDistributionPolicy.ForEdition(typeof(BuildEdition).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attribute => attribute.Key == "SpockEdition")?.Value);
}
