using Mariliz.Application.Specification;
using Mariliz.Application.Harness;
using Mariliz.Application.Ports;
using Mariliz.Application.Tests.Support;

namespace Mariliz.Application.Tests;

/// <summary>O produto sempre leva o projeto para a versão mais recente da estrutura de especificação, sem consultar a rede no plano.</summary>
public class SpecificationLatestVersionTests
{
    private const string SkillDir = ".claude/skills/tlc-spec-driven";

    /// <summary>O instalador oficial substitui a pasta da skill inteira: arquivos abolidos somem, edições locais voltam ao original.</summary>
    private static Func<ProcessSpec, CancellationToken, Task<ProcessResult?>> InstallerThatInstalls(TempProject project, string version) => (spec, _) =>
    {
        if (spec.Executable != "npx") return Task.FromResult<ProcessResult?>(null);
        var dir = Path.Combine(project.Root, ".claude", "skills", "tlc-spec-driven");
        if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        SpecificationFixture.Install(project, version);
        return Task.FromResult<ProcessResult?>(new ProcessResult(0, "ok", "", false, false));
    };

    [Theory]
    [InlineData("3.3.0", true)]
    [InlineData("3.9.9", true)]
    [InlineData("4.0.0", false)]
    [InlineData("4.1.0", false)]
    [InlineData("10.0.0", false)]
    [InlineData("abc", false)]
    [InlineData(null, false)]
    public void Desatualizado_so_quando_a_versao_e_legivel_e_anterior_a_mais_recente_conhecida(string? version, bool expected) =>
        Assert.Equal(expected, SpecificationManifest.IsOlderThanLatest(version));

    [Fact]
    public async Task Versao_antiga_e_proposta_para_atualizacao_com_o_comando_oficial_e_sem_ser_destrutiva()
    {
        using var project = TempProject.Dotnet();
        SpecificationFixture.Install(project, "3.3.0");
        var journey = new Journey(project);

        var plan = await journey.PlanAsync();

        var command = Assert.Single(plan.Commands);
        Assert.Equal("specification-update", command.Id);
        Assert.Equal("npx", command.Executable);
        Assert.Equal(["-y", "@tech-leads-club/agent-skills", "install", "-s", "tlc-spec-driven", "-a", "claude-code"], command.Arguments);
        Assert.False(command.Destructive);
        var specification = plan.Decisions.Single(d => d.ComponentId == "mariliz.specification.structure");
        Assert.Equal("enable", specification.Decision);
        Assert.Contains("4.0.0", specification.Justification);
    }

    [Fact]
    public async Task Versao_atual_nao_gera_comando_e_o_plano_continua_idempotente()
    {
        using var project = TempProject.Dotnet();
        SpecificationFixture.Install(project, "4.0.0");
        var journey = new Journey(project);

        var plan = await journey.PlanAsync();

        Assert.Empty(plan.Commands);
        Assert.Equal("keep", plan.Decisions.Single(d => d.ComponentId == "mariliz.specification.structure").Decision);
    }

    [Fact]
    public async Task Pedido_explicito_atualiza_mesmo_a_versao_atual_para_buscar_uma_mais_nova()
    {
        using var project = TempProject.Dotnet();
        SpecificationFixture.Install(project, "4.0.0");
        var journey = new Journey(project);

        var plan = await journey.Service.PlanAsync(journey.ProjectId, new PlanOptions("claude-code", UpdateSpecification: true), CancellationToken.None);

        Assert.Equal("specification-update", Assert.Single(plan.Commands).Id);
        Assert.Contains("mais recente", plan.Decisions.Single(d => d.ComponentId == "mariliz.specification.structure").Justification);
    }

    [Fact]
    public async Task Atualizacao_aprovada_leva_a_4_0_0_remove_arquivos_abolidos_e_passa_nos_evals()
    {
        using var project = TempProject.Dotnet();
        SpecificationFixture.Install(project, "3.3.0");
        Assert.True(project.Exists(SkillDir + "/scripts/validate_state.py"));
        var journey = new Journey(project, InstallerThatInstalls(project, "4.0.0"));

        var plan = await journey.PlanAsync();
        var result = await journey.ApplyAsync(plan, Journey.ApproveAll(plan));

        Assert.Equal("completed", result["outcome"]!.GetValue<string>());
        Assert.Single(journey.Runner.Calls, c => c.Executable == "npx");
        Assert.False(project.Exists(SkillDir + "/scripts/validate_state.py"));
        Assert.Equal("passed", Journey.Status(result, "EV-SPEC-001"));
        Assert.Contains("version: 4.0.0", project.Read("mariliz.lock")!);

        // Depois de atualizado, planejar de novo não propõe nada.
        Assert.Empty((await journey.PlanAsync()).Commands);
    }

    [Fact]
    public async Task Pedido_de_atualizacao_e_de_uso_unico_e_a_idempotencia_depois_de_aplicar_passa()
    {
        using var project = TempProject.Dotnet();
        SpecificationFixture.Install(project, "4.0.0");
        var journey = new Journey(project, InstallerThatInstalls(project, "4.0.0"));

        var plan = await journey.Service.PlanAsync(journey.ProjectId, new PlanOptions("claude-code", UpdateSpecification: true), CancellationToken.None);
        var result = await journey.ApplyAsync(plan, Journey.ApproveAll(plan));

        Assert.Equal("completed", result["outcome"]!.GetValue<string>());
        Assert.Equal("passed", Journey.Status(result, "EV-IDEMP-001"));
        Assert.Single(journey.Runner.Calls, c => c.Executable == "npx");
    }

    [Fact]
    public async Task Atualizacao_que_falha_restaura_a_versao_anterior_da_especificacao()
    {
        using var project = TempProject.Dotnet();
        SpecificationFixture.Install(project, "3.3.0");
        var before = project.Fingerprint();
        var journey = new Journey(project, (spec, _) =>
        {
            if (spec.Executable != "npx") return Task.FromResult<ProcessResult?>(null);
            // O instalador apaga a versão antiga, grava um pedaço da nova e morre.
            Directory.Delete(Path.Combine(project.Root, ".claude", "skills", "tlc-spec-driven"), recursive: true);
            project.Write(SkillDir + "/SKILL.md", "pela metade");
            return Task.FromResult<ProcessResult?>(new ProcessResult(1, "", "queda de rede", false, false));
        });

        var plan = await journey.PlanAsync();
        var result = await journey.ApplyAsync(plan, Journey.ApproveAll(plan));

        Assert.Equal("failed", result["outcome"]!.GetValue<string>());
        Assert.Contains("queda de rede", result["error"]!["message"]!.GetValue<string>());
        Assert.Equal(before, project.Fingerprint());
        Assert.Equal("Installed", SpecificationStateOf(project));
    }

    [Fact]
    public async Task Atualizacao_nao_aprovada_mantem_a_especificacao_atual_e_o_resultado_diz_que_ficou_pendente()
    {
        using var project = TempProject.Dotnet();
        SpecificationFixture.Install(project, "3.3.0");
        var journey = new Journey(project, InstallerThatInstalls(project, "4.0.0"));

        var plan = await journey.PlanAsync();
        var approvals = Journey.ApproveAll(plan) with { CommandIds = [] };
        var result = await journey.ApplyAsync(plan, approvals);

        Assert.DoesNotContain(journey.Runner.Calls, c => c.Executable == "npx");
        Assert.NotEqual("completed", result["outcome"]!.GetValue<string>());
        Assert.Equal("blocked", Journey.Status(result, "EV-IDEMP-001"));
        Assert.True(project.Exists(SkillDir + "/scripts/validate_state.py"));
    }

    [Fact]
    public async Task Atualizacao_que_cairia_fora_do_projeto_e_barrada_e_a_especificacao_atual_e_mantida()
    {
        using var project = TempProject.Dotnet();
        SpecificationFixture.Install(project, "3.3.0");
        var journey = new Journey(project, files: new TestFilesFactory(ancestorWithPackageJson: "home-do-usuario"));

        var plan = await journey.PlanAsync();

        Assert.Empty(plan.Commands);
        Assert.Equal("keep", plan.Decisions.Single(d => d.ComponentId == "mariliz.specification.structure").Decision);
        var limitation = plan.Limitations.Single(l => l.Code == "SPECIFICATION_OUTDATED");
        Assert.Contains("3.3.0", limitation.Message);
        Assert.Contains("home-do-usuario", limitation.Message);
    }

    [Fact]
    public async Task Especificacao_editada_localmente_nao_e_atualizado_sem_a_permissao_de_reinstalar()
    {
        using var project = TempProject.Dotnet();
        SpecificationFixture.Install(project, "3.3.0");
        File.AppendAllText(Path.Combine(project.Root, ".claude", "skills", "tlc-spec-driven", "references", "design.md"), "\nedição minha\n");
        var journey = new Journey(project);

        var plan = await journey.PlanAsync();

        Assert.Empty(plan.Commands);
        Assert.Contains(plan.Limitations, l => l.Code == "SPECIFICATION_NEEDS_REINSTALL");
    }

    private static string SpecificationStateOf(TempProject project)
    {
        var files = new Mariliz.Infrastructure.LocalProjectFiles(project.Root);
        return SpecificationInspector.Inspect(files).State.ToString();
    }
}
