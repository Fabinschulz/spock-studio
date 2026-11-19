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
    private static readonly string[] JavaScriptStacks = [Technologies.React.Id, Technologies.Nextjs.Id, Technologies.Angular.Id];

    internal static List<RuntimeFinding> Detect(IProjectFiles files, IReadOnlyList<string> all, IReadOnlyList<StackFinding> stacks)
    {
        var found = new List<RuntimeFinding>();
        foreach (var stack in stacks.Where(stack => JavaScriptStacks.Contains(stack.Id)))
            if (Node(files, all, stack.Root) is { } node) found.Add(node);
        foreach (var stack in stacks.Where(stack => stack.Id == Technologies.Dotnet.Id))
        {
            if (DotnetSdk(files, all, stack.Root) is { } sdk) found.Add(sdk);
            found.AddRange(DotnetProjects(files, all));
        }
        return found;
    }

    // O arquivo de versão vale para a pasta e as de baixo; o mais próximo vence. engines.node é só o mínimo aceito.
    private static RuntimeFinding? Node(IProjectFiles files, IReadOnlyList<string> all, string root)
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
        if (files.ReadText(manifest) is not { } json) return null;
        try
        {
            using var doc = JsonDocument.Parse(json, new JsonDocumentOptions { AllowTrailingCommas = true });
            if (doc.RootElement.TryGetProperty("engines", out var engines) && engines.ValueKind == JsonValueKind.Object
                && engines.TryGetProperty("node", out var range) && range.ValueKind == JsonValueKind.String
                && NodeVersion().Match(range.GetString()!) is { Success: true } minimum)
                return new RuntimeFinding(Technologies.Node.Id, "Node.js", root, minimum.Groups[1].Value,
                    [new Evidence(manifest, $"engines.node={range.GetString()} (mínimo aceito)")]);
        }
        catch (JsonException)
        {
            // O detector do Node já avisa do package.json inválido.
        }
        return null;
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
                    return new RuntimeFinding(Technologies.Dotnet.Id, ".NET SDK", root, version.GetString()!, [new Evidence(path, $"sdk.version={version.GetString()}{rollForward}")]);
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
                yield return new RuntimeFinding(Technologies.Dotnet.Id, ".NET (projeto)", ProjectPath.Parent(project.Path), oldest,
                    [new Evidence(project.Path, $"TargetFramework={string.Join(';', project.Targets)}")]);
    }

    private static IEnumerable<string> SelfAndAncestors(string root)
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
}
