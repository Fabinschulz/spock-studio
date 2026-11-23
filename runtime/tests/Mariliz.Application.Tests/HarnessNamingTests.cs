using System.Text.Json;
using System.Text.RegularExpressions;
using Mariliz.Application.Harness;
using Mariliz.Application.Ports;
using Mariliz.Application.Tests.Support;
using Mariliz.Core.Plan;
using Mariliz.Core.Policy;
using Mariliz.Infrastructure;

namespace Mariliz.Application.Tests;

// O nome nos arquivos é a assinatura do Spock no projeto: projeto novo recebe "spock"; o configurado antes da troca de nome
// continua com "mariliz", sem renomear, sem duplicar e sem conflito. Os caminhos aqui são literais de propósito.
public partial class HarnessNamingTests
{
    // Referências aos caminhos antigos dentro do texto gravado. Os ids dos componentes (mariliz.rule.general) não entram.
    [GeneratedRegex(@"mariliz\.(yaml|lock)|\.mariliz/|mariliz-(repo|general|stack|architecture|policy)")]
    private static partial Regex LegacyPath();

    [GeneratedRegex(@"\bMariliz\b")]
    private static partial Regex FormerProduct();

    private static readonly string[] ExternalPrefixes = [".claude/skills/tlc-spec-driven/", ".agents/"];

    // Agentes e skill do catálogo citados no texto (entre crases) e o nome sem prefixo de antes da troca.
    [GeneratedRegex(@"`((?:spock-)?(?:harness-engineer|code-reviewer|verifier|harness-contract))`")]
    private static partial Regex CatalogName();

    [GeneratedRegex(@"`(spock-[a-z-]+\.md)`")]
    private static partial Regex RuleName();

    private static readonly string[] SpockAgents =
        [".claude/agents/spock-harness-engineer.md", ".claude/agents/spock-code-reviewer.md", ".claude/agents/spock-verifier.md"];

    private const string SpockSkill = ".claude/skills/spock-harness-contract/SKILL.md";

    [Fact]
    public async Task Projeto_novo_recebe_so_caminhos_spock_e_o_texto_assina_Spock()
    {
        using var project = TempProject.Dotnet();
        project.Write("web/package.json", "{\"name\":\"web\",\"dependencies\":{\"next\":\"^16.0.1\"},\"devDependencies\":{\"typescript\":\"~5.6.0\"},\"scripts\":{\"build\":\"next build\",\"test\":\"vitest run\"}}");
        project.Write("web/tsconfig.json", "{}");
        project.Write(".spock/arquitetura/especificacao.md", "# Pedidos\n\n## Requisitos\n\n- **REQ-01** (must): O sistema deve registrar o pedido.\n");
        project.Write(".spock/arquitetura/arquitetura.md", "# Arquitetura\n\n## Stack\n\n- **backend**: ASP.NET Core 9. A equipe conhece.\n");
        project.Write(".spock/arquitetura/backlog.md", "# Backlog\n\n## EPIC-01 Pedidos\n\n### US-01 Ver os pedidos\n");
        string Enable(string id) => $"{{\"id\":\"{id}\",\"decision\":\"enable\",\"justification\":\"Cobre o projeto inteiro.\",\"evidence\":[\"backend/App.sln\"]}}";
        project.Write(".spock/proposal.json", "{\"schema\":1,\"origin\":\"harness-engineer\",\"decisions\":[" + string.Join(",", Catalog.Proposable.Select(Enable)) + "]," +
            "\"additionalRules\":[{\"id\":\"read-first\",\"title\":\"Read the guide first\",\"content\":\"Read docs/guide.md before any change.\",\"justification\":\"O guia define a ordem de leitura.\",\"evidence\":[\"docs/guide.md\"]}]}");
        var before = Snapshot(project);
        // Com o lançador spock nesta "máquina": todos os componentes do catálogo entram no plano.
        var installer = Journey.FakeInstaller(project);
        var journey = new Journey(project, async (spec, ct) => spec.Executable == "spock"
            ? new ProcessResult(0, "0.1.0\n", "", false, false)
            : await installer(spec, ct));

        var plan = await journey.PlanAsync();
        // O hook de sessão divide o settings.json com o hook de política: aparece pela decisão, não por uma mudança própria.
        var proposed = plan.Changes.Select(c => c.ComponentId)
            .Concat(plan.Decisions.Where(d => d.Decision == ComponentDecisions.Enable).Select(d => d.ComponentId)).ToHashSet(StringComparer.Ordinal);
        // Os do Codex e do Cursor só entram quando eles estão entre os ambientes (CodexAdapterTests, CursorAdapterTests).
        string[] codex = [Catalog.IdeCodexBootstrap, Catalog.IdeCodexMcp, Catalog.HookCodexSession, Catalog.HookCodexPrompt,
            Catalog.IdeCursorRule, Catalog.IdeCursorMcp, Catalog.HookCursorSession, Catalog.SkillAgentsHarnessContract,
            // Projeto .NET e Next: os padrões Angular não entram (PatternRuleTests).
            Catalog.RulePatternsAngular];
        foreach (var component in Catalog.Components.Where(c => c.Id != Catalog.SpecificationStructure && !codex.Contains(c.Id)))
            Assert.Contains(component.Id, proposed);
        Assert.All(plan.Changes, change => Assert.DoesNotContain("mariliz", change.Path, StringComparison.OrdinalIgnoreCase));
        string[] created =
        [
                     "spock.yaml", "spock.lock", ".claude/rules/spock-general.md", ".claude/rules/spock-stack-dotnet.md", ".claude/rules/spock-stack-javascript.md",
                     ".claude/rules/spock-repo.md", ".claude/rules/spock-architecture.md", ".claude/rules/spock-repo-read-first.md",
                     ".claude/rules/spock-patterns-react.md", ".claude/rules/spock-patterns-dotnet.md", ".claude/rules/spock-patterns-fullstack.md",
                     ".claude/hooks/spock-policy.py", ".spock/schemas/harness-proposal.v1.schema.json", .. SpockAgents, SpockSkill
        ];
        foreach (var path in created)
            Assert.Contains(plan.Changes, change => change.Path == path && change.Action == ChangeAction.Create);

        await journey.ApplyAsync(plan);

        var written = Snapshot(project).Where(f => !before.TryGetValue(f.Key, out var hash) || hash != f.Value)
            .Select(f => f.Key).Where(path => !ExternalPrefixes.Any(path.StartsWith)).ToList();
        Assert.All(written, path => Assert.DoesNotContain("mariliz", path, StringComparison.OrdinalIgnoreCase));
        // Todo agente e toda skill que o Spock grava levam o prefixo, e o nome do frontmatter é o do arquivo (ou da pasta).
        Assert.Equal(SpockAgents.Order(StringComparer.Ordinal), written.Where(path => path.StartsWith(".claude/agents/", StringComparison.Ordinal)).Order(StringComparer.Ordinal).ToList());
        Assert.Equal([SpockSkill], written.Where(path => path.StartsWith(".claude/skills/", StringComparison.Ordinal)).ToList());
        foreach (var path in SpockAgents)
            Assert.Contains($"\nname: {Path.GetFileNameWithoutExtension(path)}\n", project.Read(path));
        Assert.Contains("\nname: spock-harness-contract\n", project.Read(SpockSkill));
        Assert.All(ManagedLockFile.Read(new LocalProjectFiles(project.Root))!.ManagedFiles, entry => Assert.DoesNotContain("mariliz", entry.Path, StringComparison.Ordinal));
        foreach (var path in written)
        {
            var text = project.Read(path)!;
            Assert.False(FormerProduct().IsMatch(text), $"{path} cita o nome antigo do produto.");
            // O hook é o mesmo nos dois nomes: protege spock.yaml e mariliz.yaml.
            if (path != ".claude/hooks/spock-policy.py")
                Assert.False(LegacyPath().IsMatch(text), $"{path} aponta para um caminho antigo: {LegacyPath().Match(text).Value}");
            // Cada agente, skill e regra citada existe no projeto com aquele nome.
            foreach (Match match in CatalogName().Matches(text))
            {
                var name = match.Groups[1].Value;
                Assert.True(project.Exists($".claude/agents/{name}.md") || project.Exists($".claude/skills/{name}/SKILL.md"), $"{path} cita `{name}`, que não existe.");
            }
            foreach (Match match in RuleName().Matches(text))
                Assert.True(project.Exists(".claude/rules/" + match.Groups[1].Value), $"{path} cita `{match.Groups[1].Value}`, que não existe.");
        }

        Assert.StartsWith("# Declaração do Harness gerenciado pelo Spock Studio.", project.Read("spock.yaml"));
        Assert.StartsWith("# Gerado pelo Spock Studio.", project.Read("spock.lock"));
        Assert.Contains("Managed by Spock Studio.", project.Read(".claude/rules/spock-general.md"));
        Assert.Contains("`spock-repo.md`", project.Read(".claude/rules/spock-general.md"));
        var engineer = project.Read(".claude/agents/spock-harness-engineer.md")!;
        Assert.Contains("the Spock engine", engineer);
        Assert.Contains("`.spock/proposal.json`", engineer);
        Assert.Contains("`spock.yaml`", engineer);
        Assert.Contains("`spock-verifier`", engineer);
        Assert.Contains("`spock-harness-contract`", engineer);
        Assert.Contains("(`spock-verifier`)", project.Read(".claude/rules/spock-general.md"));
        Assert.Contains("${CLAUDE_PROJECT_DIR}/.claude/hooks/spock-policy.py", project.Read(".claude/settings.json"));
        Assert.False(project.Exists(".mariliz"));

        var again = await journey.PlanAsync();
        Assert.True(again.Changes.All(c => c.Action == ChangeAction.Preserve), string.Join(", ", again.Changes.Where(c => c.Action != ChangeAction.Preserve).Select(c => $"{c.Path}:{c.Action}")));
        Assert.Equal("passed", Journey.Status(await journey.ValidateAsync(), "EV-IDEMP-001"));
    }

    [Fact]
    public async Task Projeto_configurado_com_os_nomes_antigos_continua_com_eles_sem_renomear_nem_duplicar()
    {
        using var project = TempProject.Dotnet();
        project.Write("mariliz.yaml", "# Declaração do Harness gerenciado pelo Mariliz Studio.\n# Este arquivo é seu: edite à vontade. A aplicação o lê, mas não o reescreve.\n"
            + "schema: 1\nproject:\n  name: app\nimplementationModel: mariliz\naiEnvironment: claude-code\n");
        project.Write(".mariliz/proposal.json", "{\"schema\":1,\"origin\":\"harness-engineer\",\"decisions\":[],\"additionalRules\":[{\"id\":\"read-first\",\"title\":\"Read the guide first\","
            + "\"content\":\"Read docs/guide.md before any change.\",\"justification\":\"O guia define a ordem de leitura.\",\"evidence\":[\"docs/guide.md\"]}]}");
        var journey = new Journey(project, Journey.FakeInstaller(project));
        await journey.ApplyAsync(await journey.PlanAsync());

        // O que a versão anterior do Spock deixava: textos com o nome antigo e o lock registrando exatamente esse conteúdo.
        OldEngineText(project, ".claude/agents/harness-engineer.md", "the Spock engine", "the Mariliz engine");
        OldEngineText(project, ".mariliz/schemas/harness-proposal.v1.schema.json", "The Spock engine", "The Mariliz engine");
        var lockText = project.Read("mariliz.lock")!;
        project.Write("mariliz.lock", "# Gerado pelo Mariliz Studio. Não edite à mão: use a aplicação.\n" + lockText[(lockText.IndexOf('\n') + 1)..]);
        var legacyFiles = Snapshot(project).Keys.ToHashSet(StringComparer.Ordinal);

        var plan = await journey.PlanAsync();

        Assert.DoesNotContain(plan.Changes, c => c.Action is ChangeAction.Conflict or ChangeAction.Remove);
        Assert.DoesNotContain(plan.Changes, c => c.Action == ChangeAction.Create);
        Assert.All(plan.Changes, change => Assert.DoesNotContain("spock", change.Path, StringComparison.OrdinalIgnoreCase));
        Assert.Contains(plan.Changes, c => c.Path == ".claude/rules/mariliz-general.md" && c.Action == ChangeAction.Preserve);
        Assert.Contains(plan.Changes, c => c.Path == ".claude/rules/mariliz-repo-read-first.md" && c.Action == ChangeAction.Preserve);

        await journey.ApplyAsync(plan);

        Assert.Equal(legacyFiles, Snapshot(project).Keys.ToHashSet(StringComparer.Ordinal));
        Assert.False(project.Exists("spock.yaml") || project.Exists("spock.lock") || project.Exists(".spock") || project.Exists(".claude/rules/spock-general.md")
            || project.Exists(".claude/hooks/spock-policy.py"));
        Assert.Contains("the Spock engine", project.Read(".claude/agents/harness-engineer.md"));
        Assert.Contains("`.mariliz/proposal.json`", project.Read(".claude/agents/harness-engineer.md"));
        Assert.StartsWith("# Gerado pelo Spock Studio.", project.Read("mariliz.lock"));
        var settings = project.Read(".claude/settings.json")!;
        Assert.Single(Regex.Matches(settings, "policy\\.py"));
        Assert.Contains(".claude/hooks/mariliz-policy.py", settings);
        Assert.Contains("\"spock.yaml\"", project.Read(".claude/hooks/mariliz-policy.py"));
        // Agentes e skill continuam sem prefixo, com o nome de antes e citados assim.
        foreach (var name in new[] { "harness-engineer", "code-reviewer", "verifier" })
            Assert.Contains($"\nname: {name}\n", project.Read($".claude/agents/{name}.md"));
        Assert.Contains("\nname: harness-contract\n", project.Read(".claude/skills/harness-contract/SKILL.md"));
        Assert.Contains("`verifier`", project.Read(".claude/agents/harness-engineer.md"));
        Assert.Contains("(`verifier`)", project.Read(".claude/rules/mariliz-general.md"));

        var failed = (await journey.ValidateAsync())["validation"]!["results"]!.AsArray()
            .Where(r => r!["required"]!.GetValue<bool>() && r["status"]!.GetValue<string>() != "passed").ToList();
        Assert.True(failed.Count == 0, string.Join("; ", failed.Select(f => $"{f!["id"]}={f["status"]}: {f["reason"]}")));
        var again = await journey.PlanAsync();
        Assert.True(again.Changes.All(c => c.Action == ChangeAction.Preserve), string.Join(", ", again.Changes.Where(c => c.Action != ChangeAction.Preserve).Select(c => $"{c.Path}:{c.Action}")));
    }

    [Theory]
    [InlineData("spock.yaml", "ask")]
    [InlineData("spock.lock", "ask")]
    [InlineData("mariliz.yaml", "ask")]
    [InlineData("mariliz.lock", "ask")]
    [InlineData(".claude/rules/spock-general.md", "ask")]
    [InlineData(".claude/rules/mariliz-general.md", "ask")]
    [InlineData(".claude/hooks/spock-policy.py", "ask")]
    [InlineData(".claude/hooks/mariliz-policy.py", "ask")]
    [InlineData(".claude/agents/spock-verifier.md", "ask")]
    [InlineData(".claude/agents/verifier.md", "ask")]
    [InlineData(".claude/skills/spock-harness-contract/SKILL.md", "ask")]
    [InlineData(".claude/skills/harness-contract/SKILL.md", "ask")]
    [InlineData(".spock/proposal.json", null)]
    [InlineData(".mariliz/proposal.json", null)]
    public async Task O_hook_protege_os_arquivos_gerenciados_nos_dois_nomes(string path, string? expected)
    {
        using var project = new TempProject();
        var script = Path.Combine(project.Root, "hook.py");
        await File.WriteAllTextAsync(script, AssetLoader.Read("hooks/mariliz-policy.py"));
        var payload = new { hook_event_name = "PreToolUse", tool_name = "Write", cwd = project.Root, tool_input = new { file_path = path } };

        var result = await new SystemProcessRunner().RunAsync(
            new ProcessSpec(Python(), [script], project.Root, TimeSpan.FromSeconds(20), JsonSerializer.Serialize(payload)), CancellationToken.None);

        Assert.Equal(0, result.ExitCode);
        var decision = string.IsNullOrWhiteSpace(result.StandardOutput)
            ? null
            : JsonDocument.Parse(result.StandardOutput).RootElement.GetProperty("hookSpecificOutput").GetProperty("permissionDecision").GetString();
        Assert.Equal(expected, decision);
        // O que o hook protege é o que o motor pode gravar, nos dois nomes.
        Assert.True(ManagedScope.IsWritable(path), path);
    }

    private static void OldEngineText(TempProject project, string path, string current, string old)
    {
        var lockText = project.Read("mariliz.lock")!;
        var before = project.Hash(path)!;
        project.Write(path, project.Read(path)!.Replace(current, old));
        project.Write("mariliz.lock", lockText.Replace(before, project.Hash(path)!));
    }

    private static string Python()
    {
        var runner = new SystemProcessRunner();
        foreach (var candidate in new[] { "python3", "python" })
        {
            var r = runner.RunAsync(new ProcessSpec(candidate, ["--version"], Environment.CurrentDirectory, TimeSpan.FromSeconds(10)), CancellationToken.None).GetAwaiter().GetResult();
            if (!r.NotFound && r.ExitCode == 0 && (r.StandardOutput + r.StandardError).Contains("Python 3")) return candidate;
        }
        throw new InvalidOperationException("Python 3 é necessário para testar o hook.");
    }

    private static Dictionary<string, string> Snapshot(TempProject project)
    {
        var files = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var file in Directory.EnumerateFiles(project.Root, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(project.Root, file).Replace(Path.DirectorySeparatorChar, '/');
            if (relative.StartsWith(".git/", StringComparison.Ordinal)) continue;
            files[relative] = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(file)));
        }
        return files;
    }
}
