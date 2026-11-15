using System.Text.RegularExpressions;
using Mariliz.Application.Ports;
using Mariliz.Core.Discovery;
using Mariliz.Core.Text;

namespace Mariliz.Application.Discovery;

// Python, Rust, Go e Java pelos manifestos, como os outros detectores: nunca pelo nome da pasta. Os manifestos TOML e XML
// são lidos por padrão de texto, o bastante para versão e nomes de dependência; o que não dá para ler fica sem versão e com aviso.
internal static partial class LanguageDetectors
{
    private const int MaxDepth = 3;

    internal static void Detect(IProjectFiles files, IReadOnlyList<string> all, List<StackFinding> stacks, List<ToolingFinding> tooling,
        List<FrameworkFinding> frameworks, List<DiscoveryWarning> warnings)
    {
        Python(files, all, stacks, tooling, frameworks, warnings);
        Rust(files, all, stacks, tooling, frameworks, warnings);
        Go(files, all, stacks, tooling, frameworks, warnings);
        Java(files, all, stacks, tooling, frameworks, warnings);
    }

    private static IEnumerable<IGrouping<string, string>> ByRoot(IReadOnlyList<string> all, Func<string, bool> isManifest) =>
        all.Where(path => ProjectPath.Depth(path) <= MaxDepth && isManifest(Path.GetFileName(path)))
            .OrderBy(path => path, StringComparer.Ordinal)
            .GroupBy(ProjectPath.Parent)
            .OrderBy(group => group.Key, StringComparer.Ordinal);

    private static string Where(string root) => root.Length == 0 ? "." : root;

    private static void Unknown(List<DiscoveryWarning> warnings, string name, string root, IReadOnlyList<string> manifests) =>
        warnings.Add(new(DiscoveryWarningCodes.UnknownVersion, $"A versão de {name} em {Where(root)} não está declarada.", manifests));

    // ---------- Python ----------

    private static bool IsPythonManifest(string name) =>
        name is "pyproject.toml" or "setup.py" or "Pipfile" or ".python-version"
        || (name.StartsWith("requirements", StringComparison.Ordinal) && Path.GetExtension(name) is ".txt" or ".in" or ".lock");

    private static void Python(IProjectFiles files, IReadOnlyList<string> all, List<StackFinding> stacks, List<ToolingFinding> tooling,
        List<FrameworkFinding> frameworks, List<DiscoveryWarning> warnings)
    {
        foreach (var group in ByRoot(all, IsPythonManifest))
        {
            var root = group.Key;
            var manifests = group.ToList();
            // .python-version sozinho não é projeto Python: só diz a versão de um que já se declarou.
            if (manifests.All(path => Path.GetFileName(path) == ".python-version")) continue;

            var deps = new Dictionary<string, string>(StringComparer.Ordinal);
            var evidence = new List<Evidence>();
            (string Value, Evidence Source, Confidence Confidence)? version = null;
            var ruff = false;
            string? depsManifest = null;

            foreach (var manifest in manifests)
            {
                var text = files.ReadText(manifest);
                if (text is null) continue;
                var name = Path.GetFileName(manifest);
                evidence.Add(new Evidence(manifest));
                var before = deps.Count;

                if (name == ".python-version" && PythonVersion().Match(text) is { Success: true } pinned)
                    version = (pinned.Value, new Evidence(manifest, $"versão fixada={pinned.Value}"), Confidence.High);
                else if (name == "pyproject.toml")
                {
                    if (version is null && RequiresPython().Match(text) is { Success: true } requires && PythonVersion().Match(requires.Groups[1].Value) is { Success: true } declared)
                        version = (declared.Value, new Evidence(manifest, $"requires-python={requires.Groups[1].Value}"), Confidence.Medium);
                    if (version is null && RuffTarget().Match(text) is { Success: true } target)
                        version = ($"{target.Groups[1].Value}.{target.Groups[2].Value}", new Evidence(manifest, $"tool.ruff.target-version=py{target.Groups[1].Value}{target.Groups[2].Value}"), Confidence.Medium);
                    ruff |= text.Contains("[tool.ruff", StringComparison.Ordinal);
                    if (ProjectDependencies().Match(text) is { Success: true } list)
                        foreach (Match quoted in Quoted().Matches(list.Groups[1].Value)) AddRequirement(deps, quoted.Groups[1].Value);
                    foreach (var (key, value) in TomlKeys(Section(text, "tool.poetry.dependencies")))
                    {
                        if (key == "python") version ??= (PythonVersion().Match(value).Value, new Evidence(manifest, $"tool.poetry.dependencies.python={value}"), Confidence.Medium);
                        else deps.TryAdd(Normalize(key), value.Trim('"', '\''));
                    }
                }
                else if (name == "Pipfile")
                {
                    foreach (var section in new[] { "packages", "dev-packages" })
                        foreach (var (key, value) in TomlKeys(Section(text, section))) deps.TryAdd(Normalize(key), value.Trim('"', '\''));
                    if (version is null && PipfilePython().Match(text) is { Success: true } pipfile)
                        version = (pipfile.Groups[1].Value, new Evidence(manifest, $"python_version={pipfile.Groups[1].Value}"), Confidence.Medium);
                }
                else if (name != "setup.py" && name != ".python-version")
                {
                    foreach (var line in text.Split('\n')) AddRequirement(deps, line);
                }

                if (deps.Count > before) depsManifest ??= manifest;
            }

            if (version is { Value.Length: 0 }) version = null;
            if (version is { } found) evidence.Add(found.Source);
            else Unknown(warnings, "Python", root, manifests);

            stacks.Add(new StackFinding("python", "Python", root, version?.Value, version?.Confidence ?? Confidence.Low, evidence,
                version is null ? [] : ["Versão lida do que o projeto declara; o interpretador instalado pode ser outro."]));
            Frameworks.Add(frameworks, Frameworks.Python, "python", root, depsManifest ?? manifests[0], deps);

            var at = new Evidence(depsManifest ?? manifests[0]);
            if (deps.ContainsKey("pytest")) tooling.Add(new ToolingFinding("test", "pytest", root.Length == 0 ? "python -m pytest" : $"python -m pytest {root}", [at]));
            if (ruff || deps.ContainsKey("ruff")) tooling.Add(new ToolingFinding("lint", "ruff", root.Length == 0 ? "ruff check ." : $"ruff check {root}", [at]));
        }
    }

    private static void AddRequirement(Dictionary<string, string> deps, string line)
    {
        var text = line.Split('#')[0].Trim();
        if (text.Length == 0 || text.StartsWith('-')) return;
        if (Requirement().Match(text) is not { Success: true } match) return;
        deps.TryAdd(Normalize(match.Groups[1].Value), match.Groups[2].Value.Split(';')[0].Trim().TrimEnd('\\').Trim());
    }

    private static string Normalize(string name) => name.ToLowerInvariant().Replace('_', '-').Replace('.', '-');

    // ---------- Rust ----------

    private static void Rust(IProjectFiles files, IReadOnlyList<string> all, List<StackFinding> stacks, List<ToolingFinding> tooling,
        List<FrameworkFinding> frameworks, List<DiscoveryWarning> warnings)
    {
        foreach (var group in ByRoot(all, name => name == "Cargo.toml"))
        {
            var manifest = group.First();
            var text = files.ReadText(manifest);
            if (text is null || !CargoTable().IsMatch(text)) continue;
            var root = group.Key;
            var evidence = new List<Evidence> { new(manifest) };
            var notes = new List<string>();

            string? version = null;
            if (TomlString(Section(text, "package"), "rust-version") is { } declared)
            {
                version = Frameworks.VersionOf(declared);
                evidence.Add(new Evidence(manifest, $"rust-version={declared}"));
            }
            else
            {
                foreach (var toolchain in new[] { "rust-toolchain.toml", "rust-toolchain" }.Select(name => ProjectPath.Combine(root, name)))
                {
                    if (files.ReadText(toolchain) is not { } channelText) continue;
                    var channel = TomlString(channelText, "channel") ?? channelText.Trim();
                    version = Frameworks.VersionOf(channel);
                    evidence.Add(new Evidence(toolchain, $"channel={channel}"));
                    break;
                }
            }
            if (TomlString(Section(text, "package"), "edition") is { } edition) notes.Add($"Edição {edition} da linguagem.");
            if (version is null) Unknown(warnings, "Rust", root, [manifest]);

            var deps = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var section in new[] { "dependencies", "dev-dependencies", "build-dependencies", "workspace.dependencies" })
                foreach (var (key, value) in TomlKeys(Section(text, section)))
                    deps.TryAdd(key, CargoVersion(value));

            stacks.Add(new StackFinding("rust", "Rust", root, version, version is null ? Confidence.Low : Confidence.Medium, evidence, notes));
            Frameworks.Add(frameworks, Frameworks.Rust, "rust", root, manifest, deps);

            var path = root.Length == 0 ? "" : $" --manifest-path {manifest}";
            tooling.Add(new ToolingFinding("build", "cargo build", $"cargo build{path}", [new Evidence(manifest)]));
            tooling.Add(new ToolingFinding("test", "cargo test", $"cargo test{path}", [new Evidence(manifest)]));
            tooling.Add(new ToolingFinding("lint", "cargo clippy", $"cargo clippy{path}", [new Evidence(manifest)]));
        }
    }

    private static string CargoVersion(string value)
    {
        var text = value.Trim();
        if (text.StartsWith('"')) return text.Trim('"');
        return TomlInlineVersion().Match(text) is { Success: true } inline ? inline.Groups[1].Value : "";
    }

    // ---------- Go ----------

    private static void Go(IProjectFiles files, IReadOnlyList<string> all, List<StackFinding> stacks, List<ToolingFinding> tooling,
        List<FrameworkFinding> frameworks, List<DiscoveryWarning> warnings)
    {
        foreach (var group in ByRoot(all, name => name == "go.mod"))
        {
            var manifest = group.First();
            if (files.ReadText(manifest) is not { } text) continue;
            var root = group.Key;
            var evidence = new List<Evidence> { new(manifest) };

            var version = GoDirective().Match(text) is { Success: true } directive ? directive.Groups[1].Value : null;
            if (version is null) Unknown(warnings, "Go", root, [manifest]);
            else evidence.Add(new Evidence(manifest, $"go {version}"));

            var deps = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (Match require in GoRequire().Matches(text)) deps.TryAdd(require.Groups[1].Value, require.Groups[2].Value);

            stacks.Add(new StackFinding("go", "Go", root, version, version is null ? Confidence.Low : Confidence.High, evidence, []));
            Frameworks.Add(frameworks, Frameworks.Go, "go", root, manifest, deps, byPrefix: true);

            var at = root.Length == 0 ? "go" : $"go -C {root}";
            tooling.Add(new ToolingFinding("build", "go build", $"{at} build ./...", [new Evidence(manifest)]));
            tooling.Add(new ToolingFinding("test", "go test", $"{at} test ./...", [new Evidence(manifest)]));
            tooling.Add(new ToolingFinding("lint", "go vet", $"{at} vet ./...", [new Evidence(manifest)]));
        }
    }

    // ---------- Java ----------

    private static void Java(IProjectFiles files, IReadOnlyList<string> all, List<StackFinding> stacks, List<ToolingFinding> tooling,
        List<FrameworkFinding> frameworks, List<DiscoveryWarning> warnings)
    {
        foreach (var group in ByRoot(all, name => name is "pom.xml" or "build.gradle" or "build.gradle.kts"))
        {
            var root = group.Key;
            var manifest = group.First();
            if (files.ReadText(manifest) is not { } text) continue;
            var maven = Path.GetFileName(manifest) == "pom.xml";
            var evidence = new List<Evidence> { new(manifest) };
            var deps = new Dictionary<string, string>(StringComparer.Ordinal);
            string? version = null;

            if (maven)
            {
                if (MavenJava().Match(text) is { Success: true } property)
                {
                    version = property.Groups[2].Value;
                    evidence.Add(new Evidence(manifest, $"{property.Groups[1].Value}={version}"));
                }
                foreach (Match artifact in MavenArtifact().Matches(text)) deps.TryAdd(artifact.Groups[1].Value, artifact.Groups[2].Value);
            }
            else
            {
                if (GradleJava().Match(text) is { Success: true } declared)
                {
                    version = declared.Groups[1].Success && declared.Groups[1].Value.Length > 0 ? declared.Groups[1].Value : declared.Groups[2].Value.Replace('_', '.');
                    evidence.Add(new Evidence(manifest, declared.Value.Trim()));
                }
                foreach (Match coordinate in GradleCoordinate().Matches(text)) deps.TryAdd(coordinate.Groups[1].Value, coordinate.Groups[2].Value);
                if (text.Contains("org.springframework.boot", StringComparison.Ordinal)) deps.TryAdd("spring-boot", "");
            }
            if (version is null) Unknown(warnings, "Java", root, [manifest]);

            stacks.Add(new StackFinding("java", "Java", root, version, version is null ? Confidence.Low : Confidence.Medium, evidence,
                [maven ? "Build com Maven." : "Build com Gradle."]));
            Frameworks.Add(frameworks, Frameworks.Java, "java", root, manifest, deps, byPrefix: true);

            var command = maven
                ? $"{(all.Contains(ProjectPath.Combine(root, "mvnw")) ? "./mvnw" : "mvn")} test{(root.Length == 0 ? "" : $" -f {manifest}")}"
                : $"{(all.Contains(ProjectPath.Combine(root, "gradlew")) ? "./gradlew" : "gradle")} test{(root.Length == 0 ? "" : $" -p {root}")}";
            tooling.Add(new ToolingFinding("test", maven ? "maven test" : "gradle test", command, [new Evidence(manifest)]));
        }
    }

    // ---------- TOML mínimo ----------

    private static string Section(string text, string header)
    {
        var start = text.IndexOf($"[{header}]", StringComparison.Ordinal);
        if (start < 0) return "";
        var body = text[(start + header.Length + 2)..];
        var next = NextSection().Match(body);
        return next.Success ? body[..next.Index] : body;
    }

    private static IEnumerable<(string Key, string Value)> TomlKeys(string section)
    {
        foreach (var line in section.Split('\n'))
            if (TomlKey().Match(line) is { Success: true } match)
                yield return (match.Groups[1].Value, match.Groups[2].Value.Trim());
    }

    private static string? TomlString(string text, string key) =>
        Regex.Match(text, $@"(?m)^\s*{Regex.Escape(key)}\s*=\s*[""']([^""']+)[""']") is { Success: true } match ? match.Groups[1].Value : null;

    [GeneratedRegex(@"\d+\.\d+(\.\d+)?")]
    private static partial Regex PythonVersion();

    [GeneratedRegex(@"(?m)^\s*requires-python\s*=\s*[""']([^""']+)[""']")]
    private static partial Regex RequiresPython();

    [GeneratedRegex(@"(?m)^\s*target-version\s*=\s*[""']py(\d)(\d{1,2})[""']")]
    private static partial Regex RuffTarget();

    // Extras como "SQLAlchemy[asyncio]" têm colchete dentro da lista: o fim é o primeiro colchete que não fecha um desses.
    [GeneratedRegex(@"(?m)^\s*dependencies\s*=\s*\[((?:[^\[\]]|\[[^\]]*\])*)\]")]
    private static partial Regex ProjectDependencies();

    [GeneratedRegex(@"[""']([^""']+)[""']")]
    private static partial Regex Quoted();

    [GeneratedRegex(@"(?m)^\s*python_version\s*=\s*[""'](\d+\.\d+)[""']")]
    private static partial Regex PipfilePython();

    [GeneratedRegex(@"^([A-Za-z0-9][A-Za-z0-9._-]*)(?:\[[^\]]*\])?\s*(.*)$")]
    private static partial Regex Requirement();

    [GeneratedRegex(@"version\s*=\s*""([^""]+)""")]
    private static partial Regex TomlInlineVersion();

    [GeneratedRegex(@"(?m)^\s*go\s+(\d+\.\d+(\.\d+)?)\s*$")]
    private static partial Regex GoDirective();

    [GeneratedRegex(@"(?m)^\s*(?:require\s+)?([a-z0-9.\-]+\.[a-z]{2,}/[^\s]+)\s+(v[\w.\-+]+)")]
    private static partial Regex GoRequire();

    [GeneratedRegex(@"<(maven\.compiler\.release|maven\.compiler\.source|java\.version|release)>\s*(\d+(\.\d+)?)\s*</")]
    private static partial Regex MavenJava();

    [GeneratedRegex(@"<artifactId>\s*([\w.\-]+)\s*</artifactId>\s*(?:<version>\s*([^<]*)</version>)?")]
    private static partial Regex MavenArtifact();

    [GeneratedRegex(@"languageVersion(?:\.set)?\s*\(?\s*JavaLanguageVersion\.of\(\s*(\d+)\s*\)|JavaVersion\.VERSION_(\d+(?:_\d+)?)")]
    private static partial Regex GradleJava();

    [GeneratedRegex(@"[""'][\w.\-]+:([\w.\-]+)(?::([\w.\-]+))?[""']")]
    private static partial Regex GradleCoordinate();

    [GeneratedRegex(@"(?m)^\s*\[")]
    private static partial Regex NextSection();

    [GeneratedRegex(@"(?m)^\s*\[(package|workspace)\]")]
    private static partial Regex CargoTable();

    [GeneratedRegex(@"^\s*([A-Za-z0-9_\-]+)(?:\.[A-Za-z]+)?\s*=\s*(.+?)\s*$")]
    private static partial Regex TomlKey();
}
