using Mariliz.Application.Harness;
using Mariliz.Application.Tests.Support;
using Mariliz.Application.Validation;
using Mariliz.Application.Workflows;
using Mariliz.Core.Policy;

namespace Mariliz.Application.Tests;

// W3-2: uma skill curta por stack, com o "não copiar" do catálogo e um checklist, e um Eval que reprova skill longa.
public class StackSkillTests
{
    private static readonly HarnessNaming Naming = HarnessNaming.Spock;

    private static TempProject Angular()
    {
        var project = new TempProject();
        project.Write("portal/package.json", "{\"name\":\"portal\",\"dependencies\":{\"@angular/core\":\"^19.0.0\"}}");
        return project;
    }

    private static async Task<(Journey Journey, System.Text.Json.Nodes.JsonObject Result)> Applied(TempProject project)
    {
        var journey = new Journey(project, Journey.FakeInstaller(project));
        var result = await journey.ApplyAsync(await journey.PlanAsync());
        return (journey, result);
    }

    [Fact]
    public async Task Projeto_Angular_recebe_a_skill_Angular_curta_valida_e_nenhuma_skill_React()
    {
        using var project = Angular();

        var (_, result) = await Applied(project);

        var skill = project.Read(Naming.StackSkill("angular"))!;
        Assert.StartsWith("---\nname: spock-stack-angular\ndescription: ", skill);
        Assert.Contains("## Do not copy", skill);
        Assert.Contains("Credenciais em query string de GET. Instead: POST com corpo.", skill);
        Assert.Contains("## Before finishing", skill);
        Assert.Contains("Routes stay lazy", skill);
        Assert.Contains(Naming.PatternRule("angular"), skill);
        Assert.True(skill.Length < StackSkillProfiles.MaxCharacters / 2, $"{skill.Length} caracteres");
        Assert.False(project.Exists(Naming.StackSkill("react")));
        Assert.Equal("passed", Journey.Status(result, EvalIds.Skill));
        Assert.Equal("passed", Journey.Status(result, EvalIds.SkillSize));
    }

    [Fact]
    public void Toda_skill_gerada_fica_bem_abaixo_do_limite_e_traz_os_anti_padroes_da_sua_stack()
    {
        foreach (var profile in StackSkillProfiles.All)
        {
            var skill = StackSkillProfiles.Render(Naming, profile);
            Assert.True(skill.Length < StackSkillProfiles.MaxCharacters / 2, $"{profile.Stack}: {skill.Length} caracteres");
            Assert.True(OrganizationReferences.AntiPatterns.Count(item => item.Technologies.Contains(profile.AntiPatternTechnology)) >= 3, profile.Stack);
            Assert.Equal(1, skill.Split('\n').Count(line => line == "## Do not copy"));
        }
        // A tabela .NET não tem a coluna "O que fazer": a linha termina no problema.
        Assert.Contains("- Repositório genérico com ordenação por `switch` de string e `SaveChanges` duplicado.\n",
            StackSkillProfiles.Render(Naming, StackSkillProfiles.Dotnet));
    }

    [Fact]
    public async Task Skill_longa_do_proprio_projeto_e_reprovada_sem_bloquear_a_aplicacao()
    {
        using var project = Angular();
        project.Write(".claude/skills/manual/SKILL.md",
            "---\nname: manual\ndescription: Tudo sobre o projeto.\n---\n\n" + new string('x', StackSkillProfiles.MaxCharacters + 1));

        var (_, result) = await Applied(project);

        Assert.Equal("failed", Journey.Status(result, EvalIds.SkillSize));
        Assert.NotEqual("failed", result["outcome"]!.GetValue<string>());
    }

    [Fact]
    public async Task Validacao_exige_a_skill_da_stack_que_o_plano_gera()
    {
        using var project = Angular();
        var journey = new Journey(project, Journey.FakeInstaller(project));
        var plan = await journey.PlanAsync();
        var all = Journey.ApproveAll(plan);
        var without = all with { ChangeIds = all.ChangeIds.Where(id => plan.Changes.Single(change => change.Id == id).Path != Naming.StackSkill("angular")).ToList() };

        Assert.Equal("failed", Journey.Status(await journey.ApplyAsync(plan, without), EvalIds.Requirements));
    }
}
