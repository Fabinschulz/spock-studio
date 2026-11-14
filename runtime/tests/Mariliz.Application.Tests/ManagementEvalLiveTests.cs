using System.Text.Json;
using System.Text.Json.Nodes;
using Mariliz.Application.Models;
using Mariliz.Application.Management.Investigation;
using Mariliz.Application.Tests.Support;
using Mariliz.Infrastructure;
using Xunit.Abstractions;

namespace Mariliz.Application.Tests;

// Quem pontua é o pontuador Python, que não depende de quem escreveu a resposta: python intelligence/evaluations/management.py score <arquivo>. Só roda com a chave do provedor definida no terminal (LiveFactAttribute); a chave não passa por arquivo, conversa nem relatório, e o teste confere que ela não está no arquivo de resultados. MARILIZ_LIVE_EVAL_OUT escolhe a pasta.
public class ManagementEvalLiveTests(ITestOutputHelper output)
{
    private const string OutputFolder = "MARILIZ_LIVE_EVAL_OUT";

    [LiveFact(LiveKeys.OpenAi)]
    public Task Evals_de_gestao_com_a_OpenAI() => RunAsync(ModelProviders.OpenAi.Id, LiveKeys.Read(LiveKeys.OpenAi));

    [LiveFact(LiveKeys.Anthropic)]
    public Task Evals_de_gestao_com_a_Anthropic() => RunAsync(ModelProviders.Anthropic.Id, LiveKeys.Read(LiveKeys.Anthropic));

    // chat troca o provedor (os testes sem rede usam um roteirizado).
    private async Task<string> RunAsync(string provider, string key, IModelChatTransport? chat = null)
    {
        var dataset = JsonNode.Parse(File.ReadAllText(DatasetPath()))!.AsObject();
        var scenarios = dataset["scenarios"]!.AsObject();
        using var network = chat is null ? new HttpsModelTransport() : null;
        var gateway = new ModelGateway(new EnvModelSettings(provider, key), network ?? (IModelTransport)new NoopTransport(), timeout: TimeSpan.FromSeconds(60), chat: chat ?? network);
        using var access = new LocalModelAccess(new ModelBroker(gateway));
        var python = new LoopbackAiRuntime();
        var acks = new JsonObject();
        var calls = 0;
        var tokens = 0;
        try
        {
            foreach (var item in dataset["cases"]!.AsArray())
            {
                var testCase = item!.AsObject();
                var executionId = "ex-" + Guid.NewGuid().ToString("N")[..12];
                var grant = access.Open(executionId, InvestigationService.TokenBudget);
                var facts = scenarios[testCase["scenario"]!.GetValue<string>()]!.DeepClone().AsArray();
                var request = new JsonObject
                {
                    ["schema"] = "ai.investigation.start.v2",
                    ["investigationId"] = "inv-" + Guid.NewGuid().ToString("N")[..12],
                    ["executionId"] = executionId,
                    ["projectId"] = "p-" + Guid.NewGuid().ToString("N")[..12],
                    ["question"] = testCase["question"]!.GetValue<string>(),
                    ["facts"] = facts,
                    ["groups"] = GroupsFor(facts),
                    ["policy"] = new JsonObject { ["modelAllowed"] = true },
                    ["limits"] = new JsonObject { ["maxIterations"] = 12, ["maxMilliseconds"] = 150_000, ["maxTokens"] = InvestigationService.TokenBudget },
                    ["broker"] = new JsonObject { ["url"] = grant.Url, ["token"] = grant.Token }
                };
                JsonObject ack;
                ModelUsage usage;
                try
                {
                    using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(150));
                    ack = await python.InvestigateAsync(request, _ => { }, deadline.Token);
                }
                finally
                {
                    usage = access.Close(executionId);
                }
                calls += usage.Calls;
                tokens += usage.InputTokens + usage.OutputTokens;
                acks[testCase["id"]!.GetValue<string>()] = ack;
                output.WriteLine($"[evals de gestão] {provider} {testCase["id"]}: {ack["status"]}; rodadas: {ack["rounds"]}; chamadas: {usage.Calls}; tokens: {usage.InputTokens}+{usage.OutputTokens}");
                Assert.Equal("ai.investigation.ack.v1", ack["schema"]!.GetValue<string>());
            }
        }
        finally
        {
            await python.ShutdownAsync();
        }

        var folder = Environment.GetEnvironmentVariable(OutputFolder) is { Length: > 0 } custom ? custom : Path.GetTempPath();
        Directory.CreateDirectory(folder);
        var file = Path.Combine(folder, $"management-eval-{provider}.json");
        var text = acks.ToJsonString(new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
        Assert.DoesNotContain(key, text, StringComparison.Ordinal);
        File.WriteAllText(file, text);
        output.WriteLine($"[evals de gestão] {provider}: {acks.Count} casos, {calls} chamadas, {tokens} tokens. Resultados em: {file}");
        output.WriteLine($"[evals de gestão] Pontue com: python intelligence/evaluations/management.py score \"{file}\"");
        return file;
    }

    private sealed class NoopTransport : IModelTransport
    {
        public Task<ModelTransportResult> SendAsync(Uri endpoint, string key, string prompt, int maxTokens, CancellationToken ct) =>
            Task.FromResult(new ModelTransportResult("", 0));
    }

    // Cada pergunta do dataset recebe, na ordem, as respostas de referência do próprio caso.
    private sealed class ReferenceProvider(JsonArray cases) : IModelChatTransport
    {
        private readonly Dictionary<string, Queue<string>> _replies = cases.OfType<JsonObject>().ToDictionary(
            item => item["question"]!.GetValue<string>(),
            item => new Queue<string>(item["reference"]!.AsArray().Select(reply => reply!.ToJsonString())),
            StringComparer.Ordinal);

        public Task<ChatTransportResult> SendChatAsync(Uri endpoint, string key, string model, ChatRequest request, CancellationToken ct)
        {
            var prompt = request.Messages[^1].Content;
            var queue = _replies.First(pair => prompt.Contains(pair.Key, StringComparison.Ordinal)).Value;
            return Task.FromResult(new ChatTransportResult(queue.Count > 0 ? queue.Dequeue() : "{}", "modelo-roteirizado", 500, 120));
        }
    }

    [Fact]
    public async Task O_harness_roda_o_dataset_de_ponta_a_ponta_e_o_pontuador_python_aprova_as_respostas_de_referencia()
    {
        var dataset = JsonNode.Parse(File.ReadAllText(DatasetPath()))!.AsObject();
        var folder = Path.Combine(Path.GetTempPath(), "mgmt-eval-" + Guid.NewGuid().ToString("N")[..8]);
        Environment.SetEnvironmentVariable(OutputFolder, folder);
        try
        {
            var file = await RunAsync(ModelProviders.OpenAi.Id, "sk-chave-que-nao-vai-para-o-arquivo-77aa", new ReferenceProvider(dataset["cases"]!.AsArray()));

            var acks = JsonNode.Parse(File.ReadAllText(file))!.AsObject();
            Assert.Equal(dataset["cases"]!.AsArray().Count, acks.Count);
            Assert.All(acks, pair => Assert.Contains(pair.Value!["status"]!.GetValue<string>(), new[] { "completed", "completed_with_limitations" }));

            var script = Path.Combine(Path.GetDirectoryName(DatasetPath())!, "..", "management.py");
            var python = Environment.GetEnvironmentVariable("MARILIZ_PYTHON") is { Length: > 0 } custom ? custom : "python";
            var info = new System.Diagnostics.ProcessStartInfo(python) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, StandardOutputEncoding = System.Text.Encoding.UTF8 };
            info.ArgumentList.Add("-I");
            info.ArgumentList.Add(script);
            info.ArgumentList.Add("score");
            info.ArgumentList.Add(file);
            using var process = System.Diagnostics.Process.Start(info)!;
            var report = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            output.WriteLine(report);
            Assert.True(process.ExitCode == 0, report);
            Assert.Contains("resultado: passou", report);
        }
        finally
        {
            Environment.SetEnvironmentVariable(OutputFolder, null);
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
        }
    }

    // O dataset mora ao lado do runtime Python.
    private static string DatasetPath()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "intelligence", "evaluations", "datasets", "management.json");
            if (File.Exists(candidate)) return candidate;
        }
        throw new FileNotFoundException("O dataset das evals de gestão não foi encontrado a partir da pasta dos testes.");
    }

    private static JsonArray GroupsFor(JsonArray facts)
    {
        var present = facts.Select(fact => fact!["group"]!.GetValue<string>()).ToHashSet(StringComparer.Ordinal);
        return new JsonArray(Mariliz.Application.Management.Assistant.FactGroups.All.Where(group => present.Contains(group.Name)).Select(group => (JsonNode)new JsonObject
        {
            ["name"] = group.Name,
            ["roles"] = new JsonArray(group.Roles.Select(role => (JsonNode)role).ToArray()),
            ["keywords"] = new JsonArray(group.Keywords.Select(keyword => (JsonNode)keyword).ToArray())
        }).ToArray());
    }
}
