using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Mariliz.Application.Execution;
using Mariliz.Application.Models;
using Mariliz.Infrastructure;

namespace Mariliz.Application.Tests;

/// <summary>O proxy de modelo do motor: token por execução, orçamento de tokens, redação de segredos e uso contado.</summary>
public class ModelBrokerTests
{
    private const string ExecutionId = "ex-0123456789ab";
    private const string Key = "sk-test-chave-proxy-7c21";

    private sealed class FakeSettings(bool configured = true) : IModelSettings
    {
        public ModelStatus Status() => new(configured, configured ? "openai" : "");
        public void Save(string provider, string key) { }
        public void Clear() { }
        public bool TryGetKey(out string key)
        {
            key = configured ? Key : "";
            return configured;
        }
    }

    private sealed class FakeChat : IModelChatTransport
    {
        public int Calls;
        public int InputTokens = 100;
        public int OutputTokens = 100;
        public string Reply = "{\"schema\":\"engineering.plan.v1\",\"steps\":[]}";
        public bool Fail;
        public ChatRequest? Seen;
        public string? SeenModel;
        public bool SeenJsonObject;

        public Task<ChatTransportResult> SendChatAsync(Uri endpoint, string key, string model, ChatRequest request, CancellationToken ct)
        {
            Calls++;
            Seen = request;
            SeenModel = model;
            SeenJsonObject = request.JsonObject;
            if (Fail) throw new EngineException("MODEL_UNAVAILABLE", "falhou");
            return Task.FromResult(new ChatTransportResult(Reply, "gpt-4o-mini-2024-07-18", InputTokens, OutputTokens));
        }
    }

    private static (ModelBroker Broker, FakeChat Chat, string Token) Opened(int budget = 8000, bool configured = true)
    {
        var chat = new FakeChat();
        var broker = new ModelBroker(new ModelGateway(new FakeSettings(configured), new NoopTransport(), chat: chat));
        return (broker, chat, broker.Open(ExecutionId, budget));
    }

    private sealed class NoopTransport : IModelTransport
    {
        public Task<ModelTransportResult> SendAsync(Uri endpoint, string key, string prompt, int maxTokens, CancellationToken ct) =>
            Task.FromResult(new ModelTransportResult("", 0));
    }

    private static JsonObject Request(int maxTokens = 500, string executionId = ExecutionId, string content = "Planeje.") => new()
    {
        ["schema"] = "ai.model.request.v1",
        ["executionId"] = executionId,
        ["agentId"] = "planning",
        ["purpose"] = "plan",
        ["messages"] = new JsonArray(
            new JsonObject { ["role"] = "system", ["content"] = "Responda só com JSON." },
            new JsonObject { ["role"] = "user", ["content"] = content }),
        ["maxTokens"] = maxTokens,
        ["responseSchema"] = "engineering.plan.v1"
    };

    private static string Code(BrokerReply reply) => reply.Body["error"]!["code"]!.GetValue<string>();

    [Fact]
    public async Task Sem_o_token_da_execucao_a_chamada_e_recusada_e_o_modelo_nao_e_chamado()
    {
        var (broker, chat, token) = Opened();

        Assert.Equal(401, (await broker.HandleAsync(null, Request(), default)).HttpStatus);
        Assert.Equal(401, (await broker.HandleAsync("outro-token", Request(), default)).HttpStatus);
        Assert.Equal(401, (await broker.HandleAsync(token + "x", Request(), default)).HttpStatus);
        Assert.Equal(0, chat.Calls);
    }

    [Fact]
    public async Task Pedido_fora_do_contrato_e_recusado_antes_de_chamar_o_modelo()
    {
        var (broker, chat, token) = Opened();
        var cases = new List<JsonObject?> { null, Request(), Request(), Request(), Request(), Request() };
        cases[1]!["schema"] = "outro";
        cases[2]!["maxTokens"] = 0;
        cases[3]!["maxTokens"] = ModelGateway.MaxChatTokens + 1;
        cases[4]!["messages"] = new JsonArray();
        cases[5]!["purpose"] = "invadir";
        var badRole = Request();
        badRole["messages"] = new JsonArray(new JsonObject { ["role"] = "tool", ["content"] = "x" });
        cases.Add(badRole);

        foreach (var body in cases)
            Assert.Equal(400, (await broker.HandleAsync(token, body, default)).HttpStatus);
        Assert.Equal(0, chat.Calls);
    }

    [Fact]
    public async Task Execucao_nao_usa_o_token_de_outra()
    {
        var (broker, chat, token) = Opened();

        var reply = await broker.HandleAsync(token, Request(executionId: "ex-ffffffffffff"), default);

        Assert.Equal("MODEL_FORBIDDEN", Code(reply));
        Assert.Equal(0, chat.Calls);
    }

    [Fact]
    public async Task Modelo_e_provedor_sao_do_motor_e_a_resposta_traz_o_uso_real()
    {
        var (broker, chat, token) = Opened();
        chat.InputTokens = 812;
        chat.OutputTokens = 120;

        var reply = await broker.HandleAsync(token, Request(), default);

        Assert.Equal(200, reply.HttpStatus);
        Assert.Equal("ok", reply.Body["status"]!.GetValue<string>());
        Assert.Equal(ModelGateway.DefaultModel, chat.SeenModel);
        Assert.True(chat.SeenJsonObject);
        Assert.Equal("gpt-4o-mini-2024-07-18", reply.Body["modelVersion"]!.GetValue<string>());
        Assert.Equal(812, reply.Body["inputTokens"]!.GetValue<int>());
        Assert.Equal(120, reply.Body["outputTokens"]!.GetValue<int>());
        Assert.Equal(0, reply.Body["costUsd"]!.GetValue<double>());
        Assert.True(ContractFiles.IsValid(ContractFiles.Schema("intelligence", "ai.model.response.v1.schema.json"), reply.Body));
    }

    [Fact]
    public async Task A_fixture_de_pedido_do_contrato_passa_pelo_proxy()
    {
        var (broker, _, token) = Opened();
        var fixture = (JsonObject)ContractFiles.ReadFixture(Path.Combine("intelligence", "valid"), "ai.model.request.v1--plan.json")!;

        var reply = await broker.HandleAsync(token, fixture, default);

        Assert.Equal("ok", reply.Body["status"]!.GetValue<string>());
    }

    [Fact]
    public async Task O_orcamento_de_tokens_e_cobrado_antes_e_depois_de_cada_chamada()
    {
        var (broker, chat, token) = Opened(budget: 1000);

        // Cada chamada gasta 200 de verdade e reserva 600 como pior caso.
        for (var i = 0; i < 3; i++)
            Assert.Equal("ok", (await broker.HandleAsync(token, Request(600), default)).Body["status"]!.GetValue<string>());
        Assert.Equal(3, chat.Calls);

        // 600 já gastos + 600 pedidos passam do teto de 1000: o modelo nem é chamado.
        var refused = await broker.HandleAsync(token, Request(600), default);
        Assert.Equal("MODEL_BUDGET_EXCEEDED", Code(refused));
        Assert.Equal(3, chat.Calls);
        Assert.True(ContractFiles.IsValid(ContractFiles.Schema("intelligence", "ai.model.response.v1.schema.json"), refused.Body));

        // O que ainda cabe continua cabendo.
        Assert.Equal("ok", (await broker.HandleAsync(token, Request(300), default)).Body["status"]!.GetValue<string>());
    }

    [Fact]
    public async Task Pedido_maior_que_o_teto_inteiro_nunca_chega_ao_modelo()
    {
        var (broker, chat, token) = Opened(budget: 400);

        Assert.Equal("MODEL_BUDGET_EXCEEDED", Code(await broker.HandleAsync(token, Request(500), default)));
        Assert.Equal(0, chat.Calls);
    }

    [Fact]
    public async Task Falha_do_modelo_devolve_a_reserva_e_nao_gasta_tokens()
    {
        var (broker, chat, token) = Opened(budget: 1000);
        chat.Fail = true;

        Assert.Equal("MODEL_UNAVAILABLE", Code(await broker.HandleAsync(token, Request(800), default)));
        chat.Fail = false;
        Assert.Equal("ok", (await broker.HandleAsync(token, Request(800), default)).Body["status"]!.GetValue<string>());
        Assert.Equal(1, broker.Close(ExecutionId).Calls);
    }

    [Fact]
    public async Task Sem_modelo_configurado_o_erro_e_do_contrato_e_nada_e_gasto()
    {
        var (broker, chat, token) = Opened(configured: false);

        Assert.Equal("MODEL_NOT_CONFIGURED", Code(await broker.HandleAsync(token, Request(), default)));
        Assert.Equal(0, chat.Calls);
        Assert.Equal(ModelUsage.None, broker.Close(ExecutionId));
    }

    [Fact]
    public async Task A_chave_e_formatos_de_segredo_nao_saem_nas_mensagens_nem_voltam_na_resposta()
    {
        var (broker, chat, token) = Opened();
        chat.Reply = "{\"eco\":\"" + Key + "\"}";

        var reply = await broker.HandleAsync(token, Request(content: $"use {Key} e AKIAABCDEFGHIJKLMNOP"), default);

        var sent = string.Join(" ", chat.Seen!.Messages.Select(m => m.Content));
        Assert.DoesNotContain(Key, sent);
        Assert.DoesNotContain("AKIAABCDEFGHIJKLMNOP", sent);
        Assert.DoesNotContain(Key, reply.Body.ToJsonString());
    }

    [Fact]
    public async Task Fechar_invalida_o_token_e_devolve_o_uso_contado_pelo_motor_sem_duplicar()
    {
        var (broker, chat, token) = Opened();
        chat.InputTokens = 50;
        chat.OutputTokens = 25;
        await broker.HandleAsync(token, Request(), default);
        await broker.HandleAsync(token, Request(), default);

        var usage = broker.Close(ExecutionId);

        Assert.Equal(new ModelUsage(2, 100, 50, 0), usage);
        Assert.Equal(usage, broker.Close(ExecutionId));
        Assert.Equal(401, (await broker.HandleAsync(token, Request(), default)).HttpStatus);
        Assert.Equal(ModelUsage.None, broker.Close("ex-000000000000"));
    }

    [Fact]
    public async Task Pelo_HTTP_o_proxy_so_atende_em_127_0_0_1_com_o_token_e_so_na_rota_do_contrato()
    {
        var chat = new FakeChat();
        var broker = new ModelBroker(new ModelGateway(new FakeSettings(), new NoopTransport(), chat: chat));
        using var access = new LocalModelAccess(broker);
        var grant = access.Open(ExecutionId, 8000);
        using var http = new HttpClient();

        // O mesmo formato que o schema de ai.workflow.start.v2 exige para o endereço do proxy.
        Assert.Matches(new Regex(@"^http://127\.0\.0\.1:[0-9]{1,5}/v1/model$"), grant.Url);
        Assert.InRange(grant.Token.Length, 32, 128);

        using var ok = await Post(http, grant.Url, grant.Token, Request());
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        var body = JsonNode.Parse(await ok.Content.ReadAsStringAsync())!;
        Assert.Equal("ok", body["status"]!.GetValue<string>());

        using var noToken = await Post(http, grant.Url, null, Request());
        Assert.Equal(HttpStatusCode.Unauthorized, noToken.StatusCode);
        using var wrongToken = await Post(http, grant.Url, grant.Token + "x", Request());
        Assert.Equal(HttpStatusCode.Unauthorized, wrongToken.StatusCode);
        using var otherRoute = await Post(http, grant.Url.Replace("/v1/model", "/v1/outra"), grant.Token, Request());
        Assert.Equal(HttpStatusCode.NotFound, otherRoute.StatusCode);
        using var broken = await http.SendAsync(Authorized(HttpMethod.Post, grant.Url, grant.Token, new StringContent("{não é json", Encoding.UTF8, "application/json")));
        Assert.Equal(HttpStatusCode.BadRequest, broken.StatusCode);
        using var get = await http.SendAsync(Authorized(HttpMethod.Get, grant.Url, grant.Token, null));
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
        Assert.Equal(1, chat.Calls);

        // Duas execuções ao mesmo tempo usam o mesmo servidor, cada uma com o seu token.
        var second = access.Open("ex-aaaaaaaaaaaa", 8000);
        Assert.Equal(grant.Url, second.Url);
        Assert.NotEqual(grant.Token, second.Token);
        using var crossed = await Post(http, grant.Url, second.Token, Request());
        Assert.Equal("MODEL_FORBIDDEN", JsonNode.Parse(await crossed.Content.ReadAsStringAsync())!["error"]!["code"]!.GetValue<string>());

        Assert.Equal(1, access.Close(ExecutionId).Calls);
        using var closed = await Post(http, grant.Url, grant.Token, Request());
        Assert.Equal(HttpStatusCode.Unauthorized, closed.StatusCode);
    }

    private static Task<HttpResponseMessage> Post(HttpClient http, string url, string? token, JsonObject body) =>
        http.SendAsync(Authorized(HttpMethod.Post, url, token, new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json")));

    private static HttpRequestMessage Authorized(HttpMethod method, string url, string? token, HttpContent? content)
    {
        var request = new HttpRequestMessage(method, url) { Content = content };
        if (token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }
}
