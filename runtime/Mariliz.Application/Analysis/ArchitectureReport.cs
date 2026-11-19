using Mariliz.Core.Discovery;

namespace Mariliz.Application.Analysis;

// Descreve o que o código é hoje, com evidência; não diz o que ele deveria ser. Quem compara com as referências é outro passo.
public sealed record ArchitectureReport(
    string ProjectId,
    DateTimeOffset AnalyzedAt,
    IReadOnlyList<DotnetProjectNode> DotnetProjects,
    IReadOnlyList<FrontendModule> Frontends,
    IReadOnlyList<ArchitectureFinding> Findings);

public sealed record DotnetProjectNode(string Path, string Name, string Layer, IReadOnlyList<string> References, IReadOnlyList<string> Frameworks, bool Tested);

public sealed record FrontendModule(string Root, string Source, IReadOnlyList<string> Folders, IReadOnlyList<string> Features,
    IReadOnlyList<string> FeatureDependencies, bool LayerLint);

public sealed record ArchitectureFinding(string Code, string Severity, string Title, IReadOnlyList<Evidence> Evidence);

public static class ArchitectureFindingCodes
{
    public const string DependencyDirection = "dependency-direction";
    public const string FrameworkInDomain = "framework-in-domain";
    public const string InfrastructureInApplication = "infrastructure-in-application";
    public const string FeatureCrossImport = "feature-cross-import";
    public const string SharedImportsFeature = "shared-imports-feature";
    public const string NoLayerLint = "no-layer-lint";
    public const string LargeFile = "large-file";
    public const string UntestedProject = "untested-project";
    public const string UntestedFeature = "untested-feature";
}

public static class ArchitectureSeverities
{
    // Fere a direção das camadas: o agente não deve repetir.
    public const string Warning = "warning";
    // Fato para ponderar (tamanho, falta de teste), não erro.
    public const string Info = "info";
}

public static class ArchitectureLayers
{
    public const string Domain = "domain";
    public const string Application = "application";
    public const string Infrastructure = "infrastructure";
    public const string Host = "host";
    public const string Shared = "shared";
    public const string Tests = "tests";
    public const string Unknown = "unknown";
}
