using System.Text.Json;
using System.Text.RegularExpressions;
using Mariliz.Application.Ports;
using Mariliz.Core.Discovery;
using Mariliz.Core.Text;

namespace Mariliz.Application.Discovery;

internal static class NodeDetector
{
    internal static void Detect(IProjectFiles files, IReadOnlyList<string> all,
        List<StackFinding> stacks, List<ToolingFinding> tooling, List<DiscoveryWarning> warnings)
    {
        var manifests = all.Where(p => Path.GetFileName(p) == "package.json" && ProjectPath.Depth(p) <= 3).OrderBy(p => p, StringComparer.Ordinal).ToList();
        foreach (var manifest in manifests)
        {
            var text = files.ReadText(manifest);
            if (text is null) continue;
            JsonDocument doc;
            try { doc = JsonDocument.Parse(text, new JsonDocumentOptions { AllowTrailingCommas = true }); }
            catch (JsonException)
            {
                warnings.Add(new(DiscoveryWarningCodes.UnknownVersion, $"{manifest} não é um JSON válido.", [manifest]));
                continue;
            }

            using (doc)
            {
                var root = ProjectPath.Parent(manifest);
                var deps = CollectDependencies(doc.RootElement);
                var (id, name, depName) =
                    deps.ContainsKey("next") ? ("nextjs", "Next.js", "next")
                    : deps.ContainsKey("@angular/core") ? ("angular", "Angular", "@angular/core")
                    : deps.ContainsKey("react") ? ("react", "React", "react")
                    : ("node", "Node.js", "");

                if (id == "node" && deps.Count == 0 && !doc.RootElement.TryGetProperty("scripts", out _)) continue;

                var evidence = new List<Evidence>();
                string? version = null;
                var confidence = Confidence.Medium;
                var notes = new List<string>();

                if (depName.Length > 0)
                {
                    var installed = files.ReadText(ProjectPath.Combine(root, $"node_modules/{depName}/package.json"));
                    version = ReadVersionField(installed);
                    if (version is not null)
                    {
                        confidence = Confidence.High;
                        evidence.Add(new Evidence(ProjectPath.Combine(root, $"node_modules/{depName}/package.json"), $"version={version}"));
                    }
                    else
                    {
                        var range = deps[depName];
                        version = Regex.Match(range, @"\d+(\.\d+){0,2}").Value is { Length: > 0 } v ? v : null;
                        evidence.Add(new Evidence(manifest, $"{depName}={range} (faixa declarada, não instalada)"));
                        notes.Add("Versão lida da faixa declarada; instale as dependências para confirmar.");
                    }
                }
                else
                {
                    evidence.Add(new Evidence(manifest));
                    if (doc.RootElement.TryGetProperty("engines", out var eng) && eng.TryGetProperty("node", out var nodeRange))
                        { version = Regex.Match(nodeRange.GetString() ?? "", @"\d+(\.\d+){0,2}").Value; version = version?.Length == 0 ? null : version; evidence.Add(new Evidence(manifest, $"engines.node={nodeRange.GetString()}")); }
                }

                if (deps.ContainsKey("typescript") || all.Contains(ProjectPath.Combine(root, "tsconfig.json"))) notes.Add("TypeScript");
                if (id is "nextjs" or "angular" && deps.ContainsKey("react") && id == "nextjs") notes.Add("React");

                if (version is null)
                    warnings.Add(new(DiscoveryWarningCodes.UnknownVersion, $"A versão de {name} em {manifest} não pôde ser determinada.", [manifest]));

                stacks.Add(new StackFinding(id, name, root, version, version is null ? Confidence.Low : confidence, evidence, notes));
                DetectNodeTooling(files, all, doc.RootElement, root, manifest, tooling, warnings);
            }
        }
    }

    private static void DetectNodeTooling(IProjectFiles files, IReadOnlyList<string> all, JsonElement manifest,
        string root, string manifestPath, List<ToolingFinding> tooling, List<DiscoveryWarning> warnings)
    {
        var locks = new (string File, string Manager)[]
        {
            ("pnpm-lock.yaml", "pnpm"), ("yarn.lock", "yarn"), ("package-lock.json", "npm"), ("bun.lockb", "bun"), ("bun.lock", "bun")
        };
        var found = locks.Where(l => all.Contains(ProjectPath.Combine(root, l.File))).ToList();
        string? declared = null;
        if (manifest.TryGetProperty("packageManager", out var pm) && pm.ValueKind == JsonValueKind.String)
            declared = pm.GetString()?.Split('@')[0];

        var managers = found.Select(f => f.Manager).Distinct().ToList();
        if (declared is not null && !managers.Contains(declared)) managers.Add(declared);
        if (found.Select(f => f.Manager).Distinct().Count() > 1 || (declared is not null && found.Count > 0 && found.All(f => f.Manager != declared)))
            warnings.Add(new(DiscoveryWarningCodes.ConflictingPackageManagers,
                $"Gerenciadores de pacotes divergentes em {(root == "" ? "." : root)}: {string.Join(", ", managers)}.",
                found.Select(f => ProjectPath.Combine(root, f.File)).Append(manifestPath).ToList()));

        var manager = declared ?? found.FirstOrDefault().Manager ?? (managers.Count > 0 ? managers[0] : null);
        if (manager is not null)
        {
            var ev = found.Select(f => new Evidence(ProjectPath.Combine(root, f.File))).ToList();
            if (declared is not null) ev.Add(new Evidence(manifestPath, $"packageManager={pm.GetString()}"));
            tooling.Add(new ToolingFinding("package-manager", manager, null, ev));
        }

        if (manifest.TryGetProperty("scripts", out var scripts) && scripts.ValueKind == JsonValueKind.Object)
        {
            foreach (var kind in new[] { "build", "test", "lint", "typecheck" })
            {
                if (!scripts.TryGetProperty(kind, out var cmd) || cmd.ValueKind != JsonValueKind.String) continue;
                var runner = manager ?? "npm";
                var command = kind == "test" && runner == "npm" ? "npm test" : $"{runner} run {kind}";
                tooling.Add(new ToolingFinding(kind, $"{runner} {kind}", command, [new Evidence(manifestPath, $"scripts.{kind}={Ellipsis.Truncate(cmd.GetString() ?? "", 80)}")]));
            }
        }
    }

    private static Dictionary<string, string> CollectDependencies(JsonElement root)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var section in new[] { "dependencies", "devDependencies", "peerDependencies" })
        {
            if (!root.TryGetProperty(section, out var el) || el.ValueKind != JsonValueKind.Object) continue;
            foreach (var p in el.EnumerateObject()) result.TryAdd(p.Name, p.Value.ValueKind == JsonValueKind.String ? p.Value.GetString() ?? "" : "");
        }
        return result;
    }

    private static string? ReadVersionField(string? packageJson)
    {
        if (packageJson is null) return null;
        try
        {
            using var doc = JsonDocument.Parse(packageJson);
            return doc.RootElement.TryGetProperty("version", out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
        }
        catch (JsonException) { return null; }
    }
}
