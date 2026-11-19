using System.Text.Json;
using System.Text.RegularExpressions;
using Mariliz.Application.Ports;
using Mariliz.Core.Discovery;
using Mariliz.Core.Text;

namespace Mariliz.Application.Discovery;

// O runtime que o projeto fixa, lido depois das stacks: a versão do React não diz com qual Node o projeto roda, e a solução
// .NET informa só o alvo mais novo. Sem arquivo que fixe a versão, nada é inventado.
internal static partial class RuntimeDetector
{
    internal const string SdkName = ".NET SDK";
    internal const string ProjectName = ".NET (projeto)";

    private static readonly string[] JavaScriptStacks = [Technologies.React.Id, Technologies.Nextjs.Id, Technologies.Angular.Id];

    // A stack e o runtime que um gerenciador de versões fixa para ela (MV-4).
    private static readonly string[] ManagedRuntimes =
        [Technologies.Python.Id, Technologies.Ruby.Id, Technologies.Go.Id, Technologies.Java.Id, Technologies.Php.Id, Technologies.Dotnet.Id, Technologies.Rust.Id];

    internal static List<RuntimeFinding> Detect(IProjectFiles files, IReadOnlyList<string> all, IReadOnlyList<StackFinding> stacks)
    {
        var found = new List<RuntimeFinding>();
        foreach (var stack in stacks.Where(stack => JavaScriptStacks.Contains(stack.Id) || stack.Id == Technologies.Node.Id))
            if (Node(files, all, stack.Root, engines: stack.Id != Technologies.Node.Id) is { } node) found.Add(node);
        foreach (var stack in stacks.Where(stack => stack.Id == Technologies.Dotnet.Id))
        {
            if (DotnetSdk(files, all, stack.Root) is { } sdk) found.Add(sdk);
            found.AddRange(DotnetProjects(files, all));
        }
        foreach (var stack in stacks.Where(stack => ManagedRuntimes.Contains(stack.Id)))
        {
            var technology = Technologies.Find(stack.Id)!;
            if (stack.Id == Technologies.Go.Id && GoToolchain(files, stack.Root) is { } toolchain) found.Add(toolchain);
            else if (VersionManagers.Find(files, all, stack.Root, stack.Id) is { } managed)
                found.Add(new RuntimeFinding(stack.Id, technology.Name, stack.Root, managed.Version, [managed.Evidence]));
            if (stack.Id == Technologies.Java.Id && GradleWrapper(files, all, stack.Root) is { } gradle) found.Add(gradle);
        }
        return found;
    }

    // O arquivo de versão vale para a pasta e as de baixo; o mais próximo vence. Depois vêm o Volta e os gerenciadores de
    // versão; engines.node é só o mínimo aceito, e numa stack Node pura ele já é a versão da stack.
    private static RuntimeFinding? Node(IProjectFiles files, IReadOnlyList<string> all, string root, bool engines)
    {
        foreach (var folder in SelfAndAncestors(root))
        {
            foreach (var name in new[] { ".nvmrc", ".node-version" })
            {
                var path = ProjectPath.Combine(folder, name);
                if (!all.Contains(path) || files.ReadText(path) is not { } text) continue;
                var pinned = text.Trim();
                if (NodeVersion().Match(pinned) is { Success: true } match)
                    return new RuntimeFinding(Technologies.Node.Id, "Node.js", root, match.Groups[1].Value, [new Evidence(path, pinned)]);
            }
        }

        var manifest = ProjectPath.Combine(root, "package.json");
        JsonDocument? doc = null;
        try
        {
            if (files.ReadText(manifest) is { } json) doc = JsonDocument.Parse(json, new JsonDocumentOptions { AllowTrailingCommas = true });
        }
        catch (JsonException)
        {
            // O detector do Node já avisa do package.json inválido.
        }

        using (doc)
        {
            if (doc is not null && doc.RootElement.TryGetProperty("volta", out var volta) && volta.ValueKind == JsonValueKind.Object
                && volta.TryGetProperty("node", out var voltaNode) && voltaNode.ValueKind == JsonValueKind.String
                && NodeVersion().Match(voltaNode.GetString()!) is { Success: true } pinnedByVolta)
                return new RuntimeFinding(Technologies.Node.Id, "Node.js", root, pinnedByVolta.Groups[1].Value,
                    [new Evidence(manifest, $"volta.node={voltaNode.GetString()}")]);
            if (VersionManagers.Find(files, all, root, Technologies.Node.Id) is { } managed)
                return new RuntimeFinding(Technologies.Node.Id, "Node.js", root, managed.Version, [managed.Evidence]);
            if (engines && doc is not null && doc.RootElement.TryGetProperty("engines", out var declared) && declared.ValueKind == JsonValueKind.Object
                && declared.TryGetProperty("node", out var range) && range.ValueKind == JsonValueKind.String
                && NodeVersion().Match(range.GetString()!) is { Success: true } minimum)
                return new RuntimeFinding(Technologies.Node.Id, "Node.js", root, minimum.Groups[1].Value,
                    [new Evidence(manifest, $"engines.node={range.GetString()} (mínimo aceito)")]);
        }
        return null;
    }

    // go.mod: a diretiva go é o mínimo da linguagem (versão da stack); toolchain é o Go que o projeto pede para compilar.
    private static RuntimeFinding? GoToolchain(IProjectFiles files, string root)
    {
        var manifest = ProjectPath.Combine(root, "go.mod");
        return files.ReadText(manifest) is { } text && GoToolchainLine().Match(text) is { Success: true } toolchain
            ? new RuntimeFinding(Technologies.Go.Id, "Go (toolchain)", root, toolchain.Groups[1].Value, [new Evidence(manifest, toolchain.Value.Trim())])
            : null;
    }

    // A versão do Gradle que o wrapper baixa: limita a sintaxe do build e os plugins que o projeto pode usar.
    private static RuntimeFinding? GradleWrapper(IProjectFiles files, IReadOnlyList<string> all, string root)
    {
        var path = ProjectPath.Combine(root, "gradle/wrapper/gradle-wrapper.properties");
        return all.Contains(path) && files.ReadText(path) is { } text && GradleDistribution().Match(text) is { Success: true } distribution
            ? new RuntimeFinding("gradle", "Gradle (wrapper)", root, distribution.Groups[1].Value, [new Evidence(path, $"distributionUrl=...gradle-{distribution.Groups[1].Value}")])
            : null;
    }

    private static RuntimeFinding? DotnetSdk(IProjectFiles files, IReadOnlyList<string> all, string root)
    {
        foreach (var folder in SelfAndAncestors(root))
        {
            var path = ProjectPath.Combine(folder, "global.json");
            if (!all.Contains(path) || files.ReadText(path) is not { } text) continue;
            try
            {
                using var doc = JsonDocument.Parse(text, new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
                if (doc.RootElement.TryGetProperty("sdk", out var sdk) && sdk.ValueKind == JsonValueKind.Object
                    && sdk.TryGetProperty("version", out var version) && version.ValueKind == JsonValueKind.String)
                {
                    var rollForward = sdk.TryGetProperty("rollForward", out var policy) && policy.ValueKind == JsonValueKind.String ? $", rollForward={policy.GetString()}" : "";
                    return new RuntimeFinding(Technologies.Dotnet.Id, SdkName, root, version.GetString()!, [new Evidence(path, $"sdk.version={version.GetString()}{rollForward}")]);
                }
            }
            catch (JsonException)
            {
                return null;
            }
        }
        return null;
    }

    // Só quando a solução mistura alvos: com um alvo só, a versão da stack já diz tudo e uma linha por projeto seria ruído.
    private static IEnumerable<RuntimeFinding> DotnetProjects(IProjectFiles files, IReadOnlyList<string> all)
    {
        var central = new DotnetDetector.CentralVersions(files, all);
        var projects = all.Where(path => path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)).Order(StringComparer.Ordinal)
            .Select(path => DotnetDetector.Read(files, path, central, []))
            .OfType<DotnetDetector.DotnetProject>()
            .Where(project => project.Targets.Count > 0)
            .ToList();
        if (projects.Select(project => string.Join(';', project.Targets)).Distinct(StringComparer.OrdinalIgnoreCase).Count() < 2) yield break;
        foreach (var project in projects)
            if (DotnetDetector.Oldest(project.Targets) is { } oldest)
                yield return new RuntimeFinding(Technologies.Dotnet.Id, ProjectName, ProjectPath.Parent(project.Path), oldest,
                    [new Evidence(project.Path, $"TargetFramework={string.Join(';', project.Targets)}")]);
    }

    internal static IEnumerable<string> SelfAndAncestors(string root)
    {
        var folder = root;
        while (true)
        {
            yield return folder;
            if (folder.Length == 0) yield break;
            folder = ProjectPath.Parent(folder);
        }
    }

    [GeneratedRegex(@"^\D*?(\d+(\.\d+){0,2})")]
    private static partial Regex NodeVersion();

    [GeneratedRegex(@"(?m)^\s*toolchain\s+go(\d+\.\d+(?:\.\d+)?)")]
    private static partial Regex GoToolchainLine();

    [GeneratedRegex(@"distributionUrl\s*=\s*\S*gradle-(\d+(?:\.\d+){0,2})(?:-[\w.]+)?-(?:bin|all)\.zip")]
    private static partial Regex GradleDistribution();
}
