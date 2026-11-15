using System.Text.Json;
using System.Text.Json.Nodes;
using Mariliz.Application.Context;
using Mariliz.Application.Execution;
using Mariliz.Application.Models;
using Mariliz.Application.Ports;
using Mariliz.Application.Protocol;
using Mariliz.Application.Tests.Support;
using Mariliz.Application.Workflows;
using Mariliz.Application.Workspace;
using Mariliz.Infrastructure;

namespace Mariliz.Application.Tests;

public sealed class ContextReviewTests : IDisposable
{
    private const string Secret = "TEXTO-DO-PROJETO-QUE-NAO-SAI";
    private readonly TempProject _project = TempProject.Dotnet();
    private readonly WorkspaceAuthorizer _workspace = new(new LocalWorkspaceFileSystem());
    private readonly string _projectId;

    public ContextReviewTests() => _projectId = _workspace.Authorize(_project.Root).Project!.ProjectId;

    public void Dispose() => _project.Dispose();

    private sealed class FakeContext(JsonObject? built = null) : IWorkflowContext
    {
        public string DataRoot => "C:/dados";

        public Task<JsonObject> BuildAsync(string projectId, CancellationToken ct) => Task.FromResult(built ?? new JsonObject
        {
            ["available"] = true,
            ["stacks"] = new JsonArray("dotnet"),
            ["knowledgeRoot"] = "C:/dados",
            ["sources"] = new JsonArray(new JsonObject { ["path"] = "src/a.cs", ["hash"] = new string('a', 64) })
        });
    }

    private sealed class FakePreview(Func<JsonObject, JsonObject> reply) : IContextPreviewRuntime
    {
        public JsonObject? Request { get; private set; }

        public Task<JsonObject> PreviewContextAsync(JsonObject request, CancellationToken ct)
        {
            Request = request;
            return Task.FromResult(reply(request));
        }
    }

    // Uma palavra, um token: deixa as contas do teste legíveis.
    private sealed class WordTokenizers : ITokenizers
    {
        public TokenCounter For(string? provider, string? model) =>
            new(text => text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length, false, "palavras");
    }

    private static string Words(int count) => string.Join(' ', Enumerable.Repeat("w", count));

    private static JsonObject Call(string agentId, int system, int user, int maxOutput = 1024) => new()
    {
        ["agentId"] = agentId,
        ["maxOutputTokens"] = maxOutput,
        ["messages"] = new JsonArray(
            new JsonObject { ["role"] = "system", ["content"] = Words(system) },
            new JsonObject { ["role"] = "user", ["content"] = Words(user) })
    };

    private static JsonObject Reply(JsonArray calls, JsonArray? evidence = null, int pending = 0) => new()
    {
        ["schema"] = "ai.context.preview.ack.v1",
        ["calls"] = calls,
        ["evidence"] = evidence ?? [],
        ["index"] = new JsonObject { ["documents"] = 3, ["pending"] = pending, ["valid"] = true }
    };

    private static JsonObject Piece(string path, int words, bool included, bool truncated = false) => new()
    {
        ["chunkId"] = path + "#1", ["filePath"] = path, ["text"] = Words(words) + " " + Secret, ["included"] = included, ["truncated"] = truncated
    };

    private ContextReviewService Service(FakePreview preview, IWorkflowContext? context = null) =>
        new(_workspace, context ?? new FakeContext(), preview, new WordTokenizers());

    [Fact]
    public async Task Conta_cada_chamada_por_parte_e_soma_o_pior_caso_da_execucao()
    {
        var preview = new FakePreview(_ => Reply(
            [Call("planning", 100, 300), Call("architecture", 80, 300)],
            [Piece("src/frete.cs", 199, included: true)]));

        var review = await Service(preview).ReviewAsync(_projectId, "  Corrigir o frete  ", CancellationToken.None);

        var planning = review.Calls[0];
        Assert.Equal("Planejamento", planning.Label);
        Assert.Equal(100 + 4 + 300 + 4 + 3, planning.InputTokens);
        Assert.Equal(1024, planning.OutputTokens);
        Assert.Equal([104, 107, 200], planning.Sections.Select(section => section.Tokens));
        Assert.Equal(planning.InputTokens, planning.Sections.Sum(section => section.Tokens));
        Assert.Equal("Arquitetura", review.Calls[1].Label);
        Assert.Equal(review.Calls.Sum(call => call.InputTokens + call.OutputTokens), review.PlannedTokens);
        Assert.Equal(ModelBudgets.Default(ModelPurposes.Workflow), review.Budget);
        Assert.False(review.Model.Configured);
        Assert.False(review.Counting.Exact);
        Assert.Equal("Corrigir o frete", preview.Request!["goal"]!.GetValue<string>());
        Assert.Equal("ai.context.preview.v1", preview.Request["schema"]!.GetValue<string>());
        Assert.Equal("src/a.cs", preview.Request["context"]!["sources"]![0]!["path"]!.GetValue<string>());
    }

    private sealed class NoTransport : IModelTransport
    {
        public Task<ModelTransportResult> SendAsync(Uri endpoint, string key, string prompt, int maxTokens, CancellationToken ct) =>
            throw new InvalidOperationException("A revisão não chama o modelo.");
    }

    [Fact]
    public async Task O_teto_configurado_para_os_fluxos_decide_o_que_cabe()
    {
        var budgets = new FileModelBudgets(_project.DataDir);
        var models = new ModelGateway(new FileModelSettings(_project.DataDir), new NoTransport(), budgets: budgets);
        models.SetBudget(ModelPurposes.Workflow, 2_000);
        var preview = new FakePreview(_ => Reply([Call("planning", 100, 300), Call("architecture", 80, 300)]));

        var review = await new ContextReviewService(_workspace, new FakeContext(), preview, new WordTokenizers(), models)
            .ReviewAsync(_projectId, "Corrigir o frete", CancellationToken.None);

        Assert.Equal(2_000, review.Budget);
        Assert.Equal([true, false], review.Calls.Select(call => call.WithinBudget));
    }

    [Fact]
    public async Task A_chamada_que_estoura_o_orcamento_aparece_marcada()
    {
        var preview = new FakePreview(_ => Reply([Call("planning", 100, 3000), Call("architecture", 100, 3000)]));

        var review = await Service(preview).ReviewAsync(_projectId, "frete", CancellationToken.None);

        Assert.True(review.Calls[0].WithinBudget);
        Assert.False(review.Calls[1].WithinBudget);
        Assert.True(review.PlannedTokens > review.Budget);
    }

    [Fact]
    public async Task Resume_o_que_ficou_de_fora_e_nao_devolve_o_texto_do_projeto()
    {
        var preview = new FakePreview(_ => Reply([Call("planning", 10, 10)],
            [Piece("src/a.cs", 5, included: true, truncated: true), Piece("src/b.cs", 5, included: false), Piece("@decision/dec-1.md", 5, included: false)],
            pending: 2));

        var review = await Service(preview).ReviewAsync(_projectId, "frete", CancellationToken.None);

        Assert.Equal(2, review.LeftOut.CutBySize);
        Assert.Equal(1, review.LeftOut.Truncated);
        Assert.Equal(2, review.LeftOut.Pending);
        Assert.Equal(["src/a.cs", "src/b.cs", "@decision/dec-1.md"], review.Evidence.Select(piece => piece.Path));
        Assert.Equal(6, review.Evidence[0].Tokens);
        Assert.DoesNotContain(Secret, JsonSerializer.Serialize(review));
    }

    [Fact]
    public async Task Recusa_projeto_nao_autorizado_pedido_vazio_ou_longo_demais()
    {
        var service = Service(new FakePreview(_ => Reply([])));

        Assert.Equal("PROJECT_NOT_AUTHORIZED",
            (await Assert.ThrowsAsync<EngineException>(() => service.ReviewAsync("p-000000000000", "frete", CancellationToken.None))).Code);
        await Assert.ThrowsAsync<ParamException>(() => service.ReviewAsync(_projectId, "   ", CancellationToken.None));
        await Assert.ThrowsAsync<ParamException>(() => service.ReviewAsync(_projectId, new string('x', ContextReviewService.MaxGoal + 1), CancellationToken.None));
    }

    [Fact]
    public async Task Sem_indice_ou_com_resposta_fora_do_contrato_nao_inventa_numero()
    {
        var noIndex = Service(new FakePreview(_ => Reply([])), new FakeContext(new JsonObject { ["available"] = true, ["stacks"] = new JsonArray() }));
        var broken = Service(new FakePreview(_ => new JsonObject { ["schema"] = "outro" }));

        Assert.Equal("CONTEXT_UNAVAILABLE",
            (await Assert.ThrowsAsync<EngineException>(() => noIndex.ReviewAsync(_projectId, "frete", CancellationToken.None))).Code);
        Assert.Equal(EngineErrors.AiRuntimeInvalidResponse().Code,
            (await Assert.ThrowsAsync<EngineException>(() => broken.ReviewAsync(_projectId, "frete", CancellationToken.None))).Code);
    }

    [Fact]
    public void Modelo_da_OpenAI_conta_exato_e_os_outros_ficam_marcados_como_estimativa()
    {
        var tokenizers = new TiktokenTokenizers();

        var openAi = tokenizers.For("openai", "gpt-4o-mini");
        var anthropic = tokenizers.For("anthropic", "claude-haiku-5-5");
        var none = tokenizers.For(null, null);

        Assert.True(openAi.Exact);
        Assert.Equal(2, openAi.Count("hello world"));
        Assert.False(anthropic.Exact);
        Assert.Contains("Estimativa", anthropic.Method);
        Assert.False(none.Exact);
        Assert.True(anthropic.Count("Corrigir o cálculo de frete do carrinho") > 0);
    }

    [Fact]
    public void Fixture_de_contrato_da_revisao_de_contexto()
    {
        var preview = new FakePreview(_ => Reply([Call("planning", 100, 300), Call("architecture", 80, 300)],
            [Piece("src/frete.cs", 199, included: true), Piece("src/carrinho.cs", 50, included: false, truncated: true)], pending: 1));
        var session = new EngineSession("test", contextReview: Service(preview));

        var response = session.Handle(new JsonObject
        {
            ["v"] = 1, ["kind"] = "request", ["id"] = "t", ["method"] = ProtocolMethods.ContextReview,
            ["params"] = new JsonObject { ["projectId"] = _projectId, ["goal"] = "Corrigir o frete" }
        }.ToJsonString()).Response;
        var result = response["result"]?.AsObject() ?? throw new InvalidOperationException(response.ToJsonString());
        result["projectId"] = "p-000000000001";

        var pretty = result.ToJsonString(new JsonSerializerOptions
        {
            WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        }).ReplaceLineEndings("\n");
        var path = ContractFiles.Path_("fixtures", "context", "review.json");
        if (Environment.GetEnvironmentVariable("MARILIZ_UPDATE_GOLDEN") == "1" || !File.Exists(path))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, pretty + "\n");
            return;
        }

        Assert.True(pretty == File.ReadAllText(path).ReplaceLineEndings("\n").TrimEnd(),
            "context.review: o resultado mudou. Se foi deliberado, regenere com MARILIZ_UPDATE_GOLDEN=1 e ajuste o Zod da interface.");
    }
}
