using Mariliz.Application.Harness;
using Mariliz.Application.Ports;
using Mariliz.Application.Specification;
using Mariliz.Application.Tests.Support;
using Mariliz.Core.Plan;
using Mariliz.Core.Policy;
using Mariliz.Infrastructure;

namespace Mariliz.Application.Tests;

// C3: a skill do contrato vai também para .agents/skills, que o Codex e o Cursor leem. A pasta .agents segue do
// instalador da especificação e da equipe; o Spock escreve só a pasta da skill dele.
public sealed class AgentsSkillsTests : IDisposable
{
    private readonly TempProject _project = TempProject.Dotnet();

    public void Dispose() => _project.Dispose();

    private const string InstallerLock = """{"version":2,"skills":{}}""";
    private const string TeamSkill = "---\nname: deploy\ndescription: Como publicar.\n---\n\nRode o deploy.\n";
    private const string TeamSkillPath = ".agents/skills/deploy/SKILL.md";

    private static string AgentsSkill => HarnessNaming.Spock.AgentsHarnessContractSkill;

    private Journey JourneyWithLauncher()
    {
        var installer = Journey.FakeInstaller(_project);
        return new Journey(_project, async (spec, ct) => spec.Executable == "spock"
            ? new ProcessResult(0, "0.1.0\n", "", false, false)
            : await installer(spec, ct));
    }

    private void DeclareEnvironments(params string[] environments)
    {
        var declaration = HarnessNaming.Spock.Declaration;
        var text = _project.Read(declaration)!.Replace("\r\n", "\n");
        var list = string.Concat(environments.Select(e => $"- {e}\n"));
        _project.Write(declaration, text.Replace("aiEnvironment: claude-code\n", $"aiEnvironment: claude-code\naiEnvironments:\n{list}"));
    }

    // Cenário 5: o Codex encontra a skill na pasta dele, com o nome igual ao da pasta, sem tocar no que já está em .agents.
    [Theory]
    [InlineData("codex")]
    [InlineData("cursor")]
    public async Task Sem_o_claude_code_a_skill_vai_para_a_pasta_que_a_ide_le(string environment)
    {
        _project.Write(SpecificationManifest.LockFile, InstallerLock);
        _project.Write(TeamSkillPath, TeamSkill);
        var journey = JourneyWithLauncher();

        var plan = await journey.PlanAsync(environment);
        Assert.Equal(ComponentDecisions.Enable, plan.Decisions.Single(d => d.ComponentId == Catalog.SkillAgentsHarnessContract).Decision);
        Assert.DoesNotContain(plan.Decisions, d => d.ComponentId == Catalog.SkillHarnessContract);
        await journey.ApplyAsync(plan);

        var skill = _project.Read(AgentsSkill)!.Replace("\r\n", "\n");
        Assert.StartsWith("---\nname: spock-harness-contract\n", skill);
        Assert.Equal(".agents/skills/spock-harness-contract/SKILL.md", AgentsSkill);
        Assert.Equal(InstallerLock, _project.Read(SpecificationManifest.LockFile));
        Assert.Equal(TeamSkill, _project.Read(TeamSkillPath));
        Assert.False(_project.Exists(HarnessNaming.Spock.HarnessContractSkill));
        Assert.Contains(ManagedLockFile.Read(new LocalProjectFiles(_project.Root))!.Components, c => c.Id == Catalog.SkillAgentsHarnessContract);

        var again = await journey.PlanAsync(environment);
        Assert.All(again.Changes, change => Assert.Equal(ChangeAction.Preserve, change.Action));
    }

    // O Cursor lê também .claude/skills: com o Claude Code na lista, uma segunda cópia apareceria duas vezes.
    [Fact]
    public async Task Claude_code_e_cursor_ficam_so_com_a_skill_do_claude_code()
    {
        var journey = JourneyWithLauncher();
        await journey.ApplyAsync(await journey.PlanAsync());
        DeclareEnvironments("claude-code", "cursor");

        var plan = await journey.PlanAsync();
        await journey.ApplyAsync(plan);

        Assert.DoesNotContain(plan.Decisions, d => d.ComponentId == Catalog.SkillAgentsHarnessContract);
        Assert.True(_project.Exists(HarnessNaming.Spock.HarnessContractSkill));
        Assert.False(_project.Exists(AgentsSkill));
    }

    // O Codex não lê .claude/skills: ele recebe a cópia dele, e o plano diz que a especificação continua só no Claude Code.
    [Fact]
    public async Task Claude_code_e_codex_recebem_cada_um_a_sua_copia()
    {
        var journey = JourneyWithLauncher();
        await journey.ApplyAsync(await journey.PlanAsync());
        DeclareEnvironments("claude-code", "codex");

        var plan = await journey.PlanAsync();
        await journey.ApplyAsync(plan);

        Assert.Equal(_project.Read(HarnessNaming.Spock.HarnessContractSkill), _project.Read(AgentsSkill));
        Assert.Contains(plan.Observations, o => o.Text.Contains(SpecificationManifest.SkillDirectory) && o.Text.Contains("Codex"));
    }

    [Fact]
    public async Task Dispensar_a_skill_tira_so_a_pasta_do_spock()
    {
        _project.Write(SpecificationManifest.LockFile, InstallerLock);
        _project.Write(TeamSkillPath, TeamSkill);
        var journey = JourneyWithLauncher();
        await journey.ApplyAsync(await journey.PlanAsync("codex"));

        await journey.ApplyAsync(await journey.Service.PlanAsync(journey.ProjectId,
            new PlanOptions("codex", ExcludeComponents: [Catalog.SkillAgentsHarnessContract]), CancellationToken.None));

        Assert.False(_project.Exists(AgentsSkill));
        Assert.Equal(TeamSkill, _project.Read(TeamSkillPath));
        Assert.Equal(InstallerLock, _project.Read(SpecificationManifest.LockFile));
        Assert.DoesNotContain(ManagedLockFile.Read(new LocalProjectFiles(_project.Root))!.Components, c => c.Id == Catalog.SkillAgentsHarnessContract);
    }

    [Theory]
    [InlineData(".agents/skills/spock-harness-contract/SKILL.md", true)]
    [InlineData(".agents/skills/harness-contract/SKILL.md", true)]
    [InlineData(".agents/.skill-lock.json", false)]
    [InlineData(".agents/skills/tlc-spec-driven/SKILL.md", false)]
    [InlineData(".agents/skills/deploy/SKILL.md", false)]
    [InlineData(".agents/skills/spock-harness-contract-extra/SKILL.md", false)]
    public void Em_agents_o_spock_escreve_so_a_pasta_da_skill_dele(string path, bool writable)
    {
        Assert.Equal(writable, ManagedScope.IsWritable(path));
        Assert.Equal(!writable, ManagedScope.IsSpecificationOwned(path));
    }
}
