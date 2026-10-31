using Mariliz.Application.Ports;
using Mariliz.Core.Discovery;

namespace Mariliz.Application.Harness;

/// <summary>
/// Decide o estado desejado do Harness. Precedência: o que a pessoa declarou em mariliz.yaml, depois a proposta
/// validada do Harness Engineer, depois as regras-base. Nada daqui escreve em disco.
/// </summary>
public static class HarnessComposer
{
    public static ComposeResult Compose(
        DiscoveryReport report, IProjectFiles files, DeclaredConfig? declared, ProposalResult proposal, PlanOptions options) =>
        new HarnessComposition(report, files, declared, proposal, options).Build();
}
