using Mariliz.Application.Ports;
using Mariliz.Application.Tests.Support;
using Mariliz.Core.Discovery;
using Mariliz.Infrastructure;

namespace Mariliz.Application.Tests;

/// <summary>
/// Achados da primeira execução do instalador oficial da estrutura de especificação de verdade: ele instala no package.json mais próximo
/// (possivelmente fora do projeto), a versão atual é a 4.0.0, e a saída dele precisa chegar à pessoa.
/// </summary>
public class SpecificationInstallerRealityTests
{
    [Fact]
    public async Task Instalador_que_gravaria_fora_do_projeto_e_bloqueado_antes_de_executar()
    {
        using var project = TempProject.Dotnet();
        var journey = new Journey(project, files: new TestFilesFactory(ancestorWithPackageJson: "home-do-usuario"));

        var plan = await journey.PlanAsync();
        var result = await journey.ApplyAsync(plan, Journey.ApproveAll(plan));

        Assert.Empty(plan.Commands);
        var specification = plan.Decisions.Single(d => d.ComponentId == "mariliz.specification.structure");
        Assert.Equal("blocked", specification.Decision);
        Assert.Contains("home-do-usuario", specification.Justification);
        var limitation = plan.Limitations.Single(l => l.Code == "SPECIFICATION_BLOCKED");
        Assert.Contains("fora do projeto", limitation.Message);
        Assert.Contains("package.json", limitation.Message);
        Assert.DoesNotContain(journey.Runner.Calls, c => c.Executable == "npx");
        Assert.Equal("blocked", Journey.Status(result, "EV-SPEC-001"));
        Assert.NotEqual("completed", result["outcome"]!.GetValue<string>());
    }

    [Fact]
    public async Task Projeto_com_package_json_proprio_instala_normalmente_mesmo_com_um_acima()
    {
        using var project = TempProject.Dotnet();
        project.Write("package.json", "{\"name\":\"app\",\"private\":true}");
        var journey = new Journey(project, Journey.FakeInstaller(project), new TestFilesFactory(ancestorWithPackageJson: "home-do-usuario"));

        var plan = await journey.PlanAsync();

        var command = Assert.Single(plan.Commands);
        Assert.Equal("specification-install", command.Id);
        Assert.DoesNotContain(plan.Limitations, l => l.Code == "SPECIFICATION_BLOCKED");
    }

    [Fact]
    public void Arquivos_locais_acham_a_pasta_acima_com_package_json_e_devolvem_so_o_nome()
    {
        var outer = Directory.CreateTempSubdirectory("mariliz-outer-").FullName;
        try
        {
            File.WriteAllText(Path.Combine(outer, "package.json"), "{}");
            var inner = Directory.CreateDirectory(Path.Combine(outer, "a", "projeto")).FullName;

            var found = new LocalProjectFiles(inner).NearestAncestorFolderWith("package.json");

            Assert.Equal(Path.GetFileName(outer), found);
            Assert.DoesNotContain(Path.DirectorySeparatorChar, found!);
            Assert.Null(new LocalProjectFiles(inner).NearestAncestorFolderWith("arquivo-que-nao-existe-" + Guid.NewGuid().ToString("N")));
        }
        finally { Directory.Delete(outer, recursive: true); }
    }

    [Fact]
    public async Task Layout_da_versao_4_0_0_e_reconhecido_como_instalado_e_validado()
    {
        using var project = TempProject.Dotnet();
        SpecificationFixture.Install(project, "4.0.0");
        var journey = new Journey(project);

        var report = await journey.Service.DiscoverAsync(journey.ProjectId, CancellationToken.None);

        Assert.Equal(SpecificationState.Installed, report.Specification.State);
        Assert.Equal("4.0.0", report.Specification.Version);
        Assert.True(report.Specification.VersionTested);
        Assert.Empty(report.Specification.Problems);
        Assert.False(project.Exists(".claude/skills/tlc-spec-driven/scripts/validate_state.py"));
    }

    [Fact]
    public async Task Lock_registra_a_versao_real_da_especificacao_instalada()
    {
        using var project = TempProject.Dotnet();
        SpecificationFixture.Install(project, "4.0.0");
        var journey = new Journey(project);

        var result = await journey.ApplyAsync(await journey.PlanAsync());

        Assert.Equal("completed", result["outcome"]!.GetValue<string>());
        var lockText = project.Read("mariliz.lock")!;
        Assert.Contains("version: 4.0.0", lockText);
        Assert.DoesNotContain("3.3.0", lockText);
    }

    [Fact]
    public async Task Falha_do_instalador_mostra_o_final_da_saida_e_omite_credenciais()
    {
        using var project = TempProject.Dotnet();
        var before = project.Fingerprint();
        const string token = "ghp_abcdefghijklmnopqrstuvwxyz0123456789";
        var journey = new Journey(project, (spec, _) => Task.FromResult<ProcessResult?>(spec.Executable == "npx"
            ? new ProcessResult(1, "", $"npm error 404 not found\ntoken={token}\nTHE-LAST-LINE", false, false)
            : null));

        var result = await journey.ApplyAsync(await journey.PlanAsync());

        Assert.Equal("failed", result["outcome"]!.GetValue<string>());
        var message = result["error"]!["message"]!.GetValue<string>();
        Assert.Contains("código 1", message);
        Assert.Contains("npm error 404 not found", message);
        Assert.Contains("THE-LAST-LINE", message);
        Assert.DoesNotContain(token, message);
        Assert.Equal(before, project.Fingerprint());

        var journalText = string.Join("\n", Directory.EnumerateFiles(project.DataDir, "*.json", SearchOption.AllDirectories).Select(File.ReadAllText));
        Assert.DoesNotContain(token, journalText);
    }

    [Fact]
    public async Task Instalador_sem_nenhuma_saida_diz_isso_em_vez_de_prometer_detalhes()
    {
        using var project = TempProject.Dotnet();
        var journey = new Journey(project, (spec, _) => Task.FromResult<ProcessResult?>(spec.Executable == "npx" ? new ProcessResult(1, "", "", false, false) : null));

        var result = await journey.ApplyAsync(await journey.PlanAsync());

        var message = result["error"]!["message"]!.GetValue<string>();
        Assert.Contains("não escreveu nenhuma saída", message);
        Assert.DoesNotContain("guardados no diário", message);
    }
}
