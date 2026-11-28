using Mariliz.Application.Audit;
using Mariliz.Application.Execution;
using Mariliz.Application.Quality;
using Mariliz.Application.Quality.Git;
using Mariliz.Application.Tests.Support;
using Mariliz.Infrastructure;

namespace Mariliz.Application.Tests;

public sealed class ReviewDecisionTests : IDisposable
{
    private static readonly string FakeToken = "ghp_" + new string('d', 36);

    private readonly TempProject _project = new();
    private readonly MemoryAudit _audit = new();
    private readonly FakeClock _clock = new();
    private readonly string _projectId;
    private readonly CodeReviewService _reviews;
    private readonly ReviewDecisionService _decisions;

    public ReviewDecisionTests()
    {
        var engine = LocalEngine.Create(_project.DataDir, (_, _, _, _, _) => { }, rememberWorkspaces: false);
        _project.Write("README.md", "# x\n");
        _project.Git("init", "-b", "main");
        _project.Git("add", "-A");
        _project.Git("-c", "commit.gpgsign=false", "commit", "-m", "inicio");
        _projectId = engine.Workspace.Authorize(_project.Root).Project!.ProjectId;
        var files = new TestFilesFactory();
        var store = new FileReviewStore(_project.DataDir);
        _reviews = new CodeReviewService(engine.Workspace, new GitChangeReader(new SystemProcessRunner(), files), store, _clock, files, _audit);
        _decisions = new ReviewDecisionService(engine.Workspace, store, _clock, _audit);
    }

    public void Dispose() => _project.Dispose();

    private async Task<ReviewRecord> ReviewWithSecretAsync()
    {
        _project.Write("src/k.txt", $"key={FakeToken}\n");
        return await _reviews.ReviewWorkingTreeAsync(_projectId, CancellationToken.None);
    }

    [Fact]
    public async Task Achado_bloqueante_em_aberto_impede_aprovar_a_entrega()
    {
        var review = await ReviewWithSecretAsync();

        var view = _decisions.View(_projectId, review);

        Assert.Equal((1, false), (view.Status.OpenBlocking, view.Status.ReadyForDelivery));
        var error = Assert.Throws<EngineException>(() => _decisions.DecideDelivery(_projectId, review.Id, DeliveryDecisions.Approved, "pode seguir, conferi tudo"));
        Assert.Equal("REVIEW_NOT_READY", error.Code);
    }

    [Fact]
    public async Task Descartar_como_falso_positivo_exige_justificativa_e_nao_apaga_o_achado()
    {
        var review = await ReviewWithSecretAsync();
        var finding = Assert.Single(review.Findings);

        Assert.Equal("REVIEW_JUSTIFICATION_INVALID", Assert.Throws<EngineException>(() => _decisions.DecideFinding(_projectId, review.Id, finding.Id, FindingDecisions.FalsePositive, "curto")).Code);
        var view = _decisions.DecideFinding(_projectId, review.Id, finding.Id, FindingDecisions.FalsePositive, "chave de exemplo do guia de instalação, inventada");

        Assert.Single(view.Review.Findings);
        Assert.Equal(ReviewVerdicts.Rejected, view.Review.Verdict);
        Assert.Equal((0, true), (view.Status.OpenBlocking, view.Status.ReadyForDelivery));
        Assert.Equal(FindingDecisions.FalsePositive, Assert.Single(view.Decisions).Decision);
        Assert.Contains(_audit.Entries, entry => entry.Action == AuditActions.QualityFindingDecided);
    }

    [Fact]
    public async Task Risco_critico_nao_se_aceita_mas_se_descarta_com_motivo()
    {
        var review = await ReviewWithSecretAsync();
        var finding = Assert.Single(review.Findings);
        Assert.Equal(QualitySeverities.Critical, finding.Severity);

        var error = Assert.Throws<EngineException>(() => _decisions.DecideFinding(_projectId, review.Id, finding.Id, FindingDecisions.AcceptedRisk, "aceitamos por enquanto"));

        Assert.Equal("REVIEW_RISK_NOT_ACCEPTABLE", error.Code);
        Assert.Empty(_decisions.View(_projectId, review).Decisions);
    }

    [Fact]
    public async Task Com_o_bloqueio_resolvido_a_pessoa_assina_a_entrega_e_o_veredito_do_motor_nao_muda()
    {
        var review = await ReviewWithSecretAsync();
        var finding = Assert.Single(review.Findings);
        _decisions.DecideFinding(_projectId, review.Id, finding.Id, FindingDecisions.FalsePositive, "chave de exemplo do guia de instalação, inventada");

        var view = _decisions.DecideDelivery(_projectId, review.Id, DeliveryDecisions.Approved, "conferi o relatório e a chave é de exemplo");

        Assert.Equal(DeliveryDecisions.Approved, view.Status.Delivery!.Decision);
        Assert.Equal(ReviewVerdicts.Rejected, view.Status.EngineVerdict);
        Assert.Contains(_audit.Entries, entry => entry.Action == AuditActions.QualityDeliveryDecided);
    }

    [Fact]
    public async Task A_decisao_vale_para_o_mesmo_achado_numa_revisao_nova()
    {
        var first = await ReviewWithSecretAsync();
        _decisions.DecideFinding(_projectId, first.Id, first.Findings[0].Id, FindingDecisions.FalsePositive, "chave de exemplo do guia de instalação, inventada");

        var second = await _reviews.ReviewWorkingTreeAsync(_projectId, CancellationToken.None);
        var view = _decisions.View(_projectId, second);

        Assert.Equal(first.Findings[0].Id, second.Findings[0].Id);
        Assert.Equal((0, true), (view.Status.OpenBlocking, view.Status.ReadyForDelivery));
        Assert.Single(view.Decisions);
    }

    [Fact]
    public async Task Achado_de_outra_revisao_ou_decisao_desconhecida_e_recusado()
    {
        var review = await ReviewWithSecretAsync();

        Assert.Equal("REVIEW_FINDING_NOT_FOUND", Assert.Throws<EngineException>(() => _decisions.DecideFinding(_projectId, review.Id, "f-000000000000", FindingDecisions.Confirmed, null)).Code);
        Assert.Equal("REVIEW_DECISION_INVALID", Assert.Throws<EngineException>(() => _decisions.DecideFinding(_projectId, review.Id, review.Findings[0].Id, "apagar", null)).Code);
        Assert.Equal("REVIEW_NOT_FOUND", Assert.Throws<EngineException>(() => _decisions.DecideFinding(_projectId, "rev-000000000000", "f-1", FindingDecisions.Confirmed, null)).Code);
        Assert.Equal(EngineErrors.ProjectNotAuthorized().Code, Assert.Throws<EngineException>(() => _decisions.DecideFinding("p-000000000000", review.Id, "f-1", FindingDecisions.Confirmed, null)).Code);
    }

    [Fact]
    public async Task A_justificativa_com_cara_de_segredo_nao_e_guardada()
    {
        var review = await ReviewWithSecretAsync();

        var view = _decisions.DecideFinding(_projectId, review.Id, review.Findings[0].Id, FindingDecisions.FalsePositive, $"é a chave {FakeToken} do guia");

        Assert.DoesNotContain(FakeToken, System.Text.Json.JsonSerializer.Serialize(view), StringComparison.Ordinal);
        Assert.DoesNotContain(FakeToken, File.ReadAllText(Path.Combine(_project.DataDir, "projects", _projectId, "reviews", "decisions.json")), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Revisao_vazia_nunca_fica_pronta_para_entrega()
    {
        var empty = await _reviews.ReviewWorkingTreeAsync(_projectId, CancellationToken.None);

        Assert.Equal(ReviewVerdicts.NotRun, empty.Verdict);
        Assert.False(_decisions.View(_projectId, empty).Status.ReadyForDelivery);
    }
}
