namespace Mariliz.Core.Discovery;

public sealed record DiscoveryReport(
    string ProjectId,
    string ProjectName,
    IReadOnlyList<StackFinding> Stacks,
    IReadOnlyList<ToolingFinding> Tooling,
    IReadOnlyList<AiEnvironmentFinding> AiEnvironments,
    HarnessInventory Inventory,
    SpecificationStatus Specification,
    GitState Git,
    ToolAvailability Tools,
    bool HasDeclaredConfig,
    IReadOnlyList<DiscoveryWarning> Warnings)
{
    // Fora das stacks: a lista de stacks vai para o workflow com teto de 8 e decide as regras do Harness.
    public IReadOnlyList<FrameworkFinding> Frameworks { get; init; } = [];

    // C3: os ambientes que a declaração já lista, para a tela partir deles. Vazio sem declaração válida.
    public IReadOnlyList<string> DeclaredAiEnvironments { get; init; } = [];

    public IReadOnlyList<RuntimeFinding> Runtimes { get; init; } = [];

    public HarnessNames Naming { get; init; } = HarnessNames.Of(Policy.HarnessNaming.Spock);
}
