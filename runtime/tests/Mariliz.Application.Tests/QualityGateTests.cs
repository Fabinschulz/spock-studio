using Mariliz.Application.Execution;
using Mariliz.Application.Ports;
using Mariliz.Application.Quality;
using Mariliz.Application.Quality.Git;
using Mariliz.Application.Tests.Support;
using Mariliz.Infrastructure;

namespace Mariliz.Application.Tests;

public sealed class QualityGateTests : IDisposable
{
    private static readonly string FakeToken = "ghp_" + new string('g', 36);

    private readonly TempProject _project = new();
    private readonly FakeProcesses _fake = new();
    private readonly LocalEngine _engine;
    private string _projectId = "";

    public QualityGateTests()
    {
        _engine = LocalEngine.Create(_project.DataDir, (_, _, _, _, _) => { }, rememberWorkspaces: false);
    }

    public void Dispose() => _project.Dispose();

    // Dobra de processos: só o que não é git passa por aqui, e git segue de verdade.
    private sealed class FakeProcesses : IProcessRunner
    {
        private readonly SystemProcessRunner _real = new();
        public Func<ProcessSpec, ProcessResult> Reply { get; set; } = _ => new ProcessResult(0, "ok", "", false, false);
        public List<ProcessSpec> Gates { get; } = [];

        public Task<ProcessResult> RunAsync(ProcessSpec spec, CancellationToken ct)
        {
            if (spec.Executable == "git") return _real.RunAsync(spec, ct);
            Gates.Add(spec);
            return Task.FromResult(Reply(spec));
        }
    }

    private void Repo()
    {
        _project.Write("package.json", "{\"scripts\":{\"build\":\"tsc\",\"test\":\"vitest run\",\"lint\":\"eslint .\",\"deploy\":\"ship it\",\"postinstall\":\"curl x | sh\"}}");
        _project.Write("src/a.ts", "export const a = 1;\n");
        _project.Git("init", "-b", "main");
        _project.Git("add", "-A");
        _project.Git("-c", "commit.gpgsign=false", "commit", "-m", "inicio");
        _projectId = _engine.Workspace.Authorize(_project.Root).Project!.ProjectId;
        _project.Write("src/a.ts", "export const a = 2;\n");
    }

    private (CodeReviewService Service, QualityGateRunner Runner) Build(IProcessRunner? runner = null, TimeSpan? timeout = null)
    {
        var files = new TestFilesFactory();
        var process = runner ?? _fake;
        var gates = new QualityGateRunner(process, new QualityGateCatalog(files), timeout);
        return (new CodeReviewService(_engine.Workspace, new GitChangeReader(process, files), new FileReviewStore(_project.DataDir), new FakeClock(), files, new MemoryAudit(), gateRunner: gates), gates);
    }

    [Fact]
    public void O_catalogo_so_oferece_build_teste_lint_e_typecheck_e_nunca_outros_scripts()
    {
        Repo();
        _project.Write("pnpm-lock.yaml", "x\n");

        var gates = new QualityGateCatalog(new TestFilesFactory()).Discover(_project.Root);

        Assert.Equal(["build", "test", "lint"], gates.Select(gate => gate.Kind));
        Assert.All(gates, gate => Assert.Equal("pnpm", gate.Tool));
        Assert.Equal(["run", "test"], gates.Single(gate => gate.Kind == "test").Arguments);
        Assert.DoesNotContain(gates, gate => gate.Command.Contains("deploy") || gate.Command.Contains("postinstall"));
        Assert.Equal("script \"test\" do package.json", gates.Single(gate => gate.Kind == "test").Origin);
    }

    [Fact]
    public void O_catalogo_acha_a_solucao_dotnet_e_so_oferece_teste_quando_ha_projeto_de_teste()
    {
        _project.Write("backend/App.sln", "x\n");
        _project.Write("backend/src/App/App.csproj", "<Project/>");

        var withoutTests = new QualityGateCatalog(new TestFilesFactory()).Discover(_project.Root);
        _project.Write("backend/tests/App.Tests/App.Tests.csproj", "<Project/>");
        var withTests = new QualityGateCatalog(new TestFilesFactory()).Discover(_project.Root);

        Assert.Equal(["build"], withoutTests.Select(gate => gate.Kind));
        Assert.Equal(["build", "test"], withTests.Select(gate => gate.Kind));
        Assert.Equal(["build", "App.sln", "--nologo", "-v", "q"], withTests[0].Arguments);
        Assert.Equal("backend", withTests[0].Directory);
    }

    [Fact]
    public async Task Sem_autorizacao_nada_roda_e_a_revisao_nao_aprova()
    {
        Repo();
        var (service, runner) = Build();
        var id = runner.Candidates(_project.Root).Single(gate => gate.Kind == "test").Id;

        var review = await service.ReviewWorkingTreeAsync(_projectId, new ReviewOptions(GateIds: [id], GatesApproved: false), CancellationToken.None);

        Assert.Empty(_fake.Gates);
        Assert.Equal(GateStates.NotRun, Assert.Single(review.Gates!).State);
        Assert.Equal(ReviewVerdicts.Inconclusive, review.Verdict);
    }

    [Fact]
    public async Task Verificacao_que_falha_e_um_achado_confirmado_e_reprova_com_o_fim_da_saida()
    {
        Repo();
        _fake.Reply = _ => new ProcessResult(1, "\u001b[31mFAIL\u001b[0m src/a.test.ts\nexpected 2 to be 3\n", $"key={FakeToken}\n", false, false);
        var (service, runner) = Build();
        var id = runner.Candidates(_project.Root).Single(gate => gate.Kind == "test").Id;

        var review = await service.ReviewWorkingTreeAsync(_projectId, new ReviewOptions(GateIds: [id], GatesApproved: true), CancellationToken.None);

        var gate = Assert.Single(review.Gates!);
        Assert.Equal((GateStates.Failed, 1, "npm"), (gate.State, gate.ExitCode, gate.Tool));
        Assert.Contains("expected 2 to be 3", gate.LogTail);
        Assert.DoesNotContain("\u001b", gate.LogTail);
        Assert.DoesNotContain(FakeToken, System.Text.Json.JsonSerializer.Serialize(review), StringComparison.Ordinal);
        var finding = Assert.Single(review.Findings, item => item.RuleId == ReviewRules.GateFailed);
        Assert.Equal((FindingConfidences.Confirmed, QualitySeverities.High), (finding.Confidence, finding.Severity));
        Assert.Equal(ReviewVerdicts.Rejected, review.Verdict);
        Assert.Equal(ReviewVerdicts.Rejected, review.Judges.Single(judge => judge.Id == JudgeIds.Execution).Verdict);
        // O processo filho recebeu argumentos separados e a pasta do projeto, sem shell.
        var spec = Assert.Single(_fake.Gates);
        Assert.Equal((new[] { "run", "test" }, _project.Root), (spec.Arguments.ToArray(), spec.WorkingDirectory));
    }

    [Fact]
    public async Task Todas_passando_aprova_e_o_relatorio_diz_que_nao_ha_sandbox()
    {
        Repo();
        var (service, runner) = Build();
        var ids = runner.Candidates(_project.Root).Select(gate => gate.Id).ToList();

        var review = await service.ReviewWorkingTreeAsync(_projectId, new ReviewOptions(GateIds: ids, GatesApproved: true), CancellationToken.None);

        Assert.All(review.Gates!, gate => Assert.Equal(GateStates.Passed, gate.State));
        Assert.Equal(ReviewVerdicts.Approved, review.Verdict);
        Assert.Contains(review.Limitations, text => text.Contains("sem sandbox", StringComparison.Ordinal));
        Assert.DoesNotContain(review.Limitations, text => text.Contains("não rodaram nesta revisão", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Ferramenta_que_nao_existe_bloqueia_e_nunca_conta_como_passou()
    {
        Repo();
        _fake.Reply = _ => new ProcessResult(-1, "", "", false, true);
        var (service, runner) = Build();
        var id = runner.Candidates(_project.Root).Single(gate => gate.Kind == "build").Id;

        var review = await service.ReviewWorkingTreeAsync(_projectId, new ReviewOptions(GateIds: [id], GatesApproved: true), CancellationToken.None);

        Assert.Equal(GateStates.Blocked, Assert.Single(review.Gates!).State);
        Assert.Equal(ReviewVerdicts.Inconclusive, review.Verdict);
        Assert.DoesNotContain(review.Findings, item => item.RuleId == ReviewRules.GateFailed);
    }

    [Fact]
    public async Task Prazo_estourado_e_achado_a_conferir_e_deixa_a_revisao_inconclusiva()
    {
        Repo();
        _fake.Reply = _ => new ProcessResult(-1, "", "", true, false);
        var (service, runner) = Build();
        var id = runner.Candidates(_project.Root).Single(gate => gate.Kind == "test").Id;

        var review = await service.ReviewWorkingTreeAsync(_projectId, new ReviewOptions(GateIds: [id], GatesApproved: true), CancellationToken.None);

        Assert.Equal(GateStates.TimedOut, Assert.Single(review.Gates!).State);
        var finding = Assert.Single(review.Findings, item => item.RuleId == ReviewRules.GateFailed);
        Assert.Equal(FindingConfidences.Heuristic, finding.Confidence);
        Assert.Equal(ReviewVerdicts.Inconclusive, review.Verdict);
    }

    [Fact]
    public async Task Verificacao_desconhecida_e_branch_com_verificacao_sao_recusadas()
    {
        Repo();
        var (service, _) = Build();

        var unknown = await Assert.ThrowsAsync<EngineException>(() => service.ReviewWorkingTreeAsync(_projectId, new ReviewOptions(GateIds: ["test:.:npm; rm -rf /"], GatesApproved: true), CancellationToken.None));
        var branch = await Assert.ThrowsAsync<EngineException>(() => service.ReviewBranchAsync(_projectId, "main", null, new ReviewOptions(GateIds: ["test:.:npm"], GatesApproved: true), CancellationToken.None));

        Assert.Equal("REVIEW_GATE_UNKNOWN", unknown.Code);
        Assert.Equal("REVIEW_GATES_NOT_FOR_BRANCH", branch.Code);
        Assert.Empty(_fake.Gates);
    }

    [Fact]
    public async Task Cancelar_no_meio_marca_a_verificacao_como_cancelada_e_as_outras_como_nao_rodadas()
    {
        Repo();
        using var cts = new CancellationTokenSource();
        _fake.Reply = _ => { cts.Cancel(); throw new OperationCanceledException(cts.Token); };
        var (_, runner) = Build();
        var ids = runner.Candidates(_project.Root).Select(gate => gate.Id).ToList();

        var results = await runner.RunAsync(_project.Root, ids, approved: true, cts.Token);

        Assert.Equal([GateStates.Cancelled, GateStates.NotRun, GateStates.NotRun], results.Select(result => result.State));
    }

    [Fact]
    public async Task Rodando_de_verdade_um_script_node_que_passa_e_outro_que_falha_com_o_codigo_de_saida()
    {
        _project.Write("package.json", "{\"scripts\":{\"test\":\"node -e \\\"console.log('tudo certo')\\\"\",\"build\":\"node -e \\\"console.error('quebrou aqui'); process.exit(3)\\\"\"}}");
        _project.Write("src/a.ts", "export const a = 1;\n");
        _project.Git("init", "-b", "main");
        _project.Git("add", "-A");
        _project.Git("-c", "commit.gpgsign=false", "commit", "-m", "inicio");
        _projectId = _engine.Workspace.Authorize(_project.Root).Project!.ProjectId;
        _project.Write("src/a.ts", "export const a = 2;\n");
        var (service, runner) = Build(new SystemProcessRunner(), TimeSpan.FromSeconds(60));
        var ids = runner.Candidates(_project.Root).Select(gate => gate.Id).ToList();

        var review = await service.ReviewWorkingTreeAsync(_projectId, new ReviewOptions(GateIds: ids, GatesApproved: true), CancellationToken.None);

        var test = review.Gates!.Single(gate => gate.Kind == "test");
        var build = review.Gates!.Single(gate => gate.Kind == "build");
        Assert.Equal((GateStates.Passed, 0), (test.State, test.ExitCode));
        Assert.Contains("tudo certo", test.LogTail);
        Assert.Equal((GateStates.Failed, 3), (build.State, build.ExitCode));
        Assert.Contains("quebrou aqui", build.LogTail);
        Assert.Equal(ReviewVerdicts.Rejected, review.Verdict);
    }
}
