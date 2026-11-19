using System.Text.Json;
using System.Text.RegularExpressions;
using Mariliz.Core.Discovery;
using Mariliz.Core.Text;

namespace Mariliz.Application.Analysis;

// React e Next pela estrutura app/features/shared (REF-R1): uma feature usa outra só pela fronteira pública (o index, service,
// form ou domain dela), nunca pelo interior; e shared não importa feature.
internal static partial class FrontendArchitecture
{
    private const int MaxEvidence = 3;
    private const string Features = "features";
    private const string SharedFolder = "shared";

    private static readonly string[] CodeExtensions = [".ts", ".tsx", ".js", ".jsx", ".mjs"];
    private static readonly string[] LintConfigs = ["eslint.config.js", "eslint.config.mjs", "eslint.config.cjs", "eslint.config.ts", ".eslintrc", ".eslintrc.json", ".eslintrc.js", ".eslintrc.cjs"];
    private static readonly string[] LayerRules = ["no-restricted-imports", "no-restricted-paths", "boundaries"];
    private static readonly HashSet<string> PublicBoundaries = new(StringComparer.Ordinal) { "index", "service", "form", "domain" };

    internal static (List<FrontendModule> Modules, List<ArchitectureFinding> Findings) Analyze(IReadOnlyList<string> all, Func<string, string?> read)
    {
        var modules = new List<FrontendModule>();
        var findings = new List<ArchitectureFinding>();
        foreach (var manifest in all.Where(path => Path.GetFileName(path) == "package.json").Order(StringComparer.Ordinal))
        {
            if (!UsesReact(read(manifest))) continue;
            var root = ProjectPath.Parent(manifest);
            var srcFolder = ProjectPath.Combine(root, "src");
            var source = all.Any(path => Inside(srcFolder, path)) ? srcFolder : root;
            var code = all.Where(path => Inside(source, path) && IsCode(path)).ToList();
            if (code.Count == 0) continue;

            var folders = code.Select(path => FirstFolder(source, path)).OfType<string>().Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
            var featuresFolder = ProjectPath.Combine(source, Features);
            var features = code.Select(path => FirstFolder(featuresFolder, path)).OfType<string>().Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
            var lintConfig = LintConfigs.Select(name => ProjectPath.Combine(root, name)).FirstOrDefault(all.Contains);
            var layerLint = lintConfig is not null && read(lintConfig) is { } lint && LayerRules.Any(rule => lint.Contains(rule, StringComparison.Ordinal));
            var dependencies = features.Count == 0 ? [] : Imports(code, source, featuresFolder, read, findings);
            modules.Add(new FrontendModule(root.Length == 0 ? "." : root, source.Length == 0 ? "." : source, folders, features, dependencies, layerLint));
            if (features.Count == 0) continue;

            if (!layerLint)
                findings.Add(new ArchitectureFinding(ArchitectureFindingCodes.NoLayerLint, ArchitectureSeverities.Info,
                    $"{(root.Length == 0 ? "O frontend" : root)} tem features, mas nenhuma regra de lint impede import entre elas",
                    [new Evidence(lintConfig ?? manifest)]));

            var untested = features.Where(feature => !code.Any(path => Inside(ProjectPath.Combine(featuresFolder, feature), path) && IsTest(path))).ToList();
            if (untested.Count > 0)
                findings.Add(new ArchitectureFinding(ArchitectureFindingCodes.UntestedFeature, ArchitectureSeverities.Info,
                    $"{untested.Count} feature(s) sem arquivo de teste na própria pasta: {string.Join(", ", untested.Take(8))}",
                    untested.Take(8).Select(feature => new Evidence(ProjectPath.Combine(featuresFolder, feature))).ToList()));
        }
        return (modules, findings);
    }

    // Devolve o grafo entre features ("vagas → candidaturas"); o import pelo interior de outra feature vira achado.
    private static List<string> Imports(List<string> code, string source, string featuresFolder, Func<string, string?> read, List<ArchitectureFinding> findings)
    {
        var edges = new SortedSet<string>(StringComparer.Ordinal);
        var crossing = new Dictionary<(string From, string To), List<Evidence>>();
        var fromShared = new Dictionary<string, List<Evidence>>(StringComparer.Ordinal);
        var shared = ProjectPath.Combine(source, SharedFolder);
        foreach (var file in code.Where(path => !IsTest(path)))
        {
            var origin = FirstFolder(featuresFolder, file);
            var inShared = Inside(shared, file);
            if (origin is null && !inShared) continue;
            if (read(file) is not { } text) continue;
            foreach (Match match in Import().Matches(text))
            {
                var specifier = match.Groups["spec"].Value;
                // O alvo vem sem extensão e pode ser a própria pasta da feature (@/features/vagas, pelo index).
                if (Resolve(source, file, specifier) is not { } target || FirstFolder(featuresFolder, target + "/") is not { } feature) continue;
                if (origin is not null && origin != feature)
                {
                    edges.Add($"{origin} → {feature}");
                    if (!IsPublic(target)) Add(crossing, (origin, feature), new Evidence(file, $"import {specifier}"));
                }
                else if (inShared)
                    Add(fromShared, feature, new Evidence(file, $"import {specifier}"));
            }
        }

        foreach (var ((from, to), evidence) in crossing.OrderBy(pair => pair.Key.From, StringComparer.Ordinal).ThenBy(pair => pair.Key.To, StringComparer.Ordinal))
            findings.Add(new ArchitectureFinding(ArchitectureFindingCodes.FeatureCrossImport, ArchitectureSeverities.Warning,
                $"A feature {from} importa o interior da feature {to} ({Count(evidence.Count)})", evidence.Take(MaxEvidence).ToList()));
        foreach (var (feature, evidence) in fromShared.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            findings.Add(new ArchitectureFinding(ArchitectureFindingCodes.SharedImportsFeature, ArchitectureSeverities.Warning,
                $"shared importa a feature {feature} ({Count(evidence.Count)})", evidence.Take(MaxEvidence).ToList()));
        return edges.ToList();
    }

    // "@/features/vagas", ".../vagas/service" e ".../recrutamento/vagas/domain" são fronteira; ".../vagas/detail/card" é interior.
    private static bool IsPublic(string target)
    {
        var last = target[(target.LastIndexOf('/') + 1)..];
        var parts = target.Split('/');
        var features = Array.LastIndexOf(parts, Features);
        return parts.Length - features == 2 || PublicBoundaries.Contains(last);
    }

    private static string Count(int imports) => imports == 1 ? "1 import" : $"{imports} imports";

    private static void Add<TKey>(Dictionary<TKey, List<Evidence>> groups, TKey key, Evidence evidence) where TKey : notnull
    {
        if (!groups.TryGetValue(key, out var list)) groups[key] = list = [];
        list.Add(evidence);
    }

    // Relativo ao arquivo, ou pelos apelidos comuns da pasta de código (@/ e ~/). Pacote do node_modules volta nulo.
    private static string? Resolve(string source, string file, string specifier)
    {
        if (specifier.StartsWith("./", StringComparison.Ordinal) || specifier.StartsWith("../", StringComparison.Ordinal))
            return ProjectPath.Resolve(ProjectPath.Parent(file), specifier);
        if (specifier.StartsWith("@/", StringComparison.Ordinal) || specifier.StartsWith("~/", StringComparison.Ordinal))
            return ProjectPath.Combine(source, specifier[2..]);
        return null;
    }

    private static bool UsesReact(string? manifest)
    {
        if (manifest is null) return false;
        try
        {
            using var document = JsonDocument.Parse(manifest);
            return new[] { "dependencies", "devDependencies", "peerDependencies" }.Any(section =>
                document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty(section, out var dependencies) && dependencies.ValueKind == JsonValueKind.Object
                && (dependencies.TryGetProperty("react", out _) || dependencies.TryGetProperty("next", out _)));
        }
        catch (JsonException)
        {
            return false;
        }
    }

    // A primeira pasta abaixo de folder ("src/features/vagas/x.ts" em "src/features" dá "vagas"); arquivo solto na pasta dá nulo.
    private static string? FirstFolder(string folder, string path)
    {
        if (!Inside(folder, path)) return null;
        var rest = folder.Length == 0 ? path : path[(folder.Length + 1)..];
        var slash = rest.IndexOf('/');
        return slash < 0 ? null : rest[..slash];
    }

    private static bool Inside(string folder, string path) => folder.Length == 0 || path.StartsWith(folder + "/", StringComparison.Ordinal);

    internal static bool IsCode(string path) =>
        CodeExtensions.Any(extension => path.EndsWith(extension, StringComparison.OrdinalIgnoreCase)) && !path.EndsWith(".d.ts", StringComparison.OrdinalIgnoreCase);

    private static bool IsTest(string path)
    {
        var name = Path.GetFileName(path);
        return name.Contains(".test.", StringComparison.OrdinalIgnoreCase) || name.Contains(".spec.", StringComparison.OrdinalIgnoreCase);
    }

    [GeneratedRegex(@"(?:\bfrom\s+|\bimport\s*\(?\s*|\brequire\s*\(\s*)['""](?<spec>[^'""\r\n]+)['""]")]
    private static partial Regex Import();
}
