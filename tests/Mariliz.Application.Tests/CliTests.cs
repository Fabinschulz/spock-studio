using System.Text.Json.Nodes;
using Mariliz.Application.Execution;
using Mariliz.Application.Harness;
using Mariliz.Application.Ports;
using Mariliz.Application.Tests.Support;
using Mariliz.Application.Workspace;
using Mariliz.Cli;
using Mariliz.Infrastructure;

namespace Mariliz.Application.Tests;

/// <summary>A CLI usa o mesmo serviço da interface; estes testes provam as aprovações explícitas e o relato honesto.</summary>
public class CliTests
{
    private sealed class CliHarness : IDisposable
    {
        private CliRunner? _runner;
        public TempProject Project { get; }
        public InterceptingRunner Processes { get; }
        public string Out { get; private set; } = "";
        public string Err { get; private set; } = "";
        private readonly ConfigureProjectService _service;
        private readonly WorkspaceAuthorizer _workspace = new(new LocalWorkspaceFileSystem());

        public CliHarness(TempProject project, Func<ProcessSpec, CancellationToken, Task<ProcessResult?>>? intercept = null)
        {
            Project = project;
            Processes = new InterceptingRunner(intercept);
            var tracker = new RunTracker((runId, seq, type, data) => _runner?.OnEvent(runId, seq, type, data));
            _service = new ConfigureProjectService(_workspace, new TestFilesFactory(), Processes,
                new FileRunStore(project.DataDir), new SystemClock(), tracker);
        }

        public async Task<int> RunAsync(string input, bool interactive, params string[] args)
        {
            var stdout = new StringWriter();
            var stderr = new StringWriter();
            _runner = new CliRunner(_service, _workspace, new StringReader(input), stdout, stderr, interactive);
            var code = await _runner.RunAsync(args, CancellationToken.None);
            Out = stdout.ToString();
            Err = stderr.ToString();
            return code;
        }

        public Task<int> RunAsync(params string[] args) => RunAsync("", false, args);

        public void Dispose() => Project.Dispose();
    }

    private static CliHarness WithSpecification()
    {
        var project = TempProject.Dotnet();
        SpecificationFixture.Install(project);
        return new CliHarness(project);
    }

    [Fact]
    public async Task Descobrir_so_le_e_mostra_stack_e_estado_da_especificacao()
    {
        using var cli = WithSpecification();
        var before = cli.Project.Fingerprint();

        var code = await cli.RunAsync("discover", cli.Project.Root);

        Assert.Equal(CliRunner.ExitOk, code);
        Assert.Contains(".NET", cli.Out);
        Assert.Contains("Estrutura de especificação: installed", cli.Out);
        Assert.Contains("somente de leitura", cli.Out);
        Assert.Equal(before, cli.Project.Fingerprint());
    }

    [Fact]
    public async Task Saida_json_e_so_json_e_igual_ao_contrato_do_motor()
    {
        using var cli = WithSpecification();

        Assert.Equal(CliRunner.ExitOk, await cli.RunAsync("plan", cli.Project.Root, "--env", "claude-code", "--json"));

        var plan = JsonNode.Parse(cli.Out)!;
        Assert.StartsWith("plan-", plan["planId"]!.GetValue<string>());
        Assert.Equal("claude-code", plan["aiEnvironment"]!.GetValue<string>());
        Assert.Empty(cli.Err);
    }

    [Fact]
    public async Task Plano_lista_decisoes_arquivos_e_comando_sem_gravar_nada()
    {
        var project = TempProject.Dotnet();
        using var cli = new CliHarness(project);
        var before = project.Fingerprint();

        var code = await cli.RunAsync("plan", project.Root, "--env", "claude-code");

        Assert.Equal(CliRunner.ExitOk, code);
        Assert.Contains("mariliz.agent.harness-engineer", cli.Out);
        Assert.Contains("CREATE", cli.Out);
        Assert.Contains("specification-install: npx -y @tech-leads-club/agent-skills install -s tlc-spec-driven -a claude-code", cli.Out);
        Assert.Equal(before, project.Fingerprint());
        Assert.DoesNotContain(cli.Processes.Calls, c => c.Executable == "npx");
    }

    [Fact]
    public async Task Aplicar_sem_terminal_e_sem_yes_recusa_e_nao_grava()
    {
        using var cli = WithSpecification();
        var before = cli.Project.Fingerprint();

        var code = await cli.RunAsync("apply", cli.Project.Root, "--env", "claude-code");

        Assert.Equal(CliRunner.ExitUsage, code);
        Assert.Contains("--yes", cli.Err);
        Assert.Equal(before, cli.Project.Fingerprint());
    }

    [Fact]
    public async Task Aplicar_com_yes_cria_a_configuracao_e_relata_os_evals()
    {
        using var cli = WithSpecification();

        var code = await cli.RunAsync("apply", cli.Project.Root, "--env", "claude-code", "--yes");

        Assert.Equal(CliRunner.ExitOk, code);
        Assert.True(cli.Project.Exists("mariliz.lock"));
        Assert.True(cli.Project.Exists(".claude/agents/harness-engineer.md"));
        Assert.Contains("concluído; todas as verificações obrigatórias passaram", cli.Out);
        Assert.Contains("passed   EV-CONFIG-001", cli.Out);
        Assert.Contains("not_run  EV-JUDGE-001", cli.Out);
        Assert.Contains("Você está aprovando", cli.Out);
    }

    [Fact]
    public async Task Confirmacao_interativa_recusada_nao_grava_nada()
    {
        using var cli = WithSpecification();
        var before = cli.Project.Fingerprint();

        var code = await cli.RunAsync("n\n", true, "apply", cli.Project.Root, "--env", "claude-code");

        Assert.Equal(CliRunner.ExitFailed, code);
        Assert.Contains("Nada foi alterado", cli.Err);
        Assert.Equal(before, cli.Project.Fingerprint());
    }

    [Fact]
    public async Task Confirmacao_interativa_aceita_aplica()
    {
        using var cli = WithSpecification();

        var code = await cli.RunAsync("s\n", true, "apply", cli.Project.Root, "--env", "claude-code");

        Assert.Equal(CliRunner.ExitOk, code);
        Assert.True(cli.Project.Exists("mariliz.lock"));
    }

    [Fact]
    public async Task Comando_de_instalacao_da_especificacao_so_roda_quando_o_id_e_aprovado()
    {
        var project = TempProject.Dotnet();
        using var cli = new CliHarness(project, Journey.FakeInstaller(project));

        await cli.RunAsync("apply", project.Root, "--env", "claude-code", "--yes");
        Assert.DoesNotContain(cli.Processes.Calls, c => c.Executable == "npx");
        Assert.Contains("comando NÃO aprovado (use --approve-command specification-install)", cli.Out);

        var code = await cli.RunAsync("apply", project.Root, "--env", "claude-code", "--approve-command", "specification-install", "--yes");

        Assert.Single(cli.Processes.Calls, c => c.Executable == "npx");
        Assert.Contains("executar o comando specification-install", cli.Out);
        Assert.True(code is CliRunner.ExitOk or CliRunner.ExitLimited, cli.Out + cli.Err);
        Assert.True(project.Exists(".claude/skills/tlc-spec-driven/SKILL.md"));
    }

    [Fact]
    public async Task Comando_inexistente_no_plano_e_erro_de_uso()
    {
        using var cli = WithSpecification();

        var code = await cli.RunAsync("apply", cli.Project.Root, "--env", "claude-code", "--approve-command", "rm-rf", "--yes");

        Assert.Equal(CliRunner.ExitUsage, code);
        Assert.Contains("o plano não tem esse comando", cli.Err);
        Assert.False(cli.Project.Exists("mariliz.lock"));
    }

    [Fact]
    public async Task Conflito_mantem_o_arquivo_do_usuario_por_padrao()
    {
        using var cli = WithSpecification();
        const string mine = "---\nname: code-reviewer\ndescription: o meu\ntools: Read\n---\nmeu revisor\n";
        cli.Project.Write(".claude/agents/code-reviewer.md", mine);

        await cli.RunAsync("apply", cli.Project.Root, "--env", "claude-code", "--yes");

        Assert.Equal(mine, cli.Project.Read(".claude/agents/code-reviewer.md"));
        Assert.Contains("manter o seu arquivo", cli.Out);
    }

    [Fact]
    public async Task Substituir_conflito_exige_a_opcao_explicita_e_guarda_backup_que_a_reversao_restaura()
    {
        using var cli = WithSpecification();
        const string mine = "---\nname: code-reviewer\ndescription: o meu\ntools: Read\n---\nmeu revisor\n";
        cli.Project.Write(".claude/agents/code-reviewer.md", mine);
        var before = cli.Project.Fingerprint();

        await cli.RunAsync("apply", cli.Project.Root, "--env", "claude-code", "--overwrite", ".claude/agents/code-reviewer.md", "--yes");
        Assert.NotEqual(mine, cli.Project.Read(".claude/agents/code-reviewer.md"));
        Assert.Contains("substituir (o seu fica em backup)", cli.Out);

        var code = await cli.RunAsync("revert", cli.Project.Root, "--yes");

        Assert.Equal(CliRunner.ExitOk, code);
        Assert.Equal(mine, cli.Project.Read(".claude/agents/code-reviewer.md"));
        Assert.Equal(before, cli.Project.Fingerprint());
        Assert.Contains("conferidos", cli.Out);
    }

    [Fact]
    public async Task Substituir_um_caminho_sem_conflito_e_erro_de_uso()
    {
        using var cli = WithSpecification();

        var code = await cli.RunAsync("apply", cli.Project.Root, "--env", "claude-code", "--overwrite", "mariliz.yaml", "--yes");

        Assert.Equal(CliRunner.ExitUsage, code);
        Assert.False(cli.Project.Exists("mariliz.lock"));
    }

    [Fact]
    public async Task Validar_apos_aplicar_passa_e_o_json_traz_o_resultado()
    {
        using var cli = WithSpecification();
        await cli.RunAsync("apply", cli.Project.Root, "--env", "claude-code", "--yes");

        var code = await cli.RunAsync("validate", cli.Project.Root, "--json");

        Assert.Equal(CliRunner.ExitOk, code);
        var result = JsonNode.Parse(cli.Out)!;
        Assert.Equal("validate", result["kind"]!.GetValue<string>());
        Assert.Equal("completed", result["outcome"]!.GetValue<string>());
    }

    [Fact]
    public async Task Reverter_sem_aplicacao_previa_diz_que_nao_ha_o_que_reverter()
    {
        using var cli = WithSpecification();

        var code = await cli.RunAsync("revert", cli.Project.Root, "--yes");

        Assert.Equal(CliRunner.ExitFailed, code);
        Assert.Contains("NOTHING_TO_REVERT", cli.Err);
    }

    [Fact]
    public async Task Pasta_inexistente_e_erro_claro()
    {
        using var cli = WithSpecification();

        var code = await cli.RunAsync("discover", Path.Combine(cli.Project.Root, "nao-existe"));

        Assert.Equal(CliRunner.ExitFailed, code);
        Assert.Contains("PATH_NOT_FOUND", cli.Err);
    }

    [Theory]
    [InlineData("explodir")]
    [InlineData("discover")]
    [InlineData("discover", "x", "y")]
    [InlineData("plan", "x", "--desconhecida")]
    [InlineData("plan", "x", "--env")]
    public async Task Uso_incorreto_devolve_codigo_de_uso_e_a_ajuda(params string[] args)
    {
        using var cli = WithSpecification();

        var code = await cli.RunAsync(args);

        Assert.Equal(CliRunner.ExitUsage, code);
        Assert.Contains("Uso:", cli.Err);
    }

    [Fact]
    public async Task Especificacao_antiga_vira_o_comando_de_atualizacao_e_so_roda_com_a_aprovacao_dele()
    {
        var project = TempProject.Dotnet();
        SpecificationFixture.Install(project, "3.3.0");
        using var cli = new CliHarness(project, (spec, _) =>
        {
            if (spec.Executable != "npx") return Task.FromResult<ProcessResult?>(null);
            Directory.Delete(Path.Combine(project.Root, ".claude", "skills", "tlc-spec-driven"), recursive: true);
            SpecificationFixture.Install(project, "4.0.0");
            return Task.FromResult<ProcessResult?>(new ProcessResult(0, "", "", false, false));
        });

        await cli.RunAsync("apply", project.Root, "--env", "claude-code", "--yes");
        Assert.DoesNotContain(cli.Processes.Calls, c => c.Executable == "npx");
        Assert.Contains("comando NÃO aprovado (use --approve-command specification-update)", cli.Out);

        var code = await cli.RunAsync("apply", project.Root, "--env", "claude-code", "--approve-command", "specification-update", "--yes");

        Assert.Equal(CliRunner.ExitOk, code);
        Assert.Single(cli.Processes.Calls, c => c.Executable == "npx");
        Assert.Contains("version: 4.0.0", project.Read("mariliz.lock")!);
    }

    [Fact]
    public async Task Update_specification_pede_a_versao_mais_nova_mesmo_com_a_atual_instalada()
    {
        using var cli = WithSpecification();

        await cli.RunAsync("plan", cli.Project.Root, "--env", "claude-code");
        Assert.DoesNotContain("specification-update", cli.Out);

        Assert.Equal(CliRunner.ExitOk, await cli.RunAsync("plan", cli.Project.Root, "--env", "claude-code", "--update-specification"));
        Assert.Contains("specification-update: npx -y @tech-leads-club/agent-skills install -s tlc-spec-driven -a claude-code", cli.Out);
    }
}
