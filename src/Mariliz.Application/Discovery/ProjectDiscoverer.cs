using Mariliz.Application.Harness;
using Mariliz.Application.Ports;
using Mariliz.Core.Discovery;

namespace Mariliz.Application.Discovery;

/// <summary>
/// Análise somente de leitura: stack, ferramentas, ambiente de IA e Harness existente.
/// Cada conclusão carrega a evidência (arquivo e valor lido). Versão desconhecida é dita como tal.
/// </summary>
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

        var stacks = new List<StackFinding>();
        var tooling = new List<ToolingFinding>();
        DotnetDetector.Detect(files, all, stacks, tooling, warnings);
        NodeDetector.Detect(files, all, stacks, tooling, warnings);
        FlutterDetector.Detect(files, all, stacks, tooling, warnings);
        CiDetector.Detect(all, tooling);
        if (stacks.Count == 0)
            warnings.Add(new(DiscoveryWarningCodes.NoStackDetected, "Nenhuma stack conhecida foi detectada pelos arquivos de configuração.", []));

        var aiEnvironments = AiEnvironmentDetector.Detect(files, all, warnings);
        var inventory = HarnessInventoryReader.Read(files, all, managed, warnings);

        var tlc = TlcInspector.Inspect(files);
        if (tlc.State == TlcState.Drifted)
            warnings.Add(new(DiscoveryWarningCodes.TlcDrift, "A instalação do TLC diverge do que o instalador registrou.", [TlcManifest.SkillDirectory]));
        if (tlc.State is TlcState.Installed or TlcState.Drifted && !tlc.VersionTested)
            warnings.Add(new(DiscoveryWarningCodes.TlcUntestedVersion,
                $"A versão {tlc.Version ?? "desconhecida"} do TLC não foi validada por esta fase (validada: {string.Join(", ", TlcManifest.TestedVersions)}).", []));

        var tools = await _probe.ProbeToolsAsync(rootPath, ct);
        if (tools.Python is null)
            warnings.Add(new(DiscoveryWarningCodes.PythonMissing, "Python 3 não foi encontrado; os gates do TLC e o hook de política dependem dele.", []));
        if (tools.Node is null)
            warnings.Add(new(DiscoveryWarningCodes.NodeMissing, "Node.js não foi encontrado; o instalador oficial do TLC depende de npx (Node 22 ou superior).", []));

        var git = await _probe.ReadGitStateAsync(rootPath, files, tools, ct);
        if (!git.IsRepository)
            warnings.Add(new(DiscoveryWarningCodes.NotAGitRepository, "A pasta não é um repositório git; não há como reverter por controle de versão.", []));
        else if (git.HasUncommittedChanges == true)
            warnings.Add(new(DiscoveryWarningCodes.UncommittedChanges, "Há alterações locais ainda não commitadas.", git.ChangedPaths));

        return new DiscoveryReport(
            projectId, files.RootName, stacks, tooling, aiEnvironments, inventory, tlc, git, tools,
            files.FileExists(DeclaredConfig.FileName), warnings);
    }
}
