using Mariliz.Application.Cloud;
using Mariliz.Application.Execution;
using Mariliz.Application.Iac;
using Mariliz.Application.Integrations;
using Mariliz.Application.Management;
using Mariliz.Application.Ports;
using Mariliz.Application.Workspace;
using Mariliz.Infrastructure.Iac;
using Mariliz.Infrastructure.Integrations;

namespace Mariliz.Infrastructure;

// O sidecar e o servidor MCP montam a IaC e a gestão por aqui. Montadas em dois lugares, divergiram: o MCP rodava sem a
// política da edição e sem o histórico de riscos. O que muda de um para o outro (pasta autorizada, cloud, diário) entra
// como argumento, à vista de quem chama.
public sealed class EngineComposition : IDisposable
{
    private readonly IClock _clock;
    private readonly IAuditLog _audit;
    private readonly IProcessRunner _runner;
    // OpenTofu (padrão, gratuito) e Terraform ficam numa pasta própria dos dados do Spock; nada é instalado sem um pedido da
    // pessoa pela interface.
    private readonly OpenTofuToolchain _openTofu;
    private readonly HashiCorpTerraformToolchain _terraform;
    // O state de cada raiz é lido como texto; o registro garante que dois projetos do Spock não dividam o mesmo state.
    private readonly IacStateReader _states = new();
    private readonly FileIacStateRegistry _registry;

    public EngineComposition(string dataRoot, IClock clock, IAuditLog audit, IProcessRunner runner, IacDistributionPolicy iacPolicy)
    {
        DataRoot = dataRoot;
        IacPolicy = iacPolicy;
        _clock = clock;
        _audit = audit;
        _runner = runner;
        _openTofu = new OpenTofuToolchain(dataRoot, ExecutableLocator.FromEnvironment());
        _terraform = new HashiCorpTerraformToolchain(dataRoot, ExecutableLocator.FromEnvironment());
        _registry = new FileIacStateRegistry(dataRoot);
    }

    public string DataRoot { get; }

    public IacDistributionPolicy IacPolicy { get; }

    public IacRuntimeService IacRuntimes(WorkspaceAuthorizer workspace) =>
        new([_openTofu, _terraform], new FileIacSettingsStore(DataRoot), new IacConfigReader(), _runner, workspace, _clock, _audit, IacPolicy);

    public IacOperationService IacOperations(IacRuntimeService runtimes, WorkspaceAuthorizer workspace, CloudHub? cloud) =>
        new(runtimes, new CliIacRuntimeFactory(_runner, DataRoot), new IacConfigReader(), new FileIacWorkArea(DataRoot), workspace, cloud,
            _clock, _audit, _states, _registry);

    public IacStateService IacState(WorkspaceAuthorizer workspace, CloudHub? cloud, IProjectFilesFactory files, IRunStore store, RunTracker tracker) =>
        new(workspace, new IacConfigReader(), _states, _registry, cloud, files, store, _runner, _clock, tracker, _audit);

    public ManagementService Management(WorkspaceAuthorizer workspace, IntegrationHub hub) =>
        new(workspace, hub, _clock, new FileRiskHistoryStore(DataRoot));

    public void Dispose()
    {
        _openTofu.Dispose();
        _terraform.Dispose();
    }
}
