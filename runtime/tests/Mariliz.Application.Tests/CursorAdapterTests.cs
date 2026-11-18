using Mariliz.Core.Policy;
using System.Text.Json.Nodes;
using Mariliz.Application.Harness;
using Mariliz.Application.Ports;
using Mariliz.Application.Tests.Support;
using Mariliz.Core.Plan;
using Mariliz.Infrastructure;

namespace Mariliz.Application.Tests;

// C3: o Harness liga o Cursor ao Spock pelo comando spock. Regra própria em .cursor/rules, servidor MCP no .cursor/mcp.json e
// hook de sessão no .cursor/hooks.json, que tem formato próprio. O Cursor não tem hook por prompt, e o plano diz isso.
public sealed class CursorAdapterTests : IDisposable
{
    private readonly TempProject _project = TempProject.Dotnet();

    public void Dispose() => _project.Dispose();

    private const string TeamMcp = """{"mcpServers":{"outro":{"command":"outro"}}}""";
    private const string TeamHooks = """{"version":1,"hooks":{"afterFileEdit":[{"command":"./formatar.sh"}]}}""";

    private static readonly string[] CursorComponents = [Catalog.IdeCursorRule, Catalog.IdeCursorMcp, Catalog.HookCursorSession];

    private Journey JourneyWith(bool launcher)
    {
        var installer = Journey.FakeInstaller(_project);
        return new Journey(_project, async (spec, ct) => spec.Executable == "spock"
            ? launcher ? new ProcessResult(0, "0.1.0\n", "", false, false) : new ProcessResult(-1, "", "", false, true)
            : await installer(spec, ct));
    }

    // Cenário 4: o Cursor recebe o contexto sem nenhum arquivo do Claude Code no projeto.
    [Fact]
    public async Task Cursor_sozinho_recebe_a_ligacao_com_a_ide_e_nenhum_arquivo_do_claude_code()
    {
        _project.Write(Catalog.PathCursorMcp, TeamMcp);
        _project.Write(Catalog.PathCursorHooks, TeamHooks);
        var journey = JourneyWith(launcher: true);

        var plan = await journey.PlanAsync("cursor");
        foreach (var id in CursorComponents)
            Assert.Equal(ComponentDecisions.Enable, plan.Decisions.Single(d => d.ComponentId == id).Decision);
        Assert.DoesNotContain(plan.Changes, c => c.Path.StartsWith(".claude/", StringComparison.Ordinal) || c.Path is "CLAUDE.md" or ".mcp.json");
        Assert.Contains(plan.Limitations, l => l.Code == LimitationCodes.PromptHookUnavailable);
        Assert.Contains(plan.Limitations, l => l.Code == LimitationCodes.EnvironmentPartial);
        Assert.Contains(plan.Risks, r => r.Contains("workspace confiável"));

        await journey.ApplyAsync(plan);

        var rule = _project.Read(Catalog.PathCursorRule)!;
        Assert.StartsWith("---\ndescription: ", rule);
        Assert.Contains("\nalwaysApply: true\n---\n", rule);
        Assert.Contains("spock_context_bootstrap", rule);
        var servers = JsonNode.Parse(_project.Read(Catalog.PathCursorMcp)!)!["mcpServers"]!;
        Assert.Equal("outro", servers["outro"]!["command"]!.GetValue<string>());
        Assert.Equal(["mcp", "--project-dir", "${workspaceFolder}"], servers["spock"]!["args"]!.AsArray().Select(a => a!.GetValue<string>()));
        var hooks = JsonNode.Parse(_project.Read(Catalog.PathCursorHooks)!)!;
        Assert.Equal(1, hooks["version"]!.GetValue<int>());
        Assert.Equal("./formatar.sh", hooks["hooks"]!["afterFileEdit"]![0]!["command"]!.GetValue<string>());
        Assert.Equal(CursorHooksMerger.SessionCommand, hooks["hooks"]!["sessionStart"]![0]!["command"]!.GetValue<string>());
        Assert.False(_project.Exists(Catalog.PathSettings));
        var lockFile = ManagedLockFile.Read(new LocalProjectFiles(_project.Root))!;
        Assert.Equal("cursor", lockFile.AiEnvironment);
        Assert.All(CursorComponents, id => Assert.Contains(lockFile.Components, c => c.Id == id));

        var again = await journey.PlanAsync("cursor");
        Assert.All(again.Changes, change => Assert.Equal(ChangeAction.Preserve, change.Action));
        var validation = await journey.ValidateAsync();
        var failed = validation["validation"]?["results"]?.AsArray().Where(r => r!["status"]!.GetValue<string>() == "failed")
            .Select(r => $"{r!["id"]}: {r["reason"]}") ?? [];
        Assert.True(validation["outcome"]!.GetValue<string>() != "failed", string.Join(" | ", failed));
    }

    // Uma equipe com as três IDEs declara a lista no spock.yaml; cada uma recebe só as peças dela.
    [Fact]
    public async Task Tres_ambientes_na_lista_cada_um_com_as_suas_pecas()
    {
        var journey = JourneyWith(launcher: true);
        await journey.ApplyAsync(await journey.PlanAsync());
        var declaration = HarnessNaming.Spock.Declaration;
        var text = _project.Read(declaration)!.Replace("\r\n", "\n");
        _project.Write(declaration, text.Replace("aiEnvironment: claude-code\n", "aiEnvironment: claude-code\naiEnvironments:\n- claude-code\n- codex\n- cursor\n"));

        var plan = await journey.PlanAsync();
        await journey.ApplyAsync(plan);

        Assert.All(CursorComponents.Concat([Catalog.IdeCodexBootstrap, Catalog.HookCodexSession]),
            id => Assert.Equal(ComponentDecisions.Enable, plan.Decisions.Single(d => d.ComponentId == id).Decision));
        Assert.True(_project.Exists(Catalog.PathCursorRule));
        Assert.True(_project.Exists(Catalog.PathCodexHooks));
        Assert.True(SettingsMerger.HasPrompt(_project.Read(Catalog.PathSettings)));
        Assert.DoesNotContain(plan.Limitations, l => l.Code == LimitationCodes.EnvironmentPartial);
    }

    [Fact]
    public async Task Sem_o_lancador_entra_so_a_regra()
    {
        var journey = JourneyWith(launcher: false);

        var plan = await journey.PlanAsync("cursor");
        await journey.ApplyAsync(plan);

        Assert.Single(plan.Limitations, l => l.Code == LimitationCodes.LauncherMissing);
        Assert.Equal(ComponentDecisions.Skip, plan.Decisions.Single(d => d.ComponentId == Catalog.IdeCursorMcp).Decision);
        Assert.Equal(ComponentDecisions.Skip, plan.Decisions.Single(d => d.ComponentId == Catalog.HookCursorSession).Decision);
        Assert.True(_project.Exists(Catalog.PathCursorRule));
        Assert.False(_project.Exists(Catalog.PathCursorMcp));
        Assert.False(_project.Exists(Catalog.PathCursorHooks));
    }

    [Fact]
    public async Task Dispensar_o_hook_do_cursor_tira_so_o_registro_dele()
    {
        _project.Write(Catalog.PathCursorHooks, TeamHooks);
        var journey = JourneyWith(launcher: true);
        await journey.ApplyAsync(await journey.PlanAsync("cursor"));

        await journey.ApplyAsync(await journey.Service.PlanAsync(journey.ProjectId,
            new PlanOptions("cursor", ExcludeComponents: [Catalog.HookCursorSession]), CancellationToken.None));

        var hooks = JsonNode.Parse(_project.Read(Catalog.PathCursorHooks)!)!;
        Assert.False(CursorHooksMerger.HasSession(hooks.ToJsonString()));
        Assert.Equal("./formatar.sh", hooks["hooks"]!["afterFileEdit"]![0]!["command"]!.GetValue<string>());
        Assert.DoesNotContain(ManagedLockFile.Read(new LocalProjectFiles(_project.Root))!.Components, c => c.Id == Catalog.HookCursorSession);
    }

    [Fact]
    public void Hook_do_cursor_entra_no_formato_dele_e_sai_sozinho()
    {
        var (created, _) = CursorHooksMerger.MergeSession(null);
        var (merged, error) = CursorHooksMerger.MergeSession(TeamHooks);

        Assert.Null(error);
        var fresh = JsonNode.Parse(created!)!;
        Assert.Equal(1, fresh["version"]!.GetValue<int>());
        Assert.Equal(15, fresh["hooks"]!["sessionStart"]![0]!["timeout"]!.GetValue<int>());
        Assert.Null(fresh["hooks"]!["sessionStart"]![0]!["hooks"]);
        Assert.Equal(merged, CursorHooksMerger.MergeSession(merged).Content);
        var released = CursorHooksMerger.ReleaseSession(merged)!;
        Assert.False(CursorHooksMerger.HasSession(released));
        Assert.Null(JsonNode.Parse(released)!["hooks"]!["sessionStart"]);
        Assert.Null(CursorHooksMerger.ReleaseSession(released));
        Assert.StartsWith(Catalog.PathCursorHooks, CursorHooksMerger.MergeSession("{ quebrado").Error);
    }
}
