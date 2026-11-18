using System.Text.Json.Nodes;
using Mariliz.Application.Harness;
using Mariliz.Application.Ports;
using Mariliz.Application.Tests.Support;
using Mariliz.Core.Plan;
using Mariliz.Core.Policy;
using Mariliz.Infrastructure;

namespace Mariliz.Application.Tests;

// C3: a tela escolhe mais de uma IDE. O plano grava a lista na declaração (na criação ou como edição pontual aprovada) e,
// quando uma IDE sai da lista, tira o que o Spock gravou para ela, como numa dispensa.
public sealed class EnvironmentSelectionTests : IDisposable
{
    private readonly TempProject _project = TempProject.Dotnet();

    public void Dispose() => _project.Dispose();

    private const string TeamMcp = """{"mcpServers":{"outro":{"command":"outro"}}}""";
    private const string TeamHooks = """{"version":1,"hooks":{"afterFileEdit":[{"command":"./formatar.sh"}]}}""";
    private static readonly string[] CursorComponents = [Catalog.IdeCursorRule, Catalog.IdeCursorMcp, Catalog.HookCursorSession];
    private static readonly string[] CodexComponents = [Catalog.IdeCodexBootstrap, Catalog.IdeCodexMcp, Catalog.HookCodexSession, Catalog.HookCodexPrompt];

    private static string Declaration => HarnessNaming.Spock.Declaration;

    private Journey JourneyWithLauncher()
    {
        var installer = Journey.FakeInstaller(_project);
        return new Journey(_project, async (spec, ct) => spec.Executable == "spock"
            ? new ProcessResult(0, "0.1.0\n", "", false, false)
            : await installer(spec, ct));
    }

    private static Task<ConfigurationPlan> PlanWith(Journey journey, params string[] environments) =>
        journey.Service.PlanAsync(journey.ProjectId, new PlanOptions(null, AiEnvironments: environments), CancellationToken.None);

    private DeclaredConfig ReadDeclaration() => DeclaredConfig.TryRead(new LocalProjectFiles(_project.Root)).Config!;

    private ManagedLockFile ReadLock() => ManagedLockFile.Read(new LocalProjectFiles(_project.Root))!;

    private static void AssertNothingToDo(ConfigurationPlan plan) =>
        Assert.All(plan.Changes, change => Assert.Equal(ChangeAction.Preserve, change.Action));

    [Fact]
    public async Task Projeto_novo_grava_a_lista_escolhida_na_declaracao()
    {
        var journey = JourneyWithLauncher();

        var plan = await PlanWith(journey, "claude-code", "cursor");
        await journey.ApplyAsync(plan);

        var declared = ReadDeclaration();
        Assert.Equal("claude-code", declared.AiEnvironment);
        Assert.Equal(["claude-code", "cursor"], declared.Environments);
        Assert.Equal("claude-code", plan.AiEnvironment);
        Assert.True(_project.Exists(Catalog.PathCursorRule));
        Assert.True(_project.Exists(HarnessNaming.Spock.HarnessContractSkill));
        AssertNothingToDo(await PlanWith(journey, "claude-code", "cursor"));
        AssertNothingToDo(await journey.PlanAsync(null));
    }

    [Fact]
    public async Task Uma_ide_so_mantem_a_declaracao_de_sempre()
    {
        var journey = JourneyWithLauncher();

        await journey.ApplyAsync(await PlanWith(journey, "codex"));

        var text = _project.Read(Declaration)!.Replace("\r\n", "\n");
        Assert.Contains("aiEnvironment: codex\n", text);
        Assert.DoesNotContain("aiEnvironments", text);
        Assert.DoesNotContain("primaryEnvironment", text);
        Assert.DoesNotContain("\nenvironments:", text);
    }

    [Fact]
    public async Task Acrescentar_uma_ide_edita_so_os_ambientes_da_declaracao()
    {
        var journey = JourneyWithLauncher();
        await journey.ApplyAsync(await journey.PlanAsync());
        var before = _project.Read(Declaration)!.Replace("\r\n", "\n");
        _project.Write(Declaration, before.Replace("aiEnvironment: claude-code\n", "aiEnvironment: claude-code # a IDE do time\n") + "# fim\n");

        var plan = await PlanWith(journey, "claude-code", "codex");
        var edit = plan.Changes.Single(c => c.Path == Declaration);
        Assert.Equal(ChangeAction.Update, edit.Action);
        Assert.Contains("codex", edit.Reason);
        await journey.ApplyAsync(plan);

        var after = _project.Read(Declaration)!.Replace("\r\n", "\n");
        Assert.Contains("aiEnvironment: claude-code # a IDE do time\naiEnvironments:\n  - claude-code\n  - codex\n", after);
        Assert.EndsWith("# fim\n", after);
        Assert.All(CodexComponents, id => Assert.Contains(ReadLock().Components, c => c.Id == id));
        Assert.True(_project.Exists(HarnessNaming.Spock.AgentsHarnessContractSkill));
        AssertNothingToDo(await journey.PlanAsync(null));
    }

    [Fact]
    public async Task A_ide_que_sai_da_lista_leva_so_o_que_o_spock_gravou_para_ela()
    {
        _project.Write(Catalog.PathCursorMcp, TeamMcp);
        _project.Write(Catalog.PathCursorHooks, TeamHooks);
        var journey = JourneyWithLauncher();
        await journey.ApplyAsync(await PlanWith(journey, "claude-code", "cursor"));
        Assert.All(CursorComponents, id => Assert.Contains(ReadLock().Components, c => c.Id == id));

        var plan = await PlanWith(journey, "claude-code");
        Assert.All(CursorComponents, id => Assert.Equal(ComponentDecisions.Skip, plan.Decisions.Single(d => d.ComponentId == id).Decision));
        Assert.Equal(ChangeAction.Remove, plan.Changes.Single(c => c.Path == Catalog.PathCursorRule).Action);
        Assert.DoesNotContain(plan.Limitations, l => l.Code == LimitationCodes.PromptHookUnavailable);
        await journey.ApplyAsync(plan);

        Assert.Equal(["claude-code"], ReadDeclaration().Environments);
        Assert.DoesNotContain("aiEnvironments", _project.Read(Declaration));
        Assert.False(_project.Exists(Catalog.PathCursorRule));
        var servers = JsonNode.Parse(_project.Read(Catalog.PathCursorMcp)!)!["mcpServers"]!.AsObject();
        Assert.Equal(["outro"], servers.Select(s => s.Key));
        var hooks = JsonNode.Parse(_project.Read(Catalog.PathCursorHooks)!)!["hooks"]!.AsObject();
        Assert.Equal(["afterFileEdit"], hooks.Select(h => h.Key));
        Assert.DoesNotContain(ReadLock().Components, c => CursorComponents.Contains(c.Id));
        Assert.True(_project.Exists(HarnessNaming.Spock.HarnessContractSkill));
        AssertNothingToDo(await journey.PlanAsync(null));
    }

    // O Codex sai e o Claude Code entra: as peças do Codex e a skill em .agents/skills saem, porque ninguém mais na lista a lê.
    [Fact]
    public async Task Trocar_o_codex_pelo_claude_code_tira_tambem_a_skill_de_agents()
    {
        var journey = JourneyWithLauncher();
        await journey.ApplyAsync(await PlanWith(journey, "codex"));
        Assert.True(_project.Exists(HarnessNaming.Spock.AgentsHarnessContractSkill));

        var plan = await PlanWith(journey, "claude-code");
        await journey.ApplyAsync(plan);

        Assert.Equal("claude-code", ReadDeclaration().AiEnvironment);
        Assert.False(_project.Exists(HarnessNaming.Spock.AgentsHarnessContractSkill));
        Assert.True(_project.Exists(HarnessNaming.Spock.HarnessContractSkill));
        Assert.DoesNotContain(ReadLock().Components, c => CodexComponents.Contains(c.Id) || c.Id == Catalog.SkillAgentsHarnessContract);
        Assert.Equal("claude-code", ReadLock().AiEnvironment);
        AssertNothingToDo(await journey.PlanAsync(null));
    }

    [Fact]
    public async Task O_claude_code_nao_sai_de_um_projeto_ja_configurado()
    {
        var journey = JourneyWithLauncher();
        await journey.ApplyAsync(await journey.PlanAsync());

        var plan = await PlanWith(journey, "cursor");
        await journey.ApplyAsync(plan);

        Assert.Contains(plan.Limitations, l => l.Code == LimitationCodes.EnvironmentRemovalUnsupported);
        Assert.Equal(["claude-code", "cursor"], ReadDeclaration().Environments);
        Assert.True(_project.Exists(HarnessNaming.Spock.HarnessEngineerAgent));
        Assert.True(_project.Exists(Catalog.PathCursorRule));
    }

    // Editar a lista à mão também tira as peças da IDE que saiu: a declaração é a fonte.
    [Fact]
    public async Task Tirar_a_ide_a_mao_na_declaracao_tambem_limpa()
    {
        var journey = JourneyWithLauncher();
        await journey.ApplyAsync(await PlanWith(journey, "claude-code", "codex"));
        var text = _project.Read(Declaration)!.Replace("\r\n", "\n");
        _project.Write(Declaration, text.Replace("  - codex\n", "").Replace("- codex\n", ""));

        var plan = await journey.PlanAsync(null);
        await journey.ApplyAsync(plan);

        Assert.All(CodexComponents, id => Assert.Equal(ComponentDecisions.Skip, plan.Decisions.Single(d => d.ComponentId == id).Decision));
        Assert.False(_project.Exists(HarnessNaming.Spock.AgentsHarnessContractSkill));
        Assert.DoesNotContain("spock", _project.Read(Catalog.PathAgentsMd) ?? "");
    }

    [Fact]
    public async Task A_leitura_do_projeto_traz_os_ambientes_declarados()
    {
        var journey = JourneyWithLauncher();
        Assert.Empty((await journey.Service.DiscoverAsync(journey.ProjectId, CancellationToken.None)).DeclaredAiEnvironments);

        await journey.ApplyAsync(await PlanWith(journey, "claude-code", "codex"));

        Assert.Equal(["claude-code", "codex"], (await journey.Service.DiscoverAsync(journey.ProjectId, CancellationToken.None)).DeclaredAiEnvironments);
    }

    [Theory]
    [InlineData("aiEnvironment: claude-code\nimplementationModel: mariliz\n", "aiEnvironment: claude-code\naiEnvironments:\n  - claude-code\n  - cursor\nimplementationModel: mariliz\n")]
    [InlineData("aiEnvironment: claude-code\naiEnvironments: [claude-code, cursor]\nschema: 1\n", "aiEnvironment: claude-code\nschema: 1\n")]
    [InlineData("aiEnvironments:\n- cursor\n- claude-code\nschema: 1\n", "aiEnvironment: claude-code\naiEnvironments:\n  - claude-code\n  - cursor\nschema: 1\n")]
    // As cópias calculadas que versões anteriores gravavam saem, para não contradizer a lista nova.
    [InlineData("aiEnvironment: claude-code\nprimaryEnvironment: claude-code\nenvironments:\n- claude-code\nschema: 1\n", "aiEnvironment: claude-code\naiEnvironments:\n  - claude-code\n  - cursor\nschema: 1\n")]
    public void O_editor_troca_so_os_ambientes(string before, string expected)
    {
        var environments = expected.Contains("cursor") ? new[] { "claude-code", "cursor" } : ["claude-code"];
        Assert.Equal(expected, DeclarationEditor.WithEnvironments(before, environments));
    }

    [Theory]
    [InlineData("aiEnvironment: claude-code\naiEnvironments: cursor\n")]
    [InlineData("aiEnvironment: claude-code\naiEnvironments:\n  cursor: true\n")]
    public void O_editor_recusa_um_formato_que_nao_reconhece(string text) =>
        Assert.Null(DeclarationEditor.WithEnvironments(text, ["claude-code", "cursor"]));
}
