using System.Text.Json;
using System.Text.RegularExpressions;
using Mariliz.Application.Ports;
using Mariliz.Core.Discovery;
using Mariliz.Core.Text;

namespace Mariliz.Application.Discovery;

// MV-4: PHP, Ruby, C/C++ (CMake) e Swift (SwiftPM), pelos manifestos e pelo arquivo que fixa a versão.
internal static partial class LanguageDetectors
{
    // ---------- PHP ----------

    // A plataforma do Composer (config.platform.php) é a versão que o projeto diz rodar; require.php é só o mínimo aceito.
    private static void Php(IProjectFiles files, IReadOnlyList<string> all, List<StackFinding> stacks, List<ToolingFinding> tooling,
        List<FrameworkFinding> frameworks, List<DiscoveryWarning> warnings)
    {
        foreach (var group in ByRoot(all, name => name == "composer.json"))
        {
            var root = group.Key;
            var manifest = group.First();
            if (files.ReadText(manifest) is not { } text) continue;
            var evidence = new List<Evidence> { new(manifest) };
            var deps = new Dictionary<string, string>(StringComparer.Ordinal);
            (string Value, Confidence Confidence)? version = null;
            try
            {
                using var doc = JsonDocument.Parse(text, new JsonDocumentOptions { AllowTrailingCommas = true });
                foreach (var section in new[] { "require", "require-dev" })
                    if (doc.RootElement.TryGetProperty(section, out var require) && require.ValueKind == JsonValueKind.Object)
                        foreach (var dependency in require.EnumerateObject())
                            deps.TryAdd(dependency.Name, dependency.Value.ValueKind == JsonValueKind.String ? dependency.Value.GetString()! : "");
                if (doc.RootElement.TryGetProperty("config", out var config) && config.ValueKind == JsonValueKind.Object
                    && config.TryGetProperty("platform", out var platform) && platform.ValueKind == JsonValueKind.Object
                    && platform.TryGetProperty("php", out var pinned) && pinned.ValueKind == JsonValueKind.String
                    && Frameworks.VersionOf(pinned.GetString()!) is { } platformVersion)
                {
                    version = (platformVersion, Confidence.High);
                    evidence.Add(new Evidence(manifest, $"config.platform.php={pinned.GetString()}"));
                }
            }
            catch (JsonException)
            {
                warnings.Add(new(DiscoveryWarningCodes.UnknownVersion, $"{manifest} não é um JSON válido.", [manifest]));
                continue;
            }

            var phpVersionFile = ProjectPath.Combine(root, ".php-version");
            if (version is null && all.Contains(phpVersionFile) && files.ReadText(phpVersionFile) is { } fixedText
                && Frameworks.VersionOf(fixedText.Trim()) is { } fixedVersion)
            {
                version = (fixedVersion, Confidence.High);
                evidence.Add(new Evidence(phpVersionFile, $"versão fixada={fixedText.Trim()}"));
            }
            if (version is null && deps.TryGetValue("php", out var range) && Frameworks.VersionOf(range) is { } minimum)
            {
                version = (minimum, Confidence.Medium);
                evidence.Add(new Evidence(manifest, $"require.php={range} (mínimo aceito)"));
            }
            if (version is null) Unknown(warnings, "PHP", root, [manifest]);

            stacks.Add(new StackFinding(Technologies.Php.Id, Technologies.Php.Name, root, version?.Value, version?.Confidence ?? Confidence.Low, evidence,
                ["Dependências com Composer."]));
            Frameworks.Add(frameworks, Frameworks.Php, Technologies.Php.Id, root, manifest, deps);
            var at = all.Contains(ProjectPath.Combine(root, "composer.lock")) ? ProjectPath.Combine(root, "composer.lock") : manifest;
            tooling.Add(new ToolingFinding("package-manager", "composer", null, [new Evidence(at)]));
            if (deps.ContainsKey("phpunit/phpunit") || deps.ContainsKey("pestphp/pest"))
            {
                var runner = deps.ContainsKey("pestphp/pest") ? "pest" : "phpunit";
                tooling.Add(new ToolingFinding("test", runner, root.Length == 0 ? $"vendor/bin/{runner}" : $"cd {root} && vendor/bin/{runner}", [new Evidence(manifest)]));
            }
        }
    }

    // ---------- Ruby ----------

    // A versão vem do .ruby-version, da linha ruby do Gemfile ou do RUBY VERSION do Gemfile.lock; as gems, do lock quando existe.
    private static void Ruby(IProjectFiles files, IReadOnlyList<string> all, List<StackFinding> stacks, List<ToolingFinding> tooling,
        List<FrameworkFinding> frameworks, List<DiscoveryWarning> warnings)
    {
        foreach (var group in ByRoot(all, name => name == "Gemfile"))
        {
            var root = group.Key;
            var gemfile = group.First();
            if (files.ReadText(gemfile) is not { } text) continue;
            var evidence = new List<Evidence> { new(gemfile) };
            var deps = new Dictionary<string, string>(StringComparer.Ordinal);
            (string Value, Confidence Confidence)? version = null;

            var rubyVersionFile = ProjectPath.Combine(root, ".ruby-version");
            if (all.Contains(rubyVersionFile) && files.ReadText(rubyVersionFile) is { } fixedText && Frameworks.VersionOf(fixedText.Trim()) is { } fixedVersion)
            {
                version = (fixedVersion, Confidence.High);
                evidence.Add(new Evidence(rubyVersionFile, $"versão fixada={fixedText.Trim()}"));
            }
            if (version is null && GemfileRuby().Match(text) is { Success: true } declared && Frameworks.VersionOf(declared.Groups[1].Value) is { } gemfileVersion)
            {
                version = (gemfileVersion, Confidence.Medium);
                evidence.Add(new Evidence(gemfile, $"ruby {declared.Groups[1].Value}"));
            }
            foreach (Match gem in GemDeclaration().Matches(text)) deps.TryAdd(gem.Groups[1].Value, gem.Groups[2].Success ? gem.Groups[2].Value : "");

            var lockPath = ProjectPath.Combine(root, "Gemfile.lock");
            var depsManifest = gemfile;
            if (all.Contains(lockPath) && files.ReadText(lockPath) is { } lockText)
            {
                foreach (Match locked in LockedGem().Matches(lockText))
                    if (deps.ContainsKey(locked.Groups[1].Value) || Frameworks.Ruby.ContainsKey(locked.Groups[1].Value))
                        deps[locked.Groups[1].Value] = locked.Groups[2].Value;
                depsManifest = lockPath;
                if (version is null && LockedRuby().Match(lockText) is { Success: true } lockedRuby)
                {
                    version = (lockedRuby.Groups[1].Value, Confidence.High);
                    evidence.Add(new Evidence(lockPath, $"RUBY VERSION ruby {lockedRuby.Groups[1].Value}"));
                }
            }
            if (version is null) Unknown(warnings, "Ruby", root, [gemfile]);

            stacks.Add(new StackFinding(Technologies.Ruby.Id, Technologies.Ruby.Name, root, version?.Value, version?.Confidence ?? Confidence.Low, evidence,
                ["Dependências com Bundler."]));
            Frameworks.Add(frameworks, Frameworks.Ruby, Technologies.Ruby.Id, root, depsManifest, deps);
            tooling.Add(new ToolingFinding("package-manager", "bundler", null, [new Evidence(depsManifest)]));
            if (deps.ContainsKey("rspec") || deps.ContainsKey("rspec-core") || deps.ContainsKey("rspec-rails"))
                tooling.Add(new ToolingFinding("test", "rspec", root.Length == 0 ? "bundle exec rspec" : $"cd {root} && bundle exec rspec", [new Evidence(gemfile)]));
        }
    }

    // ---------- C/C++ (CMake) ----------

    // A versão é o padrão da linguagem que o projeto exige (CMAKE_CXX_STANDARD ou cxx_std_N); o CMake mínimo vai como nota.
    private static void Cpp(IProjectFiles files, IReadOnlyList<string> all, List<StackFinding> stacks, List<DiscoveryWarning> warnings)
    {
        foreach (var group in ByRoot(all, name => name == "CMakeLists.txt"))
        {
            var root = group.Key;
            // Um CMakeLists.txt dentro de outro projeto CMake é subdiretório dele, não projeto à parte.
            if (stacks.Any(stack => stack.Id == Technologies.Cpp.Id && root.StartsWith(stack.Root.Length == 0 ? "" : stack.Root + "/", StringComparison.Ordinal) && stack.Root != root))
                continue;
            var manifest = group.First();
            if (files.ReadText(manifest) is not { } text) continue;
            var evidence = new List<Evidence> { new(manifest) };
            var notes = new List<string>();
            string? version = null;
            if (CxxStandard().Match(text) is { Success: true } cxx)
            {
                version = $"C++{cxx.Groups["std"].Value}";
                evidence.Add(new Evidence(manifest, cxx.Value.Trim()));
            }
            else if (CStandard().Match(text) is { Success: true } c)
            {
                version = $"C{c.Groups[1].Value}";
                evidence.Add(new Evidence(manifest, c.Value.Trim()));
            }
            if (CmakeMinimum().Match(text) is { Success: true } cmake) notes.Add($"CMake {cmake.Groups[1].Value} ou mais novo.");
            if (version is null) Unknown(warnings, "C/C++", root, [manifest]);
            stacks.Add(new StackFinding(Technologies.Cpp.Id, Technologies.Cpp.Name, root, version, version is null ? Confidence.Low : Confidence.High, evidence, notes));
        }
    }

    // ---------- Swift (SwiftPM) ----------

    private static void Swift(IProjectFiles files, IReadOnlyList<string> all, List<StackFinding> stacks, List<ToolingFinding> tooling, List<DiscoveryWarning> warnings)
    {
        foreach (var group in ByRoot(all, name => name == "Package.swift"))
        {
            var root = group.Key;
            var manifest = group.First();
            if (files.ReadText(manifest) is not { } text) continue;
            var evidence = new List<Evidence> { new(manifest) };
            string? version = null;
            if (SwiftTools().Match(text) is { Success: true } tools)
            {
                version = tools.Groups[1].Value;
                evidence.Add(new Evidence(manifest, $"swift-tools-version:{version}"));
            }
            else Unknown(warnings, "Swift", root, [manifest]);
            stacks.Add(new StackFinding(Technologies.Swift.Id, Technologies.Swift.Name, root, version, version is null ? Confidence.Low : Confidence.High, evidence,
                ["Versão das ferramentas do Swift Package Manager: o mínimo do compilador."]));
            tooling.Add(new ToolingFinding("test", "swift test", root.Length == 0 ? "swift test" : $"swift test --package-path {root}", [new Evidence(manifest)]));
        }
    }

    [GeneratedRegex(@"(?m)^\s*ruby\s+[""']([^""']+)[""']")]
    private static partial Regex GemfileRuby();

    [GeneratedRegex(@"(?m)^\s*gem\s+[""']([\w\-]+)[""'](?:\s*,\s*[""']([^""']+)[""'])?")]
    private static partial Regex GemDeclaration();

    [GeneratedRegex(@"(?m)^    ([\w\-]+) \(([\d.]+)\)")]
    private static partial Regex LockedGem();

    [GeneratedRegex(@"RUBY VERSION\s+ruby (\d+\.\d+\.\d+)")]
    private static partial Regex LockedRuby();

    [GeneratedRegex(@"set\s*\(\s*CMAKE_CXX_STANDARD\s+(?<std>\d+)\s*\)|cxx_std_(?<std>\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex CxxStandard();

    [GeneratedRegex(@"set\s*\(\s*CMAKE_C_STANDARD\s+(\d+)\s*\)", RegexOptions.IgnoreCase)]
    private static partial Regex CStandard();

    [GeneratedRegex(@"cmake_minimum_required\s*\(\s*VERSION\s+([\d.]+)", RegexOptions.IgnoreCase)]
    private static partial Regex CmakeMinimum();

    [GeneratedRegex(@"^//\s*swift-tools-version\s*:\s*(\d+\.\d+(?:\.\d+)?)")]
    private static partial Regex SwiftTools();
}
