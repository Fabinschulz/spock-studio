using Mariliz.Core.Policy;
using System.Text.Json.Nodes;
using Mariliz.Application.Harness;
using Mariliz.Application.Ports;
using Mariliz.Application.Tests.Support;
using Mariliz.Core.Plan;
using Mariliz.Infrastructure;

namespace Mariliz.Application.Tests;

// C3: o Harness liga o Codex ao Spock pelo comando spock. Bloco no AGENTS.md, servidor MCP no .codex/config.toml e hooks de
// sessão e por prompt no .codex/hooks.json, cada um só no trecho do Spock.
public sealed class CodexAdapterTests : IDisposable
{
    private readonly TempProject _project = TempProject.Dotnet();

    public void Dispose() => _project.Dispose();

    private const string TeamAgents = "# Instruções da equipe\n\nRode os testes antes de abrir PR.\n";
    private const string TeamConfig = "model = \"gpt-5\"\n\n[mcp_servers.outro]\ncommand = \"outro\"\n";

    private static readonly string[] CodexComponents =
        [Catalog.IdeCodexBootstrap, Catalog.IdeCodexMcp, Catalog.HookCodexSession, Catalog.HookCodexPrompt];

    private Journey JourneyWith(bool launcher)
    {
        var installer = Journey.FakeInstaller(_project);
        return new Journey(_project, async (spec, ct) => spec.Executable == "spock"
            ? launcher ? new ProcessResult(0, "0.1.0\n", "", false, false) : new ProcessResult(-1, "", "", false, true)
            : await installer(spec, ct));
    }

    private JsonNode Hooks() => JsonNode.Parse(_project.Read(Catalog.PathCodexHooks)!)!;

    // Cenário 5: o Codex recebe o contexto sem nenhum arquivo do Claude Code no projeto.
    [Fact]
    public async Task Codex_sozinho_recebe_a_ligacao_com_a_ide_e_nenhum_arquivo_do_claude_code()
    {
        _project.Write(Catalog.PathAgentsMd, TeamAgents);
        _project.Write(Catalog.PathCodexConfig, TeamConfig);
        var journey = JourneyWith(launcher: true);

        var plan = await journey.PlanAsync("codex");
        foreach (var id in CodexComponents)
            Assert.Equal(ComponentDecisions.Enable, plan.Decisions.Single(d => d.ComponentId == id).Decision);
        Assert.DoesNotContain(plan.Changes, c => c.Path.StartsWith(".claude/", StringComparison.Ordinal) || c.Path is "CLAUDE.md" or ".mcp.json");
        Assert.Empty(plan.Commands);
        Assert.Contains(plan.Limitations, l => l.Code == LimitationCodes.EnvironmentPartial);
        Assert.DoesNotContain(plan.Limitations, l => l.Code == LimitationCodes.HarnessEngineerNotInvoked);
        Assert.Contains(plan.Risks, r => r.Contains("/hooks"));

        await journey.ApplyAsync(plan);

        Assert.StartsWith(TeamAgents, _project.Read(Catalog.PathAgentsMd));
        Assert.Contains("<!-- spock:begin spock.bootstrap", _project.Read(Catalog.PathAgentsMd));
        var config = _project.Read(Catalog.PathCodexConfig)!;
        Assert.StartsWith(TeamConfig, config);
        Assert.Contains("[mcp_servers.spock]\ncommand = \"spock\"\nargs = [\"mcp\", \"--project-dir\", \".\"]", config);
        Assert.Equal(SettingsMerger.SessionCommand, Hooks()["hooks"]!["SessionStart"]![0]!["hooks"]![0]!["command"]!.GetValue<string>());
        Assert.Equal(SettingsMerger.PromptCommand, Hooks()["hooks"]!["UserPromptSubmit"]![0]!["hooks"]![0]!["command"]!.GetValue<string>());
        Assert.False(_project.Exists(Catalog.PathSettings));
        var lockFile = ManagedLockFile.Read(new LocalProjectFiles(_project.Root))!;
        Assert.Equal("codex", lockFile.AiEnvironment);
        Assert.All(CodexComponents, id => Assert.Contains(lockFile.Components, c => c.Id == id));

        var again = await journey.PlanAsync("codex");
        Assert.All(again.Changes, change => Assert.Equal(ChangeAction.Preserve, change.Action));
        var validation = await journey.ValidateAsync();
        var failed = validation["validation"]?["results"]?.AsArray().Where(r => r!["status"]!.GetValue<string>() == "failed")
            .Select(r => $"{r!["id"]}: {r["reason"]}") ?? [];
        Assert.True(validation["outcome"]!.GetValue<string>() != "failed", string.Join(" | ", failed));
    }

    // A equipe que usa os dois declara a lista no spock.yaml: o Codex entra sem tocar no que o Claude Code já tem.
    [Fact]
    public async Task Claude_code_com_codex_na_lista_ganha_as_pecas_do_codex_e_mantem_as_suas()
    {
        var journey = JourneyWith(launcher: true);
        await journey.ApplyAsync(await journey.PlanAsync());
        var claudeFiles = new[] { Catalog.PathSettings, Catalog.PathClaudeMd, Catalog.PathMcpJson }.ToDictionary(p => p, p => _project.Read(p));
        var declaration = HarnessNaming.Spock.Declaration;
        var text = _project.Read(declaration)!.Replace("\r\n", "\n");
        Assert.Contains("aiEnvironment: claude-code\n", text);
        _project.Write(declaration, text.Replace("aiEnvironment: claude-code\n", "aiEnvironment: claude-code\naiEnvironments:\n- claude-code\n- codex\n"));

        var plan = await journey.PlanAsync();
        await journey.ApplyAsync(plan);

        Assert.All(CodexComponents, id => Assert.Equal(ComponentDecisions.Enable, plan.Decisions.Single(d => d.ComponentId == id).Decision));
        Assert.DoesNotContain(plan.Limitations, l => l.Code == LimitationCodes.EnvironmentPartial);
        Assert.All(claudeFiles, pair => Assert.Equal(pair.Value, _project.Read(pair.Key)));
        Assert.True(_project.Exists(Catalog.PathCodexHooks));
        Assert.Equal("claude-code", ManagedLockFile.Read(new LocalProjectFiles(_project.Root))!.AiEnvironment);
    }

    [Fact]
    public async Task Sem_o_lancador_entra_so_o_bloco_e_o_aviso_sai_uma_vez()
    {
        var journey = JourneyWith(launcher: false);

        var plan = await journey.PlanAsync("codex");
        await journey.ApplyAsync(plan);

        Assert.Single(plan.Limitations, l => l.Code == LimitationCodes.LauncherMissing);
        foreach (var id in new[] { Catalog.IdeCodexMcp, Catalog.HookCodexSession, Catalog.HookCodexPrompt })
            Assert.Equal(ComponentDecisions.Skip, plan.Decisions.Single(d => d.ComponentId == id).Decision);
        Assert.Contains("<!-- spock:begin spock.bootstrap", _project.Read(Catalog.PathAgentsMd));
        Assert.False(_project.Exists(Catalog.PathCodexConfig));
        Assert.False(_project.Exists(Catalog.PathCodexHooks));
    }

    [Fact]
    public async Task Dispensar_o_hook_por_prompt_do_codex_tira_so_o_registro_dele()
    {
        var journey = JourneyWith(launcher: true);
        await journey.ApplyAsync(await journey.PlanAsync("codex"));

        var plan = await journey.Service.PlanAsync(journey.ProjectId,
            new PlanOptions("codex", ExcludeComponents: [Catalog.HookCodexPrompt]), CancellationToken.None);
        await journey.ApplyAsync(plan);

        var hooks = _project.Read(Catalog.PathCodexHooks);
        Assert.False(SettingsMerger.HasPrompt(hooks));
        Assert.True(SettingsMerger.HasSession(hooks));
        Assert.DoesNotContain(ManagedLockFile.Read(new LocalProjectFiles(_project.Root))!.Components, c => c.Id == Catalog.HookCodexPrompt);
    }

    // O .codex/hooks.json da equipe fica: o Spock entra ao lado e a mensagem de erro cita o arquivo certo.
    [Fact]
    public void Hooks_do_codex_entram_ao_lado_dos_da_equipe()
    {
        const string current = """{ "description": "da equipe", "hooks": { "SessionStart": [ { "hooks": [ { "type": "command", "command": "./aquecer.sh" } ] } ] } }""";

        var (merged, error) = SettingsMerger.MergeSession(current, Catalog.PathCodexHooks);
        var broken = SettingsMerger.MergeSession("{ quebrado", Catalog.PathCodexHooks);

        Assert.Null(error);
        var root = JsonNode.Parse(merged!)!;
        Assert.Equal("da equipe", root["description"]!.GetValue<string>());
        Assert.Equal(2, root["hooks"]!["SessionStart"]!.AsArray().Count);
        Assert.StartsWith(Catalog.PathCodexHooks, broken.Error);
    }
}
