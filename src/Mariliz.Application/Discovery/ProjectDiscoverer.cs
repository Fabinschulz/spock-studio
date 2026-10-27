using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Mariliz.Application.Harness;
using Mariliz.Application.Ports;
using Mariliz.Core.Discovery;
using Mariliz.Core.Policy;
using YamlDotNet.Serialization;

namespace Mariliz.Application.Discovery;

/// <summary>
/// Análise somente de leitura: stack, ferramentas, ambiente de IA e Harness existente.
/// Cada conclusão carrega a evidência (arquivo e valor lido). Versão desconhecida é dita como tal.
/// </summary>
public sealed class ProjectDiscoverer(IProcessRunner processRunner)
{
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(10);

    public async Task<DiscoveryReport> DiscoverAsync(
        string projectId, string rootPath, IProjectFiles files, CancellationToken ct)
    {
        var all = files.EnumerateFiles("", 6);
        var warnings = new List<DiscoveryWarning>();

        var lockFile = ManagedLockFile.Read(files);
        var managed = lockFile?.ManagedFiles.Select(m => m.Path).ToHashSet(StringComparer.Ordinal) ?? [];

        var stacks = new List<StackFinding>();
        var tooling = new List<ToolingFinding>();
        DetectDotnet(files, all, stacks, tooling, warnings);
        DetectNode(files, all, stacks, tooling, warnings);
        DetectFlutter(files, all, stacks, tooling, warnings);
        DetectCi(all, tooling);
        if (stacks.Count == 0)
            warnings.Add(new(DiscoveryWarningCodes.NoStackDetected, "Nenhuma stack conhecida foi detectada pelos arquivos de configuração.", []));

        var aiEnvironments = DetectAiEnvironments(files, all, warnings);
        var inventory = BuildInventory(files, all, managed, warnings);

        var tlc = TlcInspector.Inspect(files);
        if (tlc.State == TlcState.Drifted)
            warnings.Add(new(DiscoveryWarningCodes.TlcDrift, "A instalação do TLC diverge do que o instalador registrou.", [TlcManifest.SkillDirectory]));
        if (tlc.State is TlcState.Installed or TlcState.Drifted && !tlc.VersionTested)
            warnings.Add(new(DiscoveryWarningCodes.TlcUntestedVersion,
                $"A versão {tlc.Version ?? "desconhecida"} do TLC não foi validada por esta fase (validada: {string.Join(", ", TlcManifest.TestedVersions)}).", []));

        var tools = await ProbeToolsAsync(rootPath, ct);
        if (tools.Python is null)
            warnings.Add(new(DiscoveryWarningCodes.PythonMissing, "Python 3 não foi encontrado; os gates do TLC e o hook de política dependem dele.", []));
        if (tools.Node is null)
            warnings.Add(new(DiscoveryWarningCodes.NodeMissing, "Node.js não foi encontrado; o instalador oficial do TLC depende de npx (Node 22 ou superior).", []));

        var git = await ReadGitStateAsync(rootPath, files, tools, ct);
        if (!git.IsRepository)
            warnings.Add(new(DiscoveryWarningCodes.NotAGitRepository, "A pasta não é um repositório git; não há como reverter por controle de versão.", []));
        else if (git.HasUncommittedChanges == true)
            warnings.Add(new(DiscoveryWarningCodes.UncommittedChanges, "Há alterações locais ainda não commitadas.", git.ChangedPaths));

        return new DiscoveryReport(
            projectId, files.RootName, stacks, tooling, aiEnvironments, inventory, tlc, git, tools,
            files.FileExists(DeclaredConfig.FileName), warnings);
    }

    // ---------------------------------------------------------------- .NET

    private static void DetectDotnet(IProjectFiles files, IReadOnlyList<string> all,
        List<StackFinding> stacks, List<ToolingFinding> tooling, List<DiscoveryWarning> warnings)
    {
        var solutions = all.Where(p => p.EndsWith(".sln", StringComparison.OrdinalIgnoreCase) || p.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase)).OrderBy(Depth).ThenBy(p => p, StringComparer.Ordinal).ToList();
        var projects = all.Where(p => p.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)).OrderBy(p => p, StringComparer.Ordinal).ToList();
        if (solutions.Count == 0 && projects.Count == 0) return;

        var evidence = new List<Evidence>();
        var frameworks = new List<string>();
        var notes = new List<string>();
        var testProjects = 0;
        var web = false;

        foreach (var csproj in projects)
        {
            var text = files.ReadText(csproj);
            if (text is null) continue;
            try
            {
                var xml = XDocument.Parse(text);
                var tfm = xml.Descendants().FirstOrDefault(e => e.Name.LocalName == "TargetFramework")?.Value
                          ?? xml.Descendants().FirstOrDefault(e => e.Name.LocalName == "TargetFrameworks")?.Value;
                if (!string.IsNullOrWhiteSpace(tfm))
                {
                    frameworks.AddRange(tfm.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
                    if (evidence.Count < 12) evidence.Add(new Evidence(csproj, $"TargetFramework={tfm}"));
                }
                var sdk = xml.Root?.Attribute("Sdk")?.Value;
                if (sdk is not null && sdk.Contains("Web", StringComparison.OrdinalIgnoreCase)) web = true;
                var refs = xml.Descendants().Where(e => e.Name.LocalName == "PackageReference").Select(e => e.Attribute("Include")?.Value ?? "").ToList();
                if (refs.Any(r => r.StartsWith("xunit", StringComparison.OrdinalIgnoreCase) || r.StartsWith("NUnit", StringComparison.OrdinalIgnoreCase) || r.StartsWith("MSTest", StringComparison.OrdinalIgnoreCase)))
                    testProjects++;
            }
            catch (System.Xml.XmlException)
            {
                warnings.Add(new(DiscoveryWarningCodes.UnknownVersion, $"Não foi possível ler {csproj}.", [csproj]));
            }
        }

        foreach (var sln in solutions.Take(3)) evidence.Insert(0, new Evidence(sln));

        var version = frameworks.Count == 0 ? null : frameworks.OrderByDescending(f => f, StringComparer.OrdinalIgnoreCase).First();
        if (web) notes.Add("ASP.NET Core (Microsoft.NET.Sdk.Web)");
        if (testProjects > 0) notes.Add($"{testProjects} projeto(s) de teste");
        if (version is null)
            warnings.Add(new(DiscoveryWarningCodes.UnknownVersion, "A versão do .NET não pôde ser determinada pelos arquivos de projeto.", projects.Take(3).ToList()));

        var root = solutions.Count > 0 ? Directory(solutions[0]) : Directory(projects[0]);
        stacks.Add(new StackFinding("dotnet", ".NET", root, version, version is null ? Confidence.Medium : Confidence.High, evidence, notes));

        var target = solutions.Count > 0 ? solutions[0] : projects.Count == 1 ? projects[0] : null;
        var arg = target is null ? "" : " " + target;
        var ev = target is null ? evidence.Take(1).ToList() : [new Evidence(target)];
        tooling.Add(new ToolingFinding("build", "dotnet build", $"dotnet build{arg}", ev));
        if (testProjects > 0) tooling.Add(new ToolingFinding("test", "dotnet test", $"dotnet test{arg}", ev));
    }

    // ---------------------------------------------------------------- Node

    private static void DetectNode(IProjectFiles files, IReadOnlyList<string> all,
        List<StackFinding> stacks, List<ToolingFinding> tooling, List<DiscoveryWarning> warnings)
    {
        var manifests = all.Where(p => Path.GetFileName(p) == "package.json" && Depth(p) <= 3).OrderBy(p => p, StringComparer.Ordinal).ToList();
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
                var root = Directory(manifest);
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
                    var installed = files.ReadText(Combine(root, $"node_modules/{depName}/package.json"));
                    version = ReadVersionField(installed);
                    if (version is not null)
                    {
                        confidence = Confidence.High;
                        evidence.Add(new Evidence(Combine(root, $"node_modules/{depName}/package.json"), $"version={version}"));
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

                if (deps.ContainsKey("typescript") || all.Contains(Combine(root, "tsconfig.json"))) notes.Add("TypeScript");
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
        var found = locks.Where(l => all.Contains(Combine(root, l.File))).ToList();
        string? declared = null;
        if (manifest.TryGetProperty("packageManager", out var pm) && pm.ValueKind == JsonValueKind.String)
            declared = pm.GetString()?.Split('@')[0];

        var managers = found.Select(f => f.Manager).Distinct().ToList();
        if (declared is not null && !managers.Contains(declared)) managers.Add(declared);
        if (found.Select(f => f.Manager).Distinct().Count() > 1 || (declared is not null && found.Count > 0 && found.All(f => f.Manager != declared)))
            warnings.Add(new(DiscoveryWarningCodes.ConflictingPackageManagers,
                $"Gerenciadores de pacotes divergentes em {(root == "" ? "." : root)}: {string.Join(", ", managers)}.",
                found.Select(f => Combine(root, f.File)).Append(manifestPath).ToList()));

        var manager = declared ?? found.FirstOrDefault().Manager ?? (managers.Count > 0 ? managers[0] : null);
        if (manager is not null)
        {
            var ev = found.Select(f => new Evidence(Combine(root, f.File))).ToList();
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
                tooling.Add(new ToolingFinding(kind, $"{runner} {kind}", command, [new Evidence(manifestPath, $"scripts.{kind}={Truncate(cmd.GetString() ?? "", 80)}")]));
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

    // ---------------------------------------------------------------- Flutter

    private static void DetectFlutter(IProjectFiles files, IReadOnlyList<string> all,
        List<StackFinding> stacks, List<ToolingFinding> tooling, List<DiscoveryWarning> warnings)
    {
        foreach (var pubspec in all.Where(p => Path.GetFileName(p) == "pubspec.yaml" && Depth(p) <= 3).OrderBy(p => p, StringComparer.Ordinal))
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
                var root = Directory(pubspec);
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

    private static void DetectCi(IReadOnlyList<string> all, List<ToolingFinding> tooling)
    {
        var workflows = all.Where(p => p.StartsWith(".github/workflows/", StringComparison.Ordinal) && (p.EndsWith(".yml") || p.EndsWith(".yaml"))).ToList();
        if (workflows.Count > 0) tooling.Add(new ToolingFinding("ci", "GitHub Actions", null, workflows.Take(8).Select(w => new Evidence(w)).ToList()));
        foreach (var (file, name) in new[] { ("azure-pipelines.yml", "Azure Pipelines"), (".gitlab-ci.yml", "GitLab CI"), ("Jenkinsfile", "Jenkins") })
            if (all.Contains(file)) tooling.Add(new ToolingFinding("ci", name, null, [new Evidence(file)]));
    }

    // ---------------------------------------------------------------- Ambiente de IA

    private static List<AiEnvironmentFinding> DetectAiEnvironments(IProjectFiles files, IReadOnlyList<string> all, List<DiscoveryWarning> warnings)
    {
        var result = new List<AiEnvironmentFinding>();

        var claude = new List<Evidence>();
        if (all.Any(p => p.StartsWith(".claude/", StringComparison.Ordinal))) claude.Add(new Evidence(".claude/"));
        foreach (var f in new[] { "CLAUDE.md", ".claude/CLAUDE.md", ".mcp.json" }) if (all.Contains(f)) claude.Add(new Evidence(f));
        if (claude.Count > 0)
        {
            var strong = claude.Any(e => e.Path == ".claude/");
            result.Add(new AiEnvironmentFinding(AiEnvironments.ClaudeCode, "Claude Code", true, strong ? Confidence.High : Confidence.Medium, claude));
        }

        var cursor = new List<Evidence>();
        if (all.Any(p => p.StartsWith(".cursor/", StringComparison.Ordinal))) cursor.Add(new Evidence(".cursor/"));
        if (all.Contains(".cursorrules")) cursor.Add(new Evidence(".cursorrules"));
        if (cursor.Count > 0) result.Add(new AiEnvironmentFinding(AiEnvironments.Cursor, "Cursor", false, Confidence.High, cursor));

        var codex = new List<Evidence>();
        if (all.Any(p => p.StartsWith(".codex/", StringComparison.Ordinal))) codex.Add(new Evidence(".codex/"));
        if (all.Contains("AGENTS.md")) codex.Add(new Evidence("AGENTS.md", "lido por várias ferramentas; evidência fraca"));
        if (codex.Count > 0)
            result.Add(new AiEnvironmentFinding(AiEnvironments.Codex, "OpenAI Codex", false, codex.Any(e => e.Path == ".codex/") ? Confidence.High : Confidence.Low, codex));

        if (result.Count == 0)
            warnings.Add(new(DiscoveryWarningCodes.NoAiEnvironment, "Nenhum ambiente de IA foi detectado; selecione o que o projeto usa.", []));
        else if (result.Count > 1)
            warnings.Add(new(DiscoveryWarningCodes.MultipleAiEnvironments, $"Mais de um ambiente de IA detectado: {string.Join(", ", result.Select(r => r.Name))}.", []));
        return result;
    }

    // ---------------------------------------------------------------- Inventário do Harness

    private static HarnessInventory BuildInventory(IProjectFiles files, IReadOnlyList<string> all,
        HashSet<string> managed, List<DiscoveryWarning> warnings)
    {
        var agents = all.Where(p => p.StartsWith(".claude/agents/", StringComparison.Ordinal) && p.EndsWith(".md"))
            .Select(p => new InventoryItem(Path.GetFileNameWithoutExtension(p), p, managed.Contains(p), FrontmatterIssue(files.ReadText(p), Path.GetFileNameWithoutExtension(p), requireTools: false))).ToList();

        var skills = all.Where(p => p.StartsWith(".claude/skills/", StringComparison.Ordinal) && p.EndsWith("/SKILL.md"))
            .Select(p => { var name = p.Split('/')[2]; return new InventoryItem(name, p, managed.Contains(p), FrontmatterIssue(files.ReadText(p), name, requireTools: false)); }).ToList();

        var rules = all.Where(p => p.StartsWith(".claude/rules/", StringComparison.Ordinal) && p.EndsWith(".md"))
            .Select(p => new InventoryItem(Path.GetFileNameWithoutExtension(p), p, managed.Contains(p), null)).ToList();
        foreach (var f in new[] { "CLAUDE.md", ".claude/CLAUDE.md" }) if (all.Contains(f)) rules.Add(new InventoryItem(f, f, managed.Contains(f), null));

        var hooks = new List<InventoryItem>();
        var settings = files.ReadText(".claude/settings.json");
        if (settings is not null)
        {
            try
            {
                using var doc = JsonDocument.Parse(settings);
                if (doc.RootElement.TryGetProperty("hooks", out var h) && h.ValueKind == JsonValueKind.Object)
                {
                    foreach (var ev in h.EnumerateObject())
                    foreach (var group in ev.Value.EnumerateArray())
                    {
                        var matcher = group.TryGetProperty("matcher", out var m) ? m.GetString() : null;
                        var count = group.TryGetProperty("hooks", out var hs) ? hs.GetArrayLength() : 0;
                        hooks.Add(new InventoryItem($"{ev.Name}{(string.IsNullOrEmpty(matcher) ? "" : ":" + matcher)} ({count})", ".claude/settings.json", false, null));
                    }
                }
            }
            catch (Exception ex) when (ex is JsonException or InvalidOperationException)
            {
                hooks.Add(new InventoryItem("settings.json", ".claude/settings.json", false, "JSON inválido ou fora do formato esperado."));
                warnings.Add(new(DiscoveryWarningCodes.InvalidConfig, ".claude/settings.json não é um JSON válido; a aplicação não o alterará.", [".claude/settings.json"]));
            }
        }
        foreach (var script in all.Where(p => p.StartsWith(".claude/hooks/", StringComparison.Ordinal)))
            hooks.Add(new InventoryItem(Path.GetFileName(script), script, managed.Contains(script), null));

        var mcp = new List<InventoryItem>();
        var mcpText = files.ReadText(".mcp.json");
        if (mcpText is not null)
        {
            try
            {
                using var doc = JsonDocument.Parse(mcpText);
                if (doc.RootElement.TryGetProperty("mcpServers", out var servers) && servers.ValueKind == JsonValueKind.Object)
                    foreach (var s in servers.EnumerateObject())
                        mcp.Add(new InventoryItem(s.Name, ".mcp.json", false, SecretScanner.ContainsSecret(s.Value.GetRawText()) ? "Possível segredo literal na configuração; use variáveis de ambiente." : null));
            }
            catch (JsonException)
            {
                mcp.Add(new InventoryItem(".mcp.json", ".mcp.json", false, "JSON inválido."));
            }
        }

        var evals = all.Where(p => p.StartsWith(".claude/evals/", StringComparison.Ordinal))
            .Select(p => new InventoryItem(Path.GetFileName(p), p, managed.Contains(p), null)).ToList();

        var known = new[] { ".claude/agents/", ".claude/skills/", ".claude/rules/", ".claude/hooks/", ".claude/evals/", ".claude/worktrees/", ".claude/rag/", ".claude/mcp/" };
        var knownFiles = new[] { ".claude/CLAUDE.md", ".claude/settings.json", ".claude/settings.local.json", ".claude/launch.json" };
        var unknown = all.Where(p => p.StartsWith(".claude/", StringComparison.Ordinal) && !known.Any(k => p.StartsWith(k, StringComparison.Ordinal)) && !knownFiles.Contains(p)).ToList();
        if (unknown.Count > 0)
            warnings.Add(new(DiscoveryWarningCodes.UnrecognizedHarnessFiles, "Arquivos em .claude/ que a aplicação não reconhece; serão preservados.", unknown.Take(10).ToList()));

        return new HarnessInventory(agents, skills, hooks, rules, mcp, evals);
    }

    public static string? FrontmatterIssue(string? markdown, string expectedName, bool requireTools)
    {
        if (markdown is null) return "Arquivo ilegível.";
        var fm = FrontmatterReader.Read(markdown);
        if (fm is null) return "Sem frontmatter.";
        if (!fm.TryGetValue("name", out var name) || string.IsNullOrWhiteSpace(name)) return "Frontmatter sem 'name'.";
        if (!string.Equals(name, expectedName, StringComparison.Ordinal)) return $"'name' ({name}) difere do nome do arquivo ({expectedName}).";
        if (!fm.TryGetValue("description", out var d) || string.IsNullOrWhiteSpace(d)) return "Frontmatter sem 'description'.";
        if (requireTools && !fm.ContainsKey("tools")) return "Frontmatter sem 'tools' explícito.";
        return null;
    }

    // ---------------------------------------------------------------- Sistema

    private async Task<ToolAvailability> ProbeToolsAsync(string root, CancellationToken ct)
    {
        async Task<string?> Version(string exe, string pattern)
        {
            var r = await processRunner.RunAsync(new ProcessSpec(exe, ["--version"], root, ProbeTimeout), ct);
            if (r.NotFound || r.TimedOut || r.ExitCode != 0) return null;
            var m = Regex.Match(r.StandardOutput + " " + r.StandardError, pattern);
            return m.Success ? m.Groups[1].Value : null;
        }

        var git = await Version("git", @"git version (\d+(?:\.\d+)+)");
        var node = await Version("node", @"v?(\d+\.\d+\.\d+)");
        string? python = null, pythonCommand = null;
        foreach (var candidate in new[] { "python3", "python" })
        {
            python = await Version(candidate, @"Python (3\.\d+\.\d+)");
            if (python is not null) { pythonCommand = candidate; break; }
        }
        return new ToolAvailability(git, node, python, pythonCommand);
    }

    private async Task<GitState> ReadGitStateAsync(string root, IProjectFiles files, ToolAvailability tools, CancellationToken ct)
    {
        if (tools.Git is null) return new GitState(files.DirectoryExists(".git"), null, []);

        var inside = await processRunner.RunAsync(new ProcessSpec("git", ["rev-parse", "--is-inside-work-tree"], root, ProbeTimeout), ct);
        if (inside.NotFound || inside.ExitCode != 0 || !inside.StandardOutput.Trim().Equals("true", StringComparison.OrdinalIgnoreCase))
            return new GitState(false, null, []);

        var status = await processRunner.RunAsync(new ProcessSpec("git", ["status", "--porcelain", "--", "."], root, ProbeTimeout), ct);
        if (status.ExitCode != 0) return new GitState(true, null, []);

        var changed = status.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Length > 3 ? l[3..].Trim().Trim('"') : l.Trim()).ToList();
        return new GitState(true, changed.Count > 0, changed.Take(50).ToList());
    }

    // ---------------------------------------------------------------- Utilidades

    private static int Depth(string path) => path.Count(c => c == '/');
    private static string Directory(string path) { var i = path.LastIndexOf('/'); return i < 0 ? "" : path[..i]; }
    private static string Combine(string root, string path) => root.Length == 0 ? path : root + "/" + path;
    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max] + "…";
}

public static class AiEnvironments
{
    public const string ClaudeCode = "claude-code";
    public const string Cursor = "cursor";
    public const string Codex = "codex";

    /// <summary>Únicos ambientes em que esta fase escreve configuração. Os demais são só detectados.</summary>
    public static readonly IReadOnlyList<string> Supported = [ClaudeCode];
    public static readonly IReadOnlyList<string> Known = [ClaudeCode, Cursor, Codex];
}

/// <summary>Leitura mínima de frontmatter YAML de arquivos Markdown (campos de primeiro nível, valores escalares).</summary>
public static class FrontmatterReader
{
    public static Dictionary<string, string>? Read(string markdown)
    {
        var text = markdown.Replace("\r\n", "\n");
        if (!text.StartsWith("---\n", StringComparison.Ordinal)) return null;
        var end = text.IndexOf("\n---", 4, StringComparison.Ordinal);
        if (end < 0) return null;

        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var line in text[4..end].Split('\n'))
        {
            var m = Regex.Match(line, @"^([A-Za-z][\w-]*):\s*(.*)$");
            if (m.Success) result[m.Groups[1].Value] = m.Groups[2].Value.Trim();
        }
        return result;
    }
}
