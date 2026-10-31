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
    IReadOnlyList<DiscoveryWarning> Warnings);
