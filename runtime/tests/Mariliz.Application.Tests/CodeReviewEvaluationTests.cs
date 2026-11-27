using System.Text.Json;
using System.Text.Json.Nodes;
using Mariliz.Application.Quality;
using Mariliz.Application.Quality.Git;
using Mariliz.Application.Tests.Support;
using Mariliz.Infrastructure;

namespace Mariliz.Application.Tests;

// A revisão medida contra um gabarito, pelo caminho inteiro (git de verdade, leitor, detectores, juízes). O gabarito foi escrito
// por quem escreveu os detectores: o teste impede regressão, não mede qualidade. Antes de virar meta de aprovação precisa de uma
// linha de base sobre alterações reais, revisadas por uma pessoa. Com MARILIZ_RECORD_EVALS=1 a medição entra no histórico.
public class CodeReviewEvaluationTests
{
    private static readonly string Datasets = Path.Combine(ContractFiles.Root, "intelligence", "evaluations");
    private static readonly string FakeToken = "ghp_" + new string('c', 36);

    private sealed record CaseResult(string Id, int Expected, int Found, IReadOnlyList<string> Missing, IReadOnlyList<string> Extra);

    [Fact]
    public async Task A_revisao_acha_os_defeitos_do_gabarito_sem_falso_positivo()
    {
        var dataset = JsonNode.Parse(File.ReadAllText(Path.Combine(Datasets, "datasets", "code-review.json")))!;
        Assert.Equal("code-review.eval.v1", dataset["schema"]!.GetValue<string>());

        var results = new List<CaseResult>();
        var knownGaps = new List<CaseResult>();
        foreach (var item in dataset["cases"]!.AsArray())
        {
            var result = await Evaluate(item!);
            // Lacuna conhecida: o caso mostra um limite do detector e não entra na meta; se um dia passar a ser acertado, o teste avisa.
            if (item!["knownGap"] is not null) knownGaps.Add(result);
            else results.Add(result);
        }
        Assert.All(knownGaps, gap => Assert.NotEmpty(gap.Extra));

        var expected = results.Sum(result => result.Expected);
        var found = results.Sum(result => result.Found);
        var extra = results.Sum(result => result.Extra.Count);
        var recall = expected == 0 ? 1.0 : Math.Round((double)found / expected, 4);

        var detail = string.Join("\n", results.Where(result => result.Missing.Count > 0 || result.Extra.Count > 0)
            .Select(result => $"{result.Id}: faltou [{string.Join("; ", result.Missing)}], sobrou [{string.Join("; ", result.Extra)}]"));
        Assert.True(recall >= dataset["target"]!["recall"]!.GetValue<double>(), detail);
        Assert.True(extra <= dataset["target"]!["falsePositives"]!.GetValue<int>(), detail);

        if (Environment.GetEnvironmentVariable("MARILIZ_RECORD_EVALS") == "1")
            Record(new JsonObject { ["cases"] = results.Count, ["expected"] = expected, ["falsePositives"] = extra, ["found"] = found, ["knownGaps"] = knownGaps.Count, ["recall"] = recall });
    }

    // Esperado é achado com a mesma regra, no mesmo arquivo (e na mesma linha quando o gabarito diz). Falso positivo é achado que
    // o gabarito não explica, fora o informativo e a análise de arquitetura, que o gabarito não cobre.
    private static async Task<CaseResult> Evaluate(JsonNode item)
    {
        using var project = new TempProject();
        var engine = LocalEngine.Create(project.DataDir, (_, _, _, _, _) => { }, rememberWorkspaces: false);
        Write(project, item["base"]);
        project.Git("init", "-b", "main");
        project.Write(".gitkeep", "");
        project.Git("add", "-A");
        project.Git("-c", "commit.gpgsign=false", "commit", "-m", "base");
        Write(project, item["change"]);
        foreach (var path in item["delete"]?.AsArray() ?? [])
            File.Delete(Path.Combine(project.Root, path!.GetValue<string>().Replace('/', Path.DirectorySeparatorChar)));

        var projectId = engine.Workspace.Authorize(project.Root).Project!.ProjectId;
        var files = new TestFilesFactory();
        var service = new CodeReviewService(engine.Workspace, new GitChangeReader(new SystemProcessRunner(), files), new FileReviewStore(project.DataDir), new FakeClock(), files, new MemoryAudit());
        var review = await service.ReviewWorkingTreeAsync(projectId, CancellationToken.None);

        var expect = item["expect"]!.AsArray()
            .Select(node => (Rule: node!["ruleId"]!.GetValue<string>(), Path: node["path"]!.GetValue<string>(), Line: node["line"]?.GetValue<int>()))
            .ToList();
        var findings = review.Findings.Where(finding => finding.RuleId != ReviewRules.Architecture && finding.Severity != QualitySeverities.Informational).ToList();
        bool Matches(ReviewFinding finding, (string Rule, string Path, int? Line) wanted) =>
            finding.RuleId == wanted.Rule && finding.Path == wanted.Path && (wanted.Line is null || finding.StartLine == wanted.Line);

        var missing = expect.Where(wanted => !findings.Any(finding => Matches(finding, wanted))).Select(wanted => $"{wanted.Rule} {wanted.Path}:{wanted.Line}").ToList();
        var surplus = findings.Where(finding => !expect.Any(wanted => Matches(finding, wanted))).Select(finding => $"{finding.RuleId} {finding.Path}:{finding.StartLine}").ToList();
        return new CaseResult(item["id"]!.GetValue<string>(), expect.Count, expect.Count - missing.Count, missing, surplus);
    }

    private static void Write(TempProject project, JsonNode? files)
    {
        foreach (var (path, content) in files?.AsObject() ?? [])
            project.Write(path, content!.GetValue<string>().Replace("{{TOKEN}}", FakeToken, StringComparison.Ordinal));
    }

    // Mesmo formato do evaluations/history.py: data local, chaves em ordem alfabética, JSON compacto, uma linha por medição.
    private static void Record(JsonObject metrics)
    {
        var entry = new JsonObject
        {
            ["measuredAt"] = DateTime.Now.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            ["metrics"] = metrics,
            ["suite"] = "code-review"
        };
        File.AppendAllText(Path.Combine(Datasets, "history.jsonl"),
            entry.ToJsonString(new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }) + "\n");
    }
}
