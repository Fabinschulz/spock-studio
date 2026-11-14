using System.Text.Json;
using System.Text.Json.Nodes;
using Mariliz.Application.AiTools;
using Mariliz.Application.Execution;
using Mariliz.Application.Ports;
using Mariliz.Application.Tests.Support;
using Mariliz.Application.Workspace;
using Mariliz.Infrastructure;

namespace Mariliz.Application.Tests;

public sealed class AgentRunTests : IDisposable
{
    private const string ClaudePath = @"C:\tools\claude.cmd";
    private readonly TempProject _project = new();
    private readonly WorkspaceAuthorizer _workspace = new(new LocalWorkspaceFileSystem());
    private readonly FileRunStore _store;
    private readonly RunTracker _tracker = new();
    private readonly string _projectId;

    public AgentRunTests()
    {
        _store = new FileRunStore(_project.DataDir);
        _projectId = _workspace.Authorize(_project.Root).Project!.ProjectId;
    }

    public void Dispose() => _project.Dispose();

    private sealed class FakeLocator(Dictionary<string, string> installed) : IExecutableLocator
    {
        public string? Find(string name) => installed.GetValueOrDefault(name);
    }

    /// <summary>Uma CLI de mentira: devolve as linhas pela saída ao vivo e depois o resultado.</summary>
    private sealed class FakeCli(string[] lines, ProcessResult result, TaskCompletionSource? hold = null) : IProcessRunner
    {
        public ProcessSpec? Spec { get; private set; }

        public async Task<ProcessResult> RunAsync(ProcessSpec spec, CancellationToken ct)
        {
            Spec = spec;
            foreach (var line in lines) spec.OnOutputLine?.Invoke(line);
            if (hold is not null) await hold.Task.WaitAsync(ct);
            return result;
        }
    }

    private AgentRunService Service(IProcessRunner runner, string? installed = "claude") =>
        new(new FakeLocator(installed is null ? [] : new() { [installed] = ClaudePath }), runner, _workspace, _tracker, _store, new SystemClock());

    private static ProcessResult Exit(int code, string stdout = "") => new(code, stdout, "", false, false);

    private static async Task<RunHandle> Finished(RunHandle run)
    {
        await run.Completion!;
        return run;
    }

    [Fact]
    public async Task Roda_so_para_leitura_na_pasta_do_projeto_e_manda_o_pedido_pela_entrada_padrao()
    {
        var cli = new FakeCli([], Exit(0, "resposta"));

        await Finished(Service(cli).Start(_projectId, "claude-code", "  O que este projeto faz?  "));

        var spec = cli.Spec!;
        Assert.Equal(ClaudePath, spec.Executable);
        Assert.Equal(_project.Root, spec.WorkingDirectory);
        Assert.Equal("O que este projeto faz?", spec.StandardInput);
        Assert.Contains("plan", spec.Arguments);
        Assert.Contains("Edit,Write,NotebookEdit,Bash", spec.Arguments);
        Assert.DoesNotContain(spec.Arguments, arg => arg.Contains("dangerously", StringComparison.Ordinal));
        Assert.DoesNotContain("O que este projeto faz?", spec.Arguments);
    }

    [Fact]
    public void Cada_ferramenta_tem_a_trava_de_leitura_da_propria_cli()
    {
        string[] Args(string id) => [.. AiToolCatalog.All.Single(tool => tool.Id == id).ReadOnly!.ArgumentsFor("/proj", "pedido")];

        Assert.Contains("read-only", Args("codex"));
        Assert.Equal("/proj", Args("codex")[Array.IndexOf(Args("codex"), "-C") + 1]);
        Assert.Contains("enabled", Args("cursor"));
        Assert.DoesNotContain("--force", Args("cursor"));
        Assert.Equal(["-p", "pedido"], Args(AiToolCatalog.Gemini));
        Assert.All(AiToolCatalog.All, tool => Assert.DoesNotContain(tool.ReadOnly!.ArgumentsFor("/p", "x"), arg => arg is "--yolo" or "--force"));
    }

    [Fact]
    public async Task A_saida_aparece_ao_vivo_sem_segredos_e_a_execucao_fica_no_diario()
    {
        var cli = new FakeCli(["Lendo o projeto", "token=ghp_abcdefghijklmnopqrstuvwxyz0123456789"], Exit(0, "Este projeto é uma API.\n"));

        var run = await Finished(Service(cli).Start(_projectId, "claude-code", "Resuma"));

        var lines = run.Snapshot(null)["events"]!.AsArray()
            .Where(evt => evt!["type"]!.GetValue<string>() == AgentRunService.OutputEvent)
            .Select(evt => evt!["data"]!["line"]!.GetValue<string>())
            .ToList();
        Assert.Equal("Lendo o projeto", lines[0]);
        Assert.DoesNotContain("ghp_abcdefghijklmnopqrstuvwxyz0123456789", lines[1]);
        Assert.Equal(RunStates.Completed, run.State);
        Assert.Equal("Este projeto é uma API.", run.Result!["answer"]!.GetValue<string>());

        var journal = _store.LoadJournal(_projectId, run.RunId)!;
        Assert.Equal(RunKinds.Agent, journal.Kind);
        Assert.Equal(RunStates.Completed, journal.State);
        Assert.NotNull(journal.FinishedAt);
        Assert.Contains(_store.LoadEvents(_projectId, run.RunId), evt => evt["type"]!.GetValue<string>() == AgentRunService.OutputEvent);
    }

    [Theory]
    [InlineData(2, false, "AGENT_FAILED")]
    [InlineData(-1, true, "AGENT_TIMEOUT")]
    public async Task Falha_ou_tempo_esgotado_ficam_registrados_com_o_motivo(int exitCode, bool timedOut, string error)
    {
        var cli = new FakeCli([], new ProcessResult(exitCode, "", "erro", timedOut, false));

        var run = await Finished(Service(cli).Start(_projectId, "claude-code", "Resuma"));

        Assert.Equal(RunStates.Failed, run.State);
        Assert.Equal(error, run.Result!["error"]!.GetValue<string>());
        Assert.Equal(error, _store.LoadJournal(_projectId, run.RunId)!.Error);
    }

    [Fact]
    public async Task Cancelar_encerra_e_registra_como_cancelada()
    {
        var cli = new FakeCli(["começou"], Exit(0), new TaskCompletionSource());
        var run = Service(cli).Start(_projectId, "claude-code", "Resuma");

        Assert.True(_tracker.Cancel(run.RunId));
        await run.Completion!;

        Assert.Equal(RunStates.Cancelled, run.State);
        Assert.Equal(RunStates.Cancelled, _store.LoadJournal(_projectId, run.RunId)!.State);
    }

    [Fact]
    public async Task Saida_sem_fim_nao_enche_o_diario()
    {
        var flood = Enumerable.Range(0, AgentRunService.MaxStreamedLines + 50).Select(i => $"linha {i}").ToArray();

        var run = await Finished(Service(new FakeCli(flood, Exit(0))).Start(_projectId, "claude-code", "Resuma"));

        var streamed = _store.LoadEvents(_projectId, run.RunId).Count(evt => evt["type"]!.GetValue<string>() == AgentRunService.OutputEvent);
        Assert.Equal(AgentRunService.MaxStreamedLines + 1, streamed);
    }

    [Fact]
    public void Recusa_o_que_nao_da_para_rodar_antes_de_iniciar()
    {
        var cli = new FakeCli([], Exit(0));

        Assert.Equal("AGENT_NOT_INSTALLED",
            Assert.Throws<EngineException>(() => Service(cli, installed: null).Start(_projectId, "claude-code", "x")).Code);
        Assert.Equal("PROJECT_NOT_AUTHORIZED",
            Assert.Throws<EngineException>(() => Service(cli).Start("p-000000000000", "claude-code", "x")).Code);
        Assert.ThrowsAny<Exception>(() => Service(cli).Start(_projectId, "desconhecida", "x"));
        Assert.ThrowsAny<Exception>(() => Service(cli).Start(_projectId, "claude-code", "   "));
        Assert.ThrowsAny<Exception>(() => Service(cli).Start(_projectId, "claude-code", new string('x', AgentRunService.MaxPromptChars + 1)));
        Assert.Null(cli.Spec);
    }

    [Fact]
    public async Task Uma_execucao_por_projeto_por_vez()
    {
        var hold = new TaskCompletionSource();
        var service = Service(new FakeCli([], Exit(0), hold));
        var first = service.Start(_projectId, "claude-code", "Resuma");

        Assert.Equal("RUN_IN_PROGRESS", Assert.Throws<EngineException>(() => service.Start(_projectId, "claude-code", "Outra")).Code);

        hold.SetResult();
        await first.Completion!;
    }

    [Fact]
    public async Task O_executor_de_processos_entrega_cada_linha_assim_que_ela_sai()
    {
        var lines = new List<string>();
        var (shell, args) = OperatingSystem.IsWindows()
            ? ("cmd.exe", new[] { "/c", "echo primeira& echo segunda" })
            : ("/bin/sh", new[] { "-c", "echo primeira; echo segunda" });

        var result = await new SystemProcessRunner().RunAsync(
            new ProcessSpec(shell, args, _project.Root, TimeSpan.FromSeconds(30), OnOutputLine: line => { lock (lines) lines.Add(line.Trim()); }),
            CancellationToken.None);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(["primeira", "segunda"], lines);
    }

    [Fact]
    public async Task Fixture_de_contrato_do_resultado_de_um_agente()
    {
        var run = await Finished(Service(new FakeCli(["lendo"], Exit(0, "Resposta do agente."))).Start(_projectId, "claude-code", "Resuma"));

        var pretty = run.Result!.ToJsonString(new JsonSerializerOptions
        {
            WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        }).ReplaceLineEndings("\n");

        var path = ContractFiles.Path_("fixtures", "agents", "run-result.json");
        if (Environment.GetEnvironmentVariable("MARILIZ_UPDATE_GOLDEN") == "1" || !File.Exists(path))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, pretty + "\n");
            return;
        }

        Assert.True(pretty == File.ReadAllText(path).ReplaceLineEndings("\n").TrimEnd(),
            "agent.run: o resultado mudou. Se foi deliberado, regenere com MARILIZ_UPDATE_GOLDEN=1 e ajuste o Zod da interface.");
    }
}
