using Mariliz.Application.Discovery;
using Mariliz.Application.Execution;
using Mariliz.Application.Ports;
using Mariliz.Core.Discovery;

namespace Mariliz.Application.Harness;

/// <summary>Descobre o projeto e calcula o plano a partir do que existe no disco naquele momento.</summary>
internal sealed class ProjectPlanner(IProcessRunner runner)
{
    private readonly ProjectDiscoverer _discoverer = new(runner);

    public Task<DiscoveryReport> DiscoverAsync(string projectId, string root, IProjectFiles files, CancellationToken ct) =>
        _discoverer.DiscoverAsync(projectId, root, files, ct);

    public async Task<Replanned> ReplanAsync(string projectId, string root, IProjectFiles files, PlanOptions options, CancellationToken ct)
    {
        var discovery = await _discoverer.DiscoverAsync(projectId, root, files, ct);
        var (declared, declaredError) = DeclaredConfig.TryRead(files);

        var composed = declaredError is null
            ? HarnessComposer.Compose(discovery, files, declared, ProposalReader.Read(files), options)
            : InvalidDeclaredConfig(declaredError);

        var plan = PlanBuilder.Build(projectId, discovery, files, composed, ManagedLockFile.Read(files));
        return new Replanned(plan, composed, discovery);
    }

    private static ComposeResult InvalidDeclaredConfig(string error) =>
        new("", DecisionOrigins.Baseline, [], [], [],
            [new(LimitationCodes.InvalidDeclaredConfig, error + " Corrija o arquivo e planeje de novo.")], [], []);
}
