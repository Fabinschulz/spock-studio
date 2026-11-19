using System.Text.Json;
using System.Text.Json.Nodes;
using Mariliz.Application.Analysis;
using Mariliz.Application.Tests.Support;

namespace Mariliz.Application.Tests;

// W2-3: a análise de arquitetura medida contra um gabarito. Os casos reproduzem a forma dos repositórios de referência sem o
// código deles. Com MARILIZ_RECORD_EVALS=1, a medição entra no histórico que o eval de referências do Python também usa.
public class ArchitectureEvaluationTests
{
    private static readonly string Datasets = Path.Combine(ContractFiles.Root, "intelligence", "evaluations");

    private sealed record CaseResult(string Id, int Expected, int Found, IReadOnlyList<string> Missing, IReadOnlyList<string> FalsePositives);

    [Fact]
    public void Analise_acha_as_violacoes_conhecidas_sem_falso_positivo()
    {
        var dataset = JsonNode.Parse(File.ReadAllText(Path.Combine(Datasets, "datasets", "architecture.json")))!;
        Assert.Equal("architecture.eval.v1", dataset["schema"]!.GetValue<string>());

        var results = dataset["cases"]!.AsArray().Select(item => Evaluate(item!)).ToList();
        var expected = results.Sum(result => result.Expected);
        var found = results.Sum(result => result.Found);
        var falsePositives = results.Sum(result => result.FalsePositives.Count);
        var recall = expected == 0 ? 1.0 : Math.Round((double)found / expected, 4);

        var detail = string.Join("\n", results.Where(result => result.Missing.Count > 0 || result.FalsePositives.Count > 0)
            .Select(result => $"{result.Id}: faltou [{string.Join("; ", result.Missing)}], sobrou [{string.Join("; ", result.FalsePositives)}]"));
        Assert.True(recall >= dataset["target"]!["recall"]!.GetValue<double>(), detail);
        Assert.True(falsePositives <= dataset["target"]!["falsePositives"]!.GetValue<int>(), detail);

        if (Environment.GetEnvironmentVariable("MARILIZ_RECORD_EVALS") == "1")
            Record(new JsonObject
            {
                ["cases"] = results.Count,
                ["expected"] = expected,
                ["falsePositives"] = falsePositives,
                ["found"] = found,
                ["recall"] = recall
            });
    }

    // Esperado é achado com o mesmo código e o assunto no título; falso positivo é warning que nenhum esperado explica.
    private static CaseResult Evaluate(JsonNode item)
    {
        using var project = new TempProject();
        foreach (var (path, content) in item["files"]!.AsObject())
            project.Write(path, content!.GetValue<string>());

        var report = ArchitectureAnalyzer.Analyze("eval", new TestFilesFactory().Open(project.Root), DateTimeOffset.UnixEpoch);
        var expect = item["expect"]!.AsArray()
            .Select(node => (Code: node!["code"]!.GetValue<string>(), Subject: node["subject"]!.GetValue<string>()))
            .ToList();
        bool Matches(ArchitectureFinding finding, (string Code, string Subject) wanted) =>
            finding.Code == wanted.Code && finding.Title.Contains(wanted.Subject, StringComparison.Ordinal);

        var missing = expect.Where(wanted => !report.Findings.Any(finding => Matches(finding, wanted))).Select(wanted => $"{wanted.Code} {wanted.Subject}").ToList();
        var extra = report.Findings
            .Where(finding => finding.Severity == ArchitectureSeverities.Warning && !expect.Any(wanted => Matches(finding, wanted)))
            .Select(finding => $"{finding.Code} {finding.Title}")
            .ToList();
        return new CaseResult(item["id"]!.GetValue<string>(), expect.Count, expect.Count - missing.Count, missing, extra);
    }

    // Mesmo formato do evaluations/history.py: data local, chaves em ordem alfabética, JSON compacto, uma linha por medição.
    private static void Record(JsonObject metrics)
    {
        var entry = new JsonObject
        {
            ["measuredAt"] = DateTime.Now.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            ["metrics"] = metrics,
            ["suite"] = "architecture"
        };
        File.AppendAllText(Path.Combine(Datasets, "history.jsonl"),
            entry.ToJsonString(new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }) + "\n");
    }
}
