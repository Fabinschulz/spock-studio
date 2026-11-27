using System.Security.Cryptography;
using System.Text.Json.Nodes;
using Mariliz.Application.Audit;
using Mariliz.Application.Boot;
using Mariliz.Application.Execution;
using Mariliz.Application.Protocol;
using Mariliz.Application.Quality;
using Mariliz.Application.Quality.Git;
using Mariliz.Application.Tests.Support;
using Mariliz.Infrastructure;

namespace Mariliz.Application.Tests;

public class UnifiedDiffParserTests
{
    [Fact]
    public void Le_modificado_adicionado_apagado_renomeado_e_binario()
    {
        const string patch = """
            diff --git a/src/a.cs b/src/a.cs
            index 111..222 100644
            --- a/src/a.cs
            +++ b/src/a.cs
            @@ -1,3 +1,4 @@ public class A
             linha 1
            -linha 2
            +linha 2 nova
            +linha extra
             linha 3
            diff --git a/novo.txt b/novo.txt
            new file mode 100644
            --- /dev/null
            +++ b/novo.txt
            @@ -0,0 +1,2 @@
            +um
            +dois
            diff --git a/velho.txt b/velho.txt
            deleted file mode 100644
            --- a/velho.txt
            +++ /dev/null
            @@ -1 +0,0 @@
            -adeus
            diff --git a/de.txt b/para.txt
            similarity index 90%
            rename from de.txt
            rename to para.txt
            diff --git a/img.png b/img.png
            Binary files a/img.png and b/img.png differ
            """;

        var files = UnifiedDiffParser.Parse(patch.Replace("\r\n", "\n"));

        Assert.Equal(["src/a.cs", "novo.txt", "velho.txt", "para.txt", "img.png"], files.Select(file => file.Path));
        var modified = files[0];
        Assert.Equal(FileStatuses.Modified, modified.Status);
        Assert.Equal((2, 1), (modified.Added, modified.Removed));
        Assert.Equal("public class A", modified.Hunks[0].Symbol);
        Assert.Equal([2, 3], modified.AddedLines.Select(line => line.NewNumber!.Value));
        Assert.Equal(FileStatuses.Added, files[1].Status);
        Assert.Equal(FileStatuses.Deleted, files[2].Status);
        Assert.Equal((FileStatuses.Renamed, "de.txt"), (files[3].Status, files[3].OldPath));
        Assert.Equal(SkipReasons.Binary, files[4].SkippedReason);
    }

    [Fact]
    public void Caminho_com_espaco_e_arquivo_so_com_mudanca_de_modo_nao_viram_outro_arquivo()
    {
        const string patch = "diff --git a/minha pasta/a b.txt b/minha pasta/a b.txt\nold mode 100644\nnew mode 100755\n";

        var file = Assert.Single(UnifiedDiffParser.Parse(patch));

        Assert.Equal("minha pasta/a b.txt", file.Path);
        Assert.Empty(file.Hunks);
    }

    [Fact]
    public void Arquivo_com_alteracao_enorme_nao_guarda_as_linhas()
    {
        var body = string.Concat(Enumerable.Range(0, UnifiedDiffParser.MaxChangedLinesPerFile + 1).Select(i => $"+linha {i}\n"));
        var patch = $"diff --git a/g.txt b/g.txt\nnew file mode 100644\n--- /dev/null\n+++ b/g.txt\n@@ -0,0 +1,{UnifiedDiffParser.MaxChangedLinesPerFile + 1} @@\n{body}";

        var file = Assert.Single(UnifiedDiffParser.Parse(patch));

        Assert.Equal(SkipReasons.TooLarge, file.SkippedReason);
        Assert.Empty(file.Hunks);
    }
}

public sealed class CodeReviewTests : IDisposable
{
    // Montado em partes para o repositório do teste não carregar uma credencial inteira no código-fonte.
    private static readonly string FakeToken = "ghp_" + new string('a', 36);

    private readonly TempProject _project = new();
    private readonly FakeClock _clock = new();
    private readonly MemoryAudit _audit = new();
    private readonly LocalEngine _engine;
    private readonly string _projectId;
    private readonly FileReviewStore _store;
    private readonly CodeReviewService _service;
    private readonly GitChangeReader _git;

    public CodeReviewTests()
    {
        _engine = LocalEngine.Create(_project.DataDir, (_, _, _, _, _) => { }, rememberWorkspaces: false);
        _project.Write("backend/src/App/App.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");
        Commit("inicio");
        _projectId = _engine.Workspace.Authorize(_project.Root).Project!.ProjectId;
        _store = new FileReviewStore(_project.DataDir);
        var files = new TestFilesFactory();
        _git = new GitChangeReader(new SystemProcessRunner(), files);
        _service = new CodeReviewService(_engine.Workspace, _git, _store, _clock, files, _audit);
    }

    public void Dispose() => _project.Dispose();

    private void Commit(string message)
    {
        if (!_project.Exists(".git")) _project.Git("init", "-b", "main");
        _project.Git("add", "-A");
        _project.Git("-c", "commit.gpgsign=false", "commit", "-m", message);
    }

    private static string Fingerprint(TempProject project)
    {
        var index = Path.Combine(project.Root, ".git", "index");
        return project.Fingerprint() + (File.Exists(index) ? Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(index))) : "");
    }

    [Fact]
    public async Task Revisao_das_alteracoes_locais_acha_o_defeito_com_arquivo_linha_e_evidencia()
    {
        _project.Write("backend/src/App/Service.cs", "namespace App;\n\npublic class Service\n{\n    public void Run()\n    {\n        try { Work(); }\n        catch (Exception) { }\n    }\n}\n");

        var review = await _service.ReviewWorkingTreeAsync(_projectId, CancellationToken.None);

        var finding = Assert.Single(review.Findings, item => item.RuleId == ReviewRules.EmptyCatch);
        Assert.Equal("backend/src/App/Service.cs", finding.Path);
        Assert.Equal(8, finding.StartLine);
        Assert.True(finding.OnChangedLines);
        Assert.Equal(EvidenceRelations.AddedLine, finding.Evidence.Relation);
        Assert.Contains("catch", finding.Evidence.Excerpt);
        Assert.Equal(FindingConfidences.Heuristic, finding.Confidence);
        Assert.Equal(ReviewVerdicts.ApprovedWithWarnings, review.Verdict);
        Assert.Equal(FileStatuses.Untracked, Assert.Single(review.Files, file => file.Path.EndsWith("Service.cs", StringComparison.Ordinal)).Status);
    }

    [Fact]
    public async Task Segredo_confirmado_reprova_e_o_valor_nunca_aparece_no_registro()
    {
        _project.Write("config.txt", $"token = {FakeToken}\n");

        var review = await _service.ReviewWorkingTreeAsync(_projectId, CancellationToken.None);

        var finding = Assert.Single(review.Findings, item => item.RuleId == ReviewRules.SecretProvider);
        Assert.Equal((FindingConfidences.Confirmed, QualitySeverities.Critical), (finding.Confidence, finding.Severity));
        Assert.Equal(1, finding.StartLine);
        Assert.Equal(ReviewVerdicts.Rejected, review.Verdict);
        Assert.Equal(ReviewVerdicts.Rejected, review.Judges.Single(judge => judge.Id == JudgeIds.Security).Verdict);
        // Nem no que foi devolvido, nem no que ficou gravado.
        var stored = File.ReadAllText(Directory.EnumerateFiles(Path.Combine(_project.DataDir, "projects", _projectId, "reviews"), "rev-*.json").Single());
        Assert.DoesNotContain(FakeToken, stored, StringComparison.Ordinal);
        Assert.DoesNotContain(FakeToken, System.Text.Json.JsonSerializer.Serialize(review), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_revisao_nao_altera_nada_no_projeto_nem_o_indice_do_git()
    {
        _project.Write("a.txt", "mudou\n");
        _project.Write("novo.txt", "novo\n");
        var before = Fingerprint(_project);

        await _service.ReviewWorkingTreeAsync(_projectId, CancellationToken.None);

        Assert.Equal(before, Fingerprint(_project));
    }

    [Fact]
    public async Task Marcador_de_conflito_teste_desligado_e_teste_apagado_sao_achados()
    {
        _project.Write("tests/FooTests.cs", "public class FooTests\n{\n    [Fact]\n    public void A() { }\n\n    [Fact]\n    public void B() { }\n}\n");
        Commit("testes");
        File.Delete(Path.Combine(_project.Root, "tests", "FooTests.cs"));
        _project.Write("tests/BarTests.cs", "public class BarTests\n{\n    [Fact(Skip = \"depois\")]\n    public void C() { }\n}\n");
        _project.Write("src/Merge.cs", "class M\n{\n<<<<<<< HEAD\n    int a;\n=======\n    int b;\n>>>>>>> outro\n}\n");

        var review = await _service.ReviewWorkingTreeAsync(_projectId, CancellationToken.None);

        Assert.Contains(review.Findings, item => item.RuleId == ReviewRules.TestFileDeleted && item.Path == "tests/FooTests.cs");
        Assert.Contains(review.Findings, item => item.RuleId == ReviewRules.TestSkipped && item.Path == "tests/BarTests.cs" && item.StartLine == 3);
        var markers = review.Findings.Where(item => item.RuleId == ReviewRules.ConflictMarker).Select(item => item.StartLine).ToList();
        Assert.Equal([3, 7], markers);
        Assert.Equal(ReviewVerdicts.Rejected, review.Verdict);
        Assert.Equal(ReviewVerdicts.Rejected, review.Judges.Single(judge => judge.Id == JudgeIds.CodeQuality).Verdict);
    }

    [Fact]
    public async Task Arquivo_gerado_e_binario_ficam_na_lista_mas_nao_sao_analisados()
    {
        _project.Write("obj/Debug/x.cs", $"var t = \"{FakeToken}\";\n");
        _project.Write("pnpm-lock.yaml", "lockfileVersion: 9\n");
        File.WriteAllBytes(Path.Combine(_project.Root, "img.bin"), [0, 1, 2, 3, 0, 0]);
        _project.Write("limpo.txt", "tudo certo\n");

        var review = await _service.ReviewWorkingTreeAsync(_projectId, CancellationToken.None);

        Assert.Empty(review.Findings);
        Assert.Equal(SkipReasons.Generated, review.Files.Single(file => file.Path == "obj/Debug/x.cs").SkippedReason);
        Assert.Equal(SkipReasons.Generated, review.Files.Single(file => file.Path == "pnpm-lock.yaml").SkippedReason);
        Assert.Equal(SkipReasons.Binary, review.Files.Single(file => file.Path == "img.bin").SkippedReason);
        Assert.True(review.Files.Single(file => file.Path == "limpo.txt").Analyzed);
        Assert.Equal(1, review.Counts.FilesAnalyzed);
    }

    [Fact]
    public async Task Sem_alteracoes_o_veredito_e_nao_executada_e_nao_aprovada()
    {
        var review = await _service.ReviewWorkingTreeAsync(_projectId, CancellationToken.None);

        Assert.Equal(ReviewVerdicts.NotRun, review.Verdict);
        Assert.Equal(0, review.Counts.Files);
    }

    [Fact]
    public async Task O_relatorio_diz_o_que_nao_foi_feito()
    {
        _project.Write("limpo.txt", "ok\n");

        var review = await _service.ReviewWorkingTreeAsync(_projectId, CancellationToken.None);

        Assert.Equal(ReviewVerdicts.NotRun, review.Judges.Single(judge => judge.Id == JudgeIds.Model).Verdict);
        Assert.Equal(ReviewVerdicts.NotRun, review.Judges.Single(judge => judge.Id == JudgeIds.Requirements).Verdict);
        Assert.Equal(ReviewVerdicts.NotRun, review.Judges.Single(judge => judge.Id == JudgeIds.Dependency).Verdict);
        Assert.Contains(review.Limitations, text => text.Contains("build, testes e lint não rodaram", StringComparison.Ordinal));
        Assert.All(review.Judges, judge => Assert.True(judge.Deterministic));
        Assert.Equal(ReviewContract.Version, review.Contract);
    }

    [Fact]
    public async Task Revisao_de_branch_exige_a_base_e_compara_com_ela()
    {
        _project.Git("checkout", "-b", "feature");
        _project.Write("src/Feature.cs", "class F\n{\n    async void Go() { }\n}\n");
        Commit("feature");

        var review = await _service.ReviewBranchAsync(_projectId, "main", null, CancellationToken.None);

        Assert.Equal((ReviewModes.Branch, "main", "feature"), (review.Mode, review.Base, review.Branch));
        var finding = Assert.Single(review.Findings, item => item.RuleId == ReviewRules.AsyncVoid);
        Assert.Equal(3, finding.StartLine);
        Assert.Equal(FileStatuses.Added, Assert.Single(review.Files).Status);
        await Assert.ThrowsAsync<EngineException>(() => _service.ReviewBranchAsync(_projectId, "", null, CancellationToken.None));
    }

    [Theory]
    [InlineData("--output=x", "REVIEW_INVALID_REF")]
    [InlineData("nao-existe", "REVIEW_REF_NOT_FOUND")]
    public async Task Referencia_invalida_ou_inexistente_e_recusada_sem_chegar_no_git(string reference, string code)
    {
        var error = await Assert.ThrowsAsync<EngineException>(() => _service.ReviewBranchAsync(_projectId, reference, null, CancellationToken.None));

        Assert.Equal(code, error.Code);
    }

    [Fact]
    public async Task Pasta_que_nao_e_repositorio_e_recusada_com_clareza()
    {
        using var plain = new TempProject();
        plain.Write("a.txt", "x\n");
        var id = _engine.Workspace.Authorize(plain.Root).Project!.ProjectId;

        var error = await Assert.ThrowsAsync<EngineException>(() => _service.ReviewWorkingTreeAsync(id, CancellationToken.None));

        Assert.Equal("REVIEW_NOT_A_REPOSITORY", error.Code);
    }

    [Fact]
    public async Task Projeto_nao_autorizado_nao_e_lido()
    {
        var error = await Assert.ThrowsAsync<EngineException>(() => _service.ReviewWorkingTreeAsync("p-000000000000", CancellationToken.None));

        Assert.Equal(EngineErrors.ProjectNotAuthorized().Code, error.Code);
    }

    [Fact]
    public async Task Repositorio_sem_commit_le_o_indice_e_os_arquivos_novos_e_avisa()
    {
        using var fresh = new TempProject();
        fresh.Git("init", "-b", "main");
        fresh.Write("a.txt", "a\n");
        fresh.Git("add", "a.txt");
        fresh.Write("b.txt", "b\n");
        var id = _engine.Workspace.Authorize(fresh.Root).Project!.ProjectId;

        var review = await _service.ReviewWorkingTreeAsync(id, CancellationToken.None);

        Assert.Equal(["a.txt", "b.txt"], review.Files.Select(file => file.Path).Order(StringComparer.Ordinal));
        Assert.Contains(review.Limitations, text => text.Contains("nenhum commit", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Conflito_de_merge_no_git_e_achado_confirmado()
    {
        _project.Write("c.txt", "base\n");
        Commit("base");
        _project.Git("checkout", "-b", "outro");
        _project.Write("c.txt", "outro\n");
        Commit("outro");
        _project.Git("checkout", "main");
        _project.Write("c.txt", "main\n");
        Commit("main");
        _project.Git("merge", "outro");

        var review = await _service.ReviewWorkingTreeAsync(_projectId, CancellationToken.None);

        var finding = Assert.Single(review.Findings, item => item.RuleId == ReviewRules.ConflictMarker && item.Path == "c.txt");
        Assert.Equal(FindingConfidences.Confirmed, finding.Confidence);
        Assert.Equal(FileStatuses.Conflicted, review.Files.Single(file => file.Path == "c.txt").Status);
        Assert.Equal(ReviewVerdicts.Rejected, review.Verdict);
    }

    [Fact]
    public async Task Historico_e_persistido_listado_e_consultavel_so_pelo_projeto_autorizado()
    {
        _project.Write("limpo.txt", "ok\n");
        var review = await _service.ReviewWorkingTreeAsync(_projectId, CancellationToken.None);

        Assert.Equal(review.Id, Assert.Single(_service.List(_projectId)).Id);
        Assert.Equal(review.Summary, _service.Detail(_projectId, review.Id).Summary);
        Assert.Equal("REVIEW_NOT_FOUND", Assert.Throws<EngineException>(() => _service.Detail(_projectId, "rev-000000000000")).Code);
        Assert.Equal("REVIEW_NOT_FOUND", Assert.Throws<EngineException>(() => _service.Detail(_projectId, "../x")).Code);
        Assert.Contains(_audit.Entries, entry => entry.Action == AuditActions.QualityReviewed);
    }

    [Fact]
    public async Task O_simbolo_so_aparece_quando_parece_a_assinatura_de_uma_funcao()
    {
        var body = string.Concat(Enumerable.Range(1, 6).Select(i => $"  const v{i} = {i};\n"));
        _project.Write("src/total.js", $"function total(items) {{\n{body}  return 1;\n}}\n");
        _project.Write("src/Loja.cs", $"namespace Loja;\n\nclass A\n{{\n{string.Concat(Enumerable.Range(1, 6).Select(i => $"    int v{i};\n"))}    int fim;\n}}\n");
        Commit("base");
        _project.Write("src/total.js", $"function total(items) {{\n{body}  try {{ go(); }} catch (e) {{}}\n  return 1;\n}}\n");
        _project.Write("src/Loja.cs", $"namespace Loja;\n\nclass A\n{{\n{string.Concat(Enumerable.Range(1, 6).Select(i => $"    int v{i};\n"))}    void M() {{ try {{ }} catch {{ }} }}\n}}\n");

        var review = await _service.ReviewWorkingTreeAsync(_projectId, CancellationToken.None);

        Assert.Equal("function total(items) {", review.Findings.Single(item => item.Path == "src/total.js" && item.RuleId == ReviewRules.EmptyCatch).Evidence.Symbol);
        Assert.Null(review.Findings.Single(item => item.Path == "src/Loja.cs" && item.RuleId == ReviewRules.EmptyCatch).Evidence.Symbol);
    }

    [Fact]
    public async Task O_mesmo_defeito_no_mesmo_lugar_tem_o_mesmo_id_entre_revisoes()
    {
        _project.Write("src/E.cs", "class E { void M() { try { } catch { } } }\n");

        var first = await _service.ReviewWorkingTreeAsync(_projectId, CancellationToken.None);
        var second = await _service.ReviewWorkingTreeAsync(_projectId, CancellationToken.None);

        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(first.Findings.Select(item => item.Id), second.Findings.Select(item => item.Id));
        Assert.Equal(first.Findings.Count, first.Findings.Select(item => item.Id).Distinct().Count());
    }
}

public sealed class QualityProtocolTests : IDisposable
{
    private readonly TempProject _project = new();
    private readonly EngineSession _session;
    private readonly string _projectId;

    public QualityProtocolTests()
    {
        var engine = LocalEngine.Create(_project.DataDir, (_, _, _, _, _) => { }, rememberWorkspaces: false);
        _project.Write("tests/FooTests.cs", "public class FooTests\n{\n    [Fact]\n    public void A() { }\n}\n");
        _project.Git("init", "-b", "main");
        _project.Git("add", "-A");
        _project.Git("-c", "commit.gpgsign=false", "commit", "-m", "inicio");
        _projectId = engine.Workspace.Authorize(_project.Root).Project!.ProjectId;
        var files = new TestFilesFactory();
        var quality = new CodeReviewService(engine.Workspace, new GitChangeReader(new SystemProcessRunner(), files), new FileReviewStore(_project.DataDir),
            new FakeClock(), files, new MemoryAudit());
        _session = new EngineSession("test", workspace: engine.Workspace, quality: quality);
    }

    public void Dispose() => _project.Dispose();

    private JsonObject Result(string method, JsonObject parameters)
    {
        var response = _session.Handle(new JsonObject { ["v"] = 1, ["kind"] = "request", ["id"] = "t", ["method"] = method, ["params"] = parameters }.ToJsonString()).Response;
        return response["result"]?.AsObject() ?? throw new InvalidOperationException(response.ToJsonString());
    }

    [Fact]
    public void Os_metodos_de_qualidade_sao_anunciados_no_hello()
    {
        var hello = _session.Handle(new JsonObject
        {
            ["v"] = 1, ["kind"] = "request", ["id"] = "h", ["method"] = "engine.hello",
            ["params"] = new JsonObject { ["protocol"] = new JsonArray(1), ["hostVersion"] = "t" }
        }.ToJsonString()).Response["result"]!["capabilities"]!.AsArray().Select(node => node!.GetValue<string>()).ToList();

        Assert.All(ProtocolMethods.Quality, method => Assert.Contains(method, hello));
    }

    [Fact]
    public void A_revisao_pelo_protocolo_tem_o_formato_do_contrato_e_a_lista_e_o_detalhe_a_acompanham()
    {
        File.Delete(Path.Combine(_project.Root, "tests", "FooTests.cs"));
        _project.Write("src/E.cs", "class E\n{\n    void M() { try { } catch { } }\n}\n");
        var token = "ghp_" + new string('b', 36);
        _project.Write("config.txt", "token = " + token + "\n");

        var started = Result(ProtocolMethods.QualityReviewStart, new JsonObject { ["projectId"] = _projectId });
        var review = started["review"]!;

        Assert.Equal("rejected", review["verdict"]!.GetValue<string>());
        Assert.DoesNotContain(token, started.ToJsonString(), StringComparison.Ordinal);
        GoldenFixtures.Check("quality-review", Scrub(started));

        var list = Result(ProtocolMethods.QualityReviewList, new JsonObject { ["projectId"] = _projectId });
        Assert.Single(list["reviews"]!.AsArray());
        GoldenFixtures.Check("quality-list", Scrub(list));

        var id = review["id"]!.GetValue<string>();
        var detail = Result(ProtocolMethods.QualityReviewDetail, new JsonObject { ["projectId"] = _projectId, ["reviewId"] = id });
        Assert.Equal(id, detail["review"]!["id"]!.GetValue<string>());
    }

    [Fact]
    public void Modo_desconhecido_e_branch_sem_base_sao_recusados_pelo_protocolo()
    {
        var response = _session.Handle(new JsonObject
        {
            ["v"] = 1, ["kind"] = "request", ["id"] = "t", ["method"] = ProtocolMethods.QualityReviewStart,
            ["params"] = new JsonObject { ["projectId"] = _projectId, ["mode"] = "tudo" }
        }.ToJsonString()).Response;

        Assert.NotNull(response["error"]);
    }

    private JsonNode Scrub(JsonObject node)
    {
        var text = GoldenFixtures.Normalize(node, _projectId, Path.GetFileName(_project.Root)).ToJsonString();
        text = System.Text.RegularExpressions.Regex.Replace(text, "rev-[0-9a-f]{12}", "rev-000000000000");
        text = System.Text.RegularExpressions.Regex.Replace(text, "\"f-[0-9a-f]{12}\"", "\"f-000000000000\"");
        return JsonNode.Parse(text)!;
    }
}

public class ReviewJudgesTests
{
    private static ReviewFinding Finding(string ruleId, string severity, string confidence) => new(
        "f-1", ruleId, "rev-1", "p-1", "a.cs", 1, 1, true, ReviewRules.Find(ruleId).Category, severity, confidence, "t", "d",
        new FindingEvidence("x", EvidenceRelations.AddedLine, "diff", null, []), "e", "a", "s", null, null, "deterministic", "teste", FindingStatuses.Open, DateTimeOffset.UnixEpoch);

    private static JudgeInput Input(IReadOnlyList<ReviewFinding> findings, int analyzed = 1, bool truncated = false) =>
        new(findings, analyzed, analyzed, truncated, [], null);

    [Fact]
    public void Achado_confirmado_e_grave_bloqueia_e_nenhum_juiz_aprovando_o_desfaz()
    {
        var blocking = Finding(ReviewRules.SecretProvider, QualitySeverities.Critical, FindingConfidences.Confirmed);
        var input = Input([blocking]);
        // Todos os juízes de dimensão dizem "approved": o veredito ainda é reprovado.
        IReadOnlyList<JudgeResult> approvals = [new("security", "S", ReviewVerdicts.Approved, "ok", [], [], true), new("code-quality", "Q", ReviewVerdicts.Approved, "ok", [], [], true)];

        Assert.Equal(ReviewVerdicts.Rejected, ReviewJudges.Decide(approvals, input).Verdict);
    }

    [Theory]
    [InlineData(QualitySeverities.High, FindingConfidences.Heuristic)]
    [InlineData(QualitySeverities.Medium, FindingConfidences.Hypothesis)]
    public void Heuristica_e_hipotese_pedem_conferencia_e_nao_barram(string severity, string confidence)
    {
        var findings = new[] { Finding(ReviewRules.TlsDisabled, severity, confidence) };

        var judges = ReviewJudges.Evaluate(Input(findings));

        Assert.Equal(ReviewVerdicts.ApprovedWithWarnings, judges[^1].Verdict);
        Assert.Equal(ReviewVerdicts.ApprovedWithWarnings, judges.Single(judge => judge.Id == JudgeIds.Security).Verdict);
    }

    [Fact]
    public void Recomendacao_informativa_nao_muda_o_veredito()
    {
        var judges = ReviewJudges.Evaluate(Input([Finding(ReviewRules.HarnessFileChanged, QualitySeverities.Informational, FindingConfidences.Heuristic)]));

        Assert.Equal(ReviewVerdicts.Approved, judges[^1].Verdict);
    }

    [Fact]
    public void Alteracao_lida_pela_metade_nunca_e_aprovada()
    {
        Assert.Equal(ReviewVerdicts.Inconclusive, ReviewJudges.Evaluate(Input([], truncated: true))[^1].Verdict);
        Assert.Equal(ReviewVerdicts.Inconclusive, ReviewJudges.Evaluate(new JudgeInput([], 3, 0, false, [], null))[^1].Verdict);
    }

    [Fact]
    public void Sem_alteracoes_e_nao_executada()
    {
        Assert.Equal(ReviewVerdicts.NotRun, ReviewJudges.Evaluate(new JudgeInput([], 0, 0, false, [], null))[^1].Verdict);
    }

    [Fact]
    public void Juizes_sem_dados_aparecem_como_nao_executados_com_o_motivo()
    {
        var judges = ReviewJudges.Evaluate(Input([]));

        foreach (var id in new[] { JudgeIds.Requirements, JudgeIds.Dependency, JudgeIds.Model })
        {
            var judge = judges.Single(item => item.Id == id);
            Assert.Equal(ReviewVerdicts.NotRun, judge.Verdict);
            Assert.False(string.IsNullOrWhiteSpace(judge.Reason));
        }
    }

    [Fact]
    public void Toda_regra_tem_juiz_categoria_e_texto_e_os_ids_nao_se_repetem()
    {
        var judges = new[] { JudgeIds.Security, JudgeIds.Architecture, JudgeIds.CodeQuality, JudgeIds.Regression };

        Assert.Equal(ReviewRules.All.Count, ReviewRules.All.Select(rule => rule.Id).Distinct().Count());
        Assert.All(ReviewRules.All, rule =>
        {
            Assert.Contains(rule.Judge, judges);
            Assert.Contains(rule.Category, FindingCategories.All);
            Assert.Contains(rule.Severity, QualitySeverities.All);
            Assert.False(string.IsNullOrWhiteSpace(rule.Suggestion));
        });
    }
}

public class ReviewBootTests
{
    private static IntentResult Recognize(string text)
    {
        var registry = new CapabilityRegistry();
        return new IntentRecognizer(registry).Recognize(new IntentRequest(text, null, null, true));
    }

    [Theory]
    [InlineData("Revise minhas alterações antes do commit")]
    [InlineData("faça um code review do que eu mudei")]
    [InlineData("quero revisar as alterações locais")]
    public void Pedido_de_revisao_vira_a_revisao_das_alteracoes_locais(string text)
    {
        var intent = Recognize(text);

        Assert.Equal(IntentResolution.Resolved, intent.Resolution);
        Assert.Equal("quality.review_changes", intent.CapabilityId);
    }

    [Fact]
    public void Pedido_de_revisar_uma_branch_abre_a_tela_porque_pede_a_base()
    {
        var intent = Recognize("revisar a branch feature antes do merge");
        var registry = new CapabilityRegistry();
        var capability = registry.Find(intent.CapabilityId!)!;
        var status = new CapabilityStatus(capability, CapabilityAvailability.Available, null);

        Assert.Equal("quality.review_branch", capability.Id);
        Assert.Equal(PermissionVerdict.HandoffOnly, PermissionEvaluator.Evaluate(capability, status, AutonomyLevel.Observation).Verdict);
    }

    [Fact]
    public void A_revisao_das_alteracoes_executa_sozinha_no_nivel_de_observacao_porque_so_le()
    {
        var registry = new CapabilityRegistry();
        var capability = registry.Find("quality.review_changes")!;
        var status = new CapabilityStatus(capability, CapabilityAvailability.Available, null);

        var decision = PermissionEvaluator.Evaluate(capability, status, AutonomyLevel.Observation);

        Assert.Equal(PermissionVerdict.Allowed, decision.Verdict);
        Assert.False(decision.NeedsApproval);
        Assert.Equal(OperationClass.Analysis, capability.Operation);
    }

    [Fact]
    public void Sem_o_metodo_no_motor_a_capacidade_nao_aparece_como_disponivel()
    {
        var registry = new CapabilityRegistry();
        var capability = registry.Find("quality.review_changes")!;
        var facts = new BootFacts("p-1", true, "x", new HashSet<string>(), false, 0, 0, 0, false, null, []);

        var status = registry.Resolve(capability, facts);

        Assert.Equal(CapabilityAvailability.Unavailable, status.Availability);
    }

    [Fact]
    public void Nenhuma_capacidade_de_qualidade_usa_um_metodo_de_escrita()
    {
        var quality = CapabilityCatalog.All.Where(capability => capability.Id.StartsWith("quality.", StringComparison.Ordinal)).ToList();

        Assert.NotEmpty(quality);
        Assert.All(quality, capability => Assert.True(capability.Operation <= OperationClass.Analysis, capability.Id));
    }
}
