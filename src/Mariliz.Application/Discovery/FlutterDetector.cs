using System.Text.RegularExpressions;
using Mariliz.Application.Ports;
using Mariliz.Core.Discovery;
using Mariliz.Core.Text;
using YamlDotNet.Serialization;

namespace Mariliz.Application.Discovery;

internal static class FlutterDetector
{
    internal static void Detect(IProjectFiles files, IReadOnlyList<string> all,
        List<StackFinding> stacks, List<ToolingFinding> tooling, List<DiscoveryWarning> warnings)
    {
        foreach (var pubspec in all.Where(p => Path.GetFileName(p) == "pubspec.yaml" && ProjectPath.Depth(p) <= 3).OrderBy(p => p, StringComparer.Ordinal))
        {
            var text = files.ReadText(pubspec);
            if (text is null) continue;
            try
            {
                var map = new DeserializerBuilder().Build().Deserialize<Dictionary<string, object?>>(text);
                if (map is null) continue;
                var usesFlutter = map.TryGetValue("dependencies", out var d) && d is Dictionary<object, object> deps && deps.ContainsKey("flutter");
                if (!usesFlutter) continue;

                string? sdk = null;
                if (map.TryGetValue("environment", out var envObj) && envObj is Dictionary<object, object> env)
                {
                    sdk = env.TryGetValue("flutter", out var f) ? f?.ToString() : env.TryGetValue("sdk", out var s) ? s?.ToString() : null;
                }
                var version = sdk is null ? null : Regex.Match(sdk, @"\d+(\.\d+){0,2}").Value is { Length: > 0 } v ? v : null;
                var root = ProjectPath.Parent(pubspec);
                if (version is null)
                    warnings.Add(new(DiscoveryWarningCodes.UnknownVersion, $"A versão do Flutter/Dart em {pubspec} não está declarada.", [pubspec]));

                stacks.Add(new StackFinding("flutter", "Flutter", root, version, version is null ? Confidence.Low : Confidence.Medium,
                    [new Evidence(pubspec, sdk is null ? "dependencies.flutter" : $"environment={sdk}")],
                    ["Versão lida da restrição declarada no pubspec."]));
                tooling.Add(new ToolingFinding("test", "flutter test", "flutter test", [new Evidence(pubspec)]));
                tooling.Add(new ToolingFinding("lint", "flutter analyze", "flutter analyze", [new Evidence(pubspec)]));
            }
            catch (YamlDotNet.Core.YamlException)
            {
                warnings.Add(new(DiscoveryWarningCodes.UnknownVersion, $"{pubspec} não é um YAML válido.", [pubspec]));
            }
        }
    }
}
