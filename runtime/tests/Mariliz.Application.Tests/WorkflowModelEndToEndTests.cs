using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Mariliz.Application.Execution;
using Mariliz.Application.Models;
using Mariliz.Application.Ports;
using Mariliz.Application.Tests.Support;
using Mariliz.Application.Workflows;
using Mariliz.Application.Workspace;
using Mariliz.Infrastructure;

namespace Mariliz.Application.Tests;

/// <summary>
/// A cadeia inteira da Fase 6, sem rede: o grafo Python de verdade chama o proxy do motor em 127.0.0.1, o proxy cobra o
/// orçamento e fala com um transporte falso. O que se confere é o que cruza as duas metades.
/// </summary>
public class WorkflowModelEndToEndTests
{
    private const string Key = "sk-test-chave-e2e-5d10";

    private sealed class Settings : IModelSettings
    {
        public ModelStatus Status() => new(true, "openai");
        public void Save(string provider, string key) { }
        public void Clear() { }
        public bool TryGetKey(out string key)
        {
            key = Key;
            return true;
        }
    }

    private sealed class NoopTransport : IModelTransport
    {
        public Task<ModelTransportResult> SendAsync(Uri endpoint, string key, string prompt, int maxTokens, CancellationToken ct) =>
            Task.FromResult(new ModelTransportResult("", 0));
    }

    /// <summary>Faz o papel do provedor: lê o trecho que o agente recebeu e escreve um plano que o cita.</summary>
    private sealed class ScriptedProvider : IModelChatTransport
    {
        public List<ChatRequest> Requests { get; } = [];
        public string? Mode { get; set; }

        public Task<ChatTransportResult> SendChatAsync(Uri endpoint, string key, string model, ChatRequest request, CancellationToken ct)
        {
            lock (Requests) Requests.Add(request);
            var user = request.Messages[^1].Content;
            var chunk = Regex.Match(user, @"<conteudo-do-projeto>\n\[([^\]\n]+)\]").Groups[1].Value;
            var isJudge = request.Messages[0].Content.Contains("avaliador independente", StringComparison.Ordinal);
            var withImpact = !user.Contains("\"impact\": null", StringComparison.Ordinal);
            var reply = Mode == "invalid"
                ? "isto não é JSON"
                : isJudge ? Judgment(chunk, withImpact)
                : new JsonObject
                {
                    ["schema"] = "engineering.plan.v1",
                    ["steps"] = new JsonArray(
                        new JsonObject { ["id"] = "ler-cobranca", ["title"] = "Ler o módulo de cobrança", ["evidence"] = new JsonArray(chunk) },
                        new JsonObject { ["id"] = "propor", ["title"] = "Propor a mudança" },
                        new JsonObject { ["id"] = "validar", ["title"] = "Validar com os testes" })
                }.ToJsonString();
            return Task.FromResult(new ChatTransportResult(reply, "gpt-4o-mini-2024-07-18", 400, 120));
        }
    }

    /// <summary>A opinião que o juiz falso escreve: notas boas, citando o trecho que ele recebeu.</summary>
    private static string Judgment(string chunk, bool withImpact)
    {
        var scores = new JsonObject { ["plan_quality"] = 4, ["context_fit"] = 4, ["request_fit"] = 5 };
        if (withImpact) scores["architecture_coherence"] = 4;
        return new JsonObject
        {
            ["schema"] = "engineering.judgment.v1",
            ["criteriaVersion"] = "1",
            ["scores"] = scores,
            ["rationale"] = "O plano é concreto e cita o módulo de cobrança.",
            ["evidence"] = new JsonArray(chunk)
        }.ToJsonString();
    }

    private static (WorkflowService Service, ScriptedProvider Provider, LoopbackAiRuntime Runtime, string ProjectId, TempProject Project) Build()
    {
        var project = TempProject.Dotnet();
        project.Write("docs/cobranca.md", "# Cobrança\n\nO módulo de cobrança recorrente fatura assinaturas todos os meses.\n");
        var provider = new ScriptedProvider();
        var gateway = new ModelGateway(new Settings(), new NoopTransport(), chat: provider);
        var access = new LocalModelAccess(new ModelBroker(gateway));
        var authorizer = new WorkspaceAuthorizer(new LocalWorkspaceFileSystem());
        var projectId = authorizer.Authorize(project.Root).Project!.ProjectId;
        var runtime = new LoopbackAiRuntime();
        var service = new WorkflowService(authorizer, runtime, new RunTracker(), new SystemClock(), new FileRunStore(project.DataDir),
            new WorkflowContextBuilder(authorizer, runtime, new TestFilesFactory(), new SystemProcessRunner(), project.DataDir), access,
            new FileEvalRecords(project.DataDir));
        return (service, provider, runtime, projectId, project);
    }

    private static async Task<RunHandle> RunAsync(WorkflowService service, string projectId, bool useModel, bool impact = false)
    {
        var input = new JsonObject { ["goal"] = "descrever o módulo de cobrança recorrente", ["impact"] = impact, ["useModel"] = useModel };
        var run = await service.StartAsync(projectId, input, CancellationToken.None);
        await run.Completion!;
        return run;
    }

    [Fact]
    public async Task O_agente_chama_o_proxy_do_motor_e_o_plano_do_modelo_chega_ao_resultado_com_o_uso_contado_pelo_motor()
    {
        var (service, provider, runtime, projectId, project) = Build();
        using var _ = project;
        try
        {
            var run = await RunAsync(service, projectId, useModel: true);

            Assert.Equal(RunStates.CompletedWithLimitations, run.State);
            // O plano determinístico teria 3 etapas (describe, context, evidence): a contagem do modelo é a do roteiro.
            Assert.StartsWith("3 etapa(s)", run.Result!["summary"]!.GetValue<string>());
            var usage = run.Result["usage"]!;
            Assert.Equal(provider.Requests.Count, usage["modelCalls"]!.GetValue<int>());
            Assert.Equal(400 * provider.Requests.Count, usage["inputTokens"]!.GetValue<int>());
            Assert.Equal(120 * provider.Requests.Count, usage["outputTokens"]!.GetValue<int>());
            Assert.DoesNotContain(run.Result["limitations"]!.AsArray(), item => item!["code"]!.GetValue<string>() == "PLAN_MODEL_FALLBACK");

            // O que saiu para o provedor: o trecho do projeto no bloco delimitado, e nunca a chave.
            var sent = string.Join("\n", provider.Requests.SelectMany(r => r.Messages).Select(m => m.Content));
            Assert.Contains("<conteudo-do-projeto>", sent);
            Assert.Contains("cobrança recorrente", sent);
            Assert.DoesNotContain(Key, sent);
        }
        finally
        {
            await runtime.ShutdownAsync();
        }
    }

    [Fact]
    public async Task Com_impacto_os_dois_agentes_usam_o_modelo_e_o_orcamento_do_motor_aguenta()
    {
        var (service, provider, runtime, projectId, project) = Build();
        using var _ = project;
        try
        {
            var run = await RunAsync(service, projectId, useModel: true, impact: true);

            Assert.True(run.State is RunStates.CompletedWithLimitations or RunStates.Completed, run.State);
            // O mesmo roteiro responde um plano às duas chamadas: o Architecture o recusa (formato de impacto) e cai nas regras.
            Assert.Contains(run.Result!["limitations"]!.AsArray(), item => item!["code"]!.GetValue<string>() == "IMPACT_MODEL_FALLBACK");
            Assert.NotNull(run.Result["impact"]);
            var calls = run.Result["usage"]!["modelCalls"]!.GetValue<int>();
            Assert.Equal(provider.Requests.Count, calls);
            Assert.InRange(calls, 4, 5);
            Assert.InRange(run.Result["usage"]!["inputTokens"]!.GetValue<int>() + run.Result["usage"]!["outputTokens"]!.GetValue<int>(), 1, WorkflowRequests.ModelTokenBudget);
        }
        finally
        {
            await runtime.ShutdownAsync();
        }
    }

    [Fact]
    public async Task Resposta_fora_do_contrato_cai_no_plano_deterministico_e_o_gasto_continua_registrado()
    {
        var (service, provider, runtime, projectId, project) = Build();
        using var _ = project;
        provider.Mode = "invalid";
        try
        {
            var run = await RunAsync(service, projectId, useModel: true);

            Assert.Equal(RunStates.CompletedWithLimitations, run.State);
            Assert.StartsWith("3 etapa(s)", run.Result!["summary"]!.GetValue<string>());
            var codes = run.Result["limitations"]!.AsArray().Select(item => item!["code"]!.GetValue<string>()).ToList();
            Assert.Contains("PLAN_MODEL_FALLBACK", codes);
            Assert.Contains("MODEL_OUTPUT_INVALID", codes);
            // Duas tentativas do Planning e duas do juiz: o gasto de cada uma continua registrado.
            Assert.Equal(4, run.Result["usage"]!["modelCalls"]!.GetValue<int>());
            Assert.Equal("not_run", run.Result["evaluation"]!["judge"]!.GetValue<string>());
        }
        finally
        {
            await runtime.ShutdownAsync();
        }
    }

    [Fact]
    public async Task O_juiz_roda_pelo_proxy_e_o_motor_registra_a_opiniao_com_o_modelo_e_o_hash_do_que_foi_enviado()
    {
        var (service, provider, runtime, projectId, project) = Build();
        using var _ = project;
        try
        {
            var run = await RunAsync(service, projectId, useModel: true);

            var evaluation = run.Result!["evaluation"]!;
            Assert.Equal("deterministic", evaluation["verdict"]!.GetValue<string>());
            Assert.Equal("passed", evaluation["judge"]!.GetValue<string>());
            Assert.Equal(ModelGateway.DefaultModel, evaluation["judgment"]!["model"]!.GetValue<string>());
            Assert.Equal("gpt-4o-mini-2024-07-18", evaluation["judgment"]!["modelVersion"]!.GetValue<string>());

            // O registro em disco é do motor e leva o hash exato das mensagens que o juiz mandou ao provedor.
            var executionId = run.Result["executionId"]!.GetValue<string>();
            var path = Path.Combine(project.DataDir, "projects", projectId, "evals", executionId + ".json");
            Assert.True(File.Exists(path));
            var record = JsonNode.Parse(File.ReadAllText(path))!;
            Assert.True(ContractFiles.IsValid(ContractFiles.Schema("intelligence", "engineering.eval.record.v1.schema.json"), record));
            var judgeRequest = provider.Requests.Single(r => r.Messages[0].Content.Contains("avaliador independente", StringComparison.Ordinal));
            var sentJson = System.Text.Json.JsonSerializer.Serialize(judgeRequest.Messages.Select(m => new { role = m.Role, content = m.Content }));
            var expectedHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(sentJson))).ToLowerInvariant();
            Assert.Equal(expectedHash, record["promptHash"]!.GetValue<string>());
            Assert.Equal(400, record["inputTokens"]!.GetValue<int>());

            // O juiz só vê o resultado final: nada das tentativas nem das recusas de quem escreveu.
            Assert.DoesNotContain("A resposta foi recusada", judgeRequest.Messages[1].Content);

            var types = run.Snapshot()["events"]!.AsArray().Select(e => e!["type"]!.GetValue<string>()).Where(t => t.StartsWith("eval.", StringComparison.Ordinal));
            Assert.Equal(["eval.started", "eval.completed"], types);
        }
        finally
        {
            await runtime.ShutdownAsync();
        }
    }

    [Fact]
    public async Task Sem_pedir_modelo_o_provedor_nunca_e_chamado()
    {
        var (service, provider, runtime, projectId, project) = Build();
        using var _ = project;
        try
        {
            var run = await RunAsync(service, projectId, useModel: false);

            Assert.Empty(provider.Requests);
            Assert.Null(run.Result!["usage"]);
        }
        finally
        {
            await runtime.ShutdownAsync();
        }
    }
}
