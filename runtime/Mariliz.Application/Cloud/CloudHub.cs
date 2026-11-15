using Mariliz.Application.Integrations;
using Mariliz.Application.Ports;
using Mariliz.Application.Workspace;

namespace Mariliz.Application.Cloud;

public sealed class CloudHub
{
    public CloudHub(ICloudStore store, CloudProviderRegistry providers, WorkspaceAuthorizer workspace, IClock clock, IAuditLog audit)
    {
        Providers = providers;
        Connections = new CloudConnectionService(store, providers, clock, audit);
        Projects = new CloudProjectService(store, Connections, providers, workspace, clock, audit);
        Reads = new CloudReadService(Connections, Projects, providers, clock, audit);
    }

    public CloudProviderRegistry Providers { get; }
    public CloudConnectionService Connections { get; }
    public CloudProjectService Projects { get; }
    public CloudReadService Reads { get; }
}
