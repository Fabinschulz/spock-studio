using Mariliz.Application.Cloud;
using Mariliz.Application.Integrations;
using Mariliz.Application.Ports;
using Mariliz.Application.Workspace;
using Mariliz.Infrastructure.Cloud.Aws;
using Mariliz.Infrastructure.Cloud.Azure;
using Mariliz.Infrastructure.Cloud.Gcp;

namespace Mariliz.Infrastructure.Cloud;

// Os provedores desta versão são registrados aqui; um provedor novo entra nesta lista e em CloudProviders.
public static class LocalCloudHub
{
    public static IReadOnlyList<ICloudProvider> Providers() => [new AwsCloudProvider(), new AzureCloudProvider(), new GcpCloudProvider()];

    public static CloudHub Create(string dataRoot, WorkspaceAuthorizer workspace, IClock clock, IAuditLog audit) =>
        new(new FileCloudStore(dataRoot), new CloudProviderRegistry(Providers()), workspace, clock, audit);
}
