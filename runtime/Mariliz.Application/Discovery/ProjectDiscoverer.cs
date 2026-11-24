using Mariliz.Application.Harness;
using Mariliz.Application.Ports;
using Mariliz.Application.Specification;
using Mariliz.Core.Discovery;

namespace Mariliz.Application.Discovery;

// Somente leitura. Cada conclusão carrega a evidência (arquivo e valor lido); versão desconhecida é dita como tal.
public sealed class ProjectDiscoverer(IProcessRunner processRunner)
{
    private readonly SystemProbe _probe = new(processRunner);

    public async Task<DiscoveryReport> DiscoverAsync(
        string projectId, string rootPath, IProjectFiles files, CancellationToken ct)
    {
        var all = files.EnumerateFiles("", 6);
        var warnings = new List<DiscoveryWarning>();

        var lockFile = ManagedLockFile.Read(files);
        var managed = lockFile?.ManagedFiles.Select(m => m.Path).ToHashSet(StringComparer.Ordinal) ?? [];

        var (stacks, tooling, frameworks, runtimes) = Scan(files, all, warnings);
        if (stacks.Count == 0)
            warnings.Add(new(DiscoveryWarningCodes.NoStackDetected, "Nenhuma stack conhecida foi detectada pelos arquivos de configuração.", []));

        var aiEnvironments = AiEnvironmentDetector.Detect(files, all, warnings);
        var inventory = HarnessInventoryReader.Read(files, all, managed, warnings);

        var specification = SpecificationInspector.Inspect(files);
        if (specification.State == SpecificationState.Drifted)
            warnings.Add(new(DiscoveryWarningCodes.SpecificationDrift, "A estrutura de especificação diverge do que o instalador registrou.", [SpecificationManifest.SkillDirectory]));
        if (specification.State is SpecificationState.Installed or SpecificationState.Drifted && !specification.VersionTested)
            warnings.Add(new(DiscoveryWarningCodes.SpecificationUntestedVersion,
                $"A versão {specification.Version ?? "desconhecida"} da estrutura de especificação não foi validada por esta fase (validada: {string.Join(", ", SpecificationManifest.TestedVersions)}).", []));

        var tools = await _probe.ProbeToolsAsync(rootPath, ct);
        if (tools.Python is null)
            warnings.Add(new(DiscoveryWarningCodes.PythonMissing, "Python 3 não foi encontrado; as validações da especificação e o hook de política dependem dele.", []));
        if (tools.Node is null)
            warnings.Add(new(DiscoveryWarningCodes.NodeMissing, "Node.js não foi encontrado; o instalador oficial da estrutura de especificação depende de npx (Node 22 ou superior).", []));

        var git = await _probe.ReadGitStateAsync(rootPath, files, tools, ct);
        if (!git.IsRepository)
            warnings.Add(new(DiscoveryWarningCodes.NotAGitRepository, "A pasta não é um repositório git; não há como reverter por controle de versão.", []));
        else if (git.HasUncommittedChanges == true)
            warnings.Add(new(DiscoveryWarningCodes.UncommittedChanges, "Há alterações locais ainda não commitadas.", git.ChangedPaths));

        var naming = files.Naming();
        return new DiscoveryReport(
            projectId, files.RootName, stacks, tooling, aiEnvironments, inventory, specification, git, tools,
            files.FileExists(naming.Declaration), warnings)
        {
            Frameworks = frameworks,
            DeclaredAiEnvironments = DeclaredConfig.TryRead(files).Config?.Environments ?? [],
            Runtimes = runtimes,
            Naming = HarnessNames.Of(naming)
        };
    }

    // Só arquivos, sem processo: a checagem de versões depois da tarefa (MV-3) usa isto sem rodar git, python ou node.
    public static TechnologyScan Scan(IProjectFiles files, List<DiscoveryWarning>? warnings = null) =>
        Scan(files, files.EnumerateFiles("", 6), warnings ?? []);

    private static TechnologyScan Scan(IProjectFiles files, IReadOnlyList<string> all, List<DiscoveryWarning> warnings)
    {
        var stacks = new List<StackFinding>();
        var tooling = new List<ToolingFinding>();
        var frameworks = new List<FrameworkFinding>();
        DotnetDetector.Detect(files, all, stacks, tooling, frameworks, warnings);
        NodeDetector.Detect(files, all, stacks, tooling, frameworks, warnings);
        FlutterDetector.Detect(files, all, stacks, tooling, warnings);
        LanguageDetectors.Detect(files, all, stacks, tooling, frameworks, warnings);
        CiDetector.Detect(all, tooling);
        return new TechnologyScan(stacks, tooling, frameworks, RuntimeDetector.Detect(files, all, stacks));
    }
}

public sealed record TechnologyScan(
    List<StackFinding> Stacks,
    List<ToolingFinding> Tooling,
    List<FrameworkFinding> Frameworks,
    List<RuntimeFinding> Runtimes);
