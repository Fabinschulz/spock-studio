using System.Text.Json;
using System.Text.Json.Nodes;
using Mariliz.Application.Execution;
using Mariliz.Application.Models;
using Mariliz.Application.Protocol;
using Mariliz.Application.Tests.Support;
using Mariliz.Infrastructure;

namespace Mariliz.Application.Tests;

internal sealed class MemoryPreferences(params (string Purpose, string Model)[] initial) : IModelPreferences
{
    private readonly Dictionary<string, string> _choices = initial.ToDictionary(pair => pair.Purpose, pair => pair.Model);

    public IReadOnlyDictionary<string, string> Read() => new Dictionary<string, string>(_choices);

    public void Write(string purpose, string? model)
    {
        if (model is null) _choices.Remove(purpose);
        else _choices[purpose] = model;
    }

    public void Clear() => _choices.Clear();
}

// Quem escolhe é sempre a pessoa: o agente declara a finalidade, nunca o modelo.
public class ModelPurposesTests
{
    private const string OpenAiKey = "sk-test-openai-fase-f-7c2d41";
    private const string AnthropicKey = "sk-ant-test-anthropic-fase-f-91ab_x";

    private sealed class Capture : IModelTransport, IModelChatTransport
    {
        public List<(Uri Endpoint, string Model)> Chats { get; } = [];

        public Task<ModelTransportResult> SendAsync(Uri endpoint, string key, string prompt, int maxTokens, CancellationToken ct) =>
            Task.FromResult(new ModelTransportResult("ok", 2));

        public Task<ChatTransportResult> SendChatAsync(Uri endpoint, string key, string model, ChatRequest request, CancellationToken ct)
        {
            Chats.Add((endpoint, model));
            return Task.FromResult(new ChatTransportResult("resposta", "versao", 3, 4));
        }
    }

    private sealed class Setup : IDisposable
    {
        public TempProject Project { get; } = TempProject.Dotnet();
        public Capture Transport { get; } = new();
        public ModelGateway Gateway { get; }
        public string DataDir => Project.DataDir;

        public Setup() => Gateway = New();

        // Como depois de reiniciar o aplicativo.
        public ModelGateway New() => new(new FileModelSettings(DataDir), Transport, chat: Transport,
            checks: new FileModelCheckStore(DataDir), preferences: new FileModelPreferences(DataDir));

        public void Dispose() => Project.Dispose();
    }

    private static readonly ChatRequest Chat = new([new ChatMessage("user", "oi")], 32, false);

    private static string ModelOf(ModelConnection connection, string purpose) => connection.Purposes.Single(item => item.Id == purpose).Model;

    [Fact]
    public void As_finalidades_sao_tres_com_nome_e_descricao_sem_travessao()
    {
        Assert.Equal(["assistant", "judge", "workflow"], ModelPurposes.All.Select(purpose => purpose.Id));
        Assert.All(ModelPurposes.All, purpose =>
        {
            Assert.NotEmpty(purpose.Label);
            Assert.NotEmpty(purpose.Description);
            Assert.DoesNotContain("\u2014", purpose.Label + purpose.Description);
        });
        Assert.Null(ModelPurposes.Find("outra"));
    }

    [Theory]
    [InlineData("judge", "judge")]
    [InlineData("classify", "workflow")]
    [InlineData("plan", "workflow")]
    [InlineData("architecture", "workflow")]
    [InlineData("rewrite-query", "workflow")]
    [InlineData("investigate", "assistant")]
    [InlineData("qualquer-outra", "workflow")]
    public void O_runtime_declara_o_agente_e_o_motor_traduz_para_a_finalidade(string brokerPurpose, string expected)
    {
        Assert.Equal(expected, ModelPurposes.ForBrokerPurpose(brokerPurpose));
    }

    [Fact]
    public void O_padrao_de_cada_provedor_esta_entre_os_modelos_que_ele_oferece()
    {
        Assert.All(ModelProviders.All, provider =>
        {
            Assert.Contains(provider.Models, option => option.Id == provider.DefaultModel);
            Assert.Equal(provider.Models.Count, provider.Models.Select(option => option.Id).Distinct().Count());
            Assert.All(provider.Models, option => Assert.NotEmpty(option.Name));
        });
        Assert.True(ModelProviders.Anthropic.Models.Count > 1);
    }

    [Fact]
    public async Task Sem_escolha_toda_finalidade_usa_o_padrao_do_provedor()
    {
        using var s = new Setup();
        await s.Gateway.ConnectAsync("openai", OpenAiKey, CancellationToken.None);

        var purposes = s.Gateway.Connection().Purposes;

        Assert.Equal(["assistant", "judge", "workflow"], purposes.Select(item => item.Id));
        Assert.All(purposes, item => Assert.Equal((ModelProviders.OpenAi.DefaultModel, true), (item.Model, item.IsDefault)));
        Assert.All(purposes, item => Assert.NotEmpty(item.Label));
    }

    [Fact]
    public async Task Sem_chave_nao_ha_finalidades_para_mostrar_e_escolher_falha()
    {
        using var s = new Setup();

        Assert.Empty(s.Gateway.Connection().Purposes);
        var error = Assert.Throws<EngineException>(() => s.Gateway.SetPurposeModel("judge", "gpt-4o"));
        Assert.Equal("MODEL_NOT_CONFIGURED", error.Code);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task Escolher_um_modelo_para_o_juiz_muda_so_o_juiz_e_sobrevive_a_reiniciar()
    {
        using var s = new Setup();
        await s.Gateway.ConnectAsync("openai", OpenAiKey, CancellationToken.None);

        var connection = s.Gateway.SetPurposeModel("judge", "gpt-4o");

        Assert.Equal("gpt-4o", ModelOf(connection, "judge"));
        Assert.False(connection.Purposes.Single(item => item.Id == "judge").IsDefault);
        Assert.Equal(ModelProviders.OpenAi.DefaultModel, ModelOf(connection, "assistant"));
        Assert.Equal(ModelProviders.OpenAi.DefaultModel, ModelOf(connection, "workflow"));
        Assert.Equal("gpt-4o", ModelOf(s.New().Connection(), "judge"));
    }

    [Fact]
    public async Task Voltar_ao_padrao_apaga_a_escolha_e_escolher_o_proprio_padrao_tambem()
    {
        using var s = new Setup();
        await s.Gateway.ConnectAsync("openai", OpenAiKey, CancellationToken.None);
        s.Gateway.SetPurposeModel("judge", "gpt-4o");
        var file = Path.Combine(s.DataDir, "model-purposes.json");
        Assert.True(File.Exists(file));

        s.Gateway.SetPurposeModel("judge", null);
        Assert.False(File.Exists(file));

        s.Gateway.SetPurposeModel("judge", "gpt-4o");
        var again = s.Gateway.SetPurposeModel("judge", ModelProviders.OpenAi.DefaultModel);
        Assert.True(again.Purposes.Single(item => item.Id == "judge").IsDefault);
        Assert.False(File.Exists(file));
    }

    [Fact]
    public async Task Modelo_que_nao_e_do_provedor_e_finalidade_inexistente_sao_recusados()
    {
        using var s = new Setup();
        await s.Gateway.ConnectAsync("openai", OpenAiKey, CancellationToken.None);

        var foreign = Assert.Throws<EngineException>(() => s.Gateway.SetPurposeModel("judge", "claude-opus-5-5"));
        var free = Assert.Throws<EngineException>(() => s.Gateway.SetPurposeModel("judge", "qualquer-coisa"));
        var unknown = Assert.Throws<EngineException>(() => s.Gateway.SetPurposeModel("outra", "gpt-4o"));

        Assert.Equal(("MODEL_CHOICE_DENIED", "MODEL_CHOICE_DENIED", "MODEL_PURPOSE_UNKNOWN"), (foreign.Code, free.Code, unknown.Code));
        Assert.False(File.Exists(Path.Combine(s.DataDir, "model-purposes.json")));
    }

    [Fact]
    public async Task Cada_finalidade_chama_o_modelo_que_a_pessoa_escolheu_para_ela()
    {
        using var s = new Setup();
        await s.Gateway.ConnectAsync("anthropic", AnthropicKey, CancellationToken.None);
        s.Gateway.SetPurposeModel("judge", "claude-opus-5-5");
        s.Gateway.SetPurposeModel("assistant", "claude-sonnet-5-5");

        var judge = await s.Gateway.ChatAsync(Chat, CancellationToken.None, ModelPurposes.Judge);
        var assistant = await s.Gateway.ChatAsync(Chat, CancellationToken.None, ModelPurposes.Assistant);
        var workflow = await s.Gateway.ChatAsync(Chat, CancellationToken.None, ModelPurposes.Workflow);
        var unspecified = await s.Gateway.ChatAsync(Chat, CancellationToken.None);

        Assert.Equal(["claude-opus-5-5", "claude-sonnet-5-5", "claude-haiku-5-5", "claude-haiku-5-5"], s.Transport.Chats.Select(call => call.Model));
        Assert.Equal(["claude-opus-5-5", "claude-sonnet-5-5", "claude-haiku-5-5", "claude-haiku-5-5"], new[] { judge, assistant, workflow, unspecified }.Select(done => done.Model));
        Assert.All(s.Transport.Chats, call => Assert.Equal(ModelProviders.Anthropic.Endpoint, call.Endpoint));
    }

    [Fact]
    public async Task O_juiz_do_runtime_chega_ao_modelo_escolhido_para_o_juiz_e_o_resto_ao_dos_fluxos()
    {
        using var s = new Setup();
        await s.Gateway.ConnectAsync("openai", OpenAiKey, CancellationToken.None);
        s.Gateway.SetPurposeModel("judge", "gpt-4o");
        var broker = new ModelBroker(s.Gateway);
        var token = broker.Open("ex-000000000001", 10_000);

        JsonObject Body(string purpose) => new()
        {
            ["schema"] = "ai.model.request.v1",
            ["executionId"] = "ex-000000000001",
            ["agentId"] = purpose == "judge" ? "judge" : "planning",
            ["purpose"] = purpose,
            ["maxTokens"] = 100,
            ["messages"] = new JsonArray(new JsonObject { ["role"] = "user", ["content"] = "oi" })
        };

        var judge = await broker.HandleAsync(token, Body("judge"), CancellationToken.None);
        var plan = await broker.HandleAsync(token, Body("plan"), CancellationToken.None);

        Assert.Equal("gpt-4o", judge.Body["model"]!.GetValue<string>());
        Assert.Equal(ModelProviders.OpenAi.DefaultModel, plan.Body["model"]!.GetValue<string>());
        Assert.Equal([("judge", "gpt-4o"), ("plan", ModelProviders.OpenAi.DefaultModel)], broker.Calls("ex-000000000001").Select(call => (call.Purpose, call.Model)));
    }

    [Fact]
    public async Task O_agente_nao_escolhe_modelo_nem_finalidade_fora_da_lista()
    {
        using var s = new Setup();
        await s.Gateway.ConnectAsync("openai", OpenAiKey, CancellationToken.None);
        s.Gateway.SetPurposeModel("judge", "gpt-4o");
        var broker = new ModelBroker(s.Gateway);
        var token = broker.Open("ex-000000000001", 10_000);
        var body = new JsonObject
        {
            ["schema"] = "ai.model.request.v1",
            ["executionId"] = "ex-000000000001",
            ["agentId"] = "judge",
            ["purpose"] = "assistant",
            ["model"] = "gpt-4o",
            ["maxTokens"] = 100,
            ["messages"] = new JsonArray(new JsonObject { ["role"] = "user", ["content"] = "oi" })
        };

        var reply = await broker.HandleAsync(token, body, CancellationToken.None);

        // "assistant" não é uma finalidade que o runtime possa declarar, e um campo "model" no pedido não é lido.
        Assert.Equal(400, reply.HttpStatus);
        Assert.Empty(s.Transport.Chats);
    }

    [Fact]
    public async Task Com_uma_chave_so_trocar_de_provedor_faz_a_finalidade_seguir_o_novo_e_reconectar_o_antigo_revive_a_escolha()
    {
        using var s = new Setup();
        await s.Gateway.ConnectAsync("openai", OpenAiKey, CancellationToken.None);
        s.Gateway.SetPurposeModel("judge", "gpt-4o");

        await s.Gateway.ConnectAsync("openai", OpenAiKey, CancellationToken.None);
        Assert.Equal("gpt-4o", ModelOf(s.Gateway.Connection(), "judge"));

        var switched = await s.Gateway.ConnectAsync("anthropic", AnthropicKey, CancellationToken.None);

        Assert.All(switched.Purposes, item => Assert.Equal((ModelProviders.Anthropic.DefaultModel, true), (item.Model, item.IsDefault)));
        Assert.True(File.Exists(Path.Combine(s.DataDir, "model-purposes.json")));

        await s.Gateway.ConnectAsync("openai", OpenAiKey, CancellationToken.None);
        Assert.Equal("gpt-4o", ModelOf(s.Gateway.Connection(), "judge"));
    }

    [Fact]
    public async Task Escolha_de_outro_provedor_que_sobrou_no_arquivo_vale_como_nao_escolhida()
    {
        using var s = new Setup();
        await s.Gateway.ConnectAsync("openai", OpenAiKey, CancellationToken.None);
        File.WriteAllText(Path.Combine(s.DataDir, "model-purposes.json"), new JsonObject { ["judge"] = "claude-opus-5-5", ["workflow"] = "gpt-4o" }.ToJsonString());

        var connection = s.Gateway.Connection();

        Assert.Equal(ModelProviders.OpenAi.DefaultModel, ModelOf(connection, "judge"));
        Assert.Equal("gpt-4o", ModelOf(connection, "workflow"));
        await s.Gateway.ChatAsync(Chat, CancellationToken.None, ModelPurposes.Judge);
        Assert.Equal(ModelProviders.OpenAi.DefaultModel, s.Transport.Chats.Single().Model);
    }

    [Theory]
    [InlineData("{ nao e json")]
    [InlineData("[1,2]")]
    [InlineData("{\"judge\":42,\"outra\":\"gpt-4o\"}")]
    public async Task Arquivo_ilegivel_ou_com_finalidade_desconhecida_vale_tudo_no_padrao(string content)
    {
        using var s = new Setup();
        await s.Gateway.ConnectAsync("openai", OpenAiKey, CancellationToken.None);
        File.WriteAllText(Path.Combine(s.DataDir, "model-purposes.json"), content);

        Assert.All(s.Gateway.Connection().Purposes, item => Assert.True(item.IsDefault));
    }

    [Fact]
    public async Task Remover_a_chave_apaga_as_escolhas()
    {
        using var s = new Setup();
        await s.Gateway.ConnectAsync("openai", OpenAiKey, CancellationToken.None);
        s.Gateway.SetPurposeModel("judge", "gpt-4o");

        s.Gateway.Remove("openai");

        Assert.False(File.Exists(Path.Combine(s.DataDir, "model-purposes.json")));
        Assert.Empty(s.Gateway.Connection().Purposes);
    }

    private static JsonObject Call(EngineSession session, string method, JsonObject? parameters = null) =>
        session.Handle(new JsonObject { ["v"] = 1, ["kind"] = "request", ["id"] = "t", ["method"] = method, ["params"] = parameters ?? new JsonObject() }.ToJsonString()).Response;

    [Fact]
    public async Task O_protocolo_escolhe_e_volta_ao_padrao_e_recusa_o_que_nao_e_do_provedor()
    {
        using var s = new Setup();
        await s.Gateway.ConnectAsync("openai", OpenAiKey, CancellationToken.None);
        var session = new EngineSession("test", models: s.Gateway);

        var chosen = Call(session, "model.purpose", new JsonObject { ["purpose"] = "judge", ["model"] = "gpt-4o" });
        var judge = chosen["result"]!["purposes"]!.AsArray().Single(item => item!["id"]!.GetValue<string>() == "judge")!;
        Assert.Equal(("gpt-4o", false), (judge["model"]!.GetValue<string>(), judge["isDefault"]!.GetValue<bool>()));

        var denied = Call(session, "model.purpose", new JsonObject { ["purpose"] = "judge", ["model"] = "claude-opus-5-5" });
        Assert.Equal("MODEL_CHOICE_DENIED", denied["error"]!["code"]!.GetValue<string>());

        var cleared = Call(session, "model.purpose", new JsonObject { ["purpose"] = "judge" });
        Assert.True(cleared["result"]!["purposes"]!.AsArray().All(item => item!["isDefault"]!.GetValue<bool>()));
        Assert.Equal(ProtocolErrorCodes.InvalidParams, Call(session, "model.purpose", new JsonObject())["error"]!["code"]!.GetValue<string>());
    }

    [Fact]
    public async Task Fixture_de_contrato_com_as_finalidades()
    {
        using var s = new Setup();
        await s.Gateway.ConnectAsync("anthropic", AnthropicKey, CancellationToken.None);
        s.Gateway.SetPurposeModel("judge", "claude-opus-5-5");
        var text = JsonSerializer.Serialize(s.Gateway.Connection(), Mariliz.Application.Integrations.IntegrationJson.Options);

        var pretty = JsonNode.Parse(text)!.ToJsonString(new JsonSerializerOptions
        {
            WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        }).ReplaceLineEndings("\n");
        Assert.DoesNotContain(AnthropicKey, pretty);

        // O estado conferido tem data do relógio de verdade: a fixture guarda só a forma, com a data normalizada.
        pretty = System.Text.RegularExpressions.Regex.Replace(pretty, "\"checkedAt\": \"[^\"]+\"", "\"checkedAt\": \"2026-11-10T12:00:00+00:00\"");
        var path = ContractFiles.Path_("fixtures", "models", "connection-purposes.json");
        if (Environment.GetEnvironmentVariable("MARILIZ_UPDATE_GOLDEN") == "1" || !File.Exists(path))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, pretty + "\n");
            return;
        }

        Assert.True(pretty == File.ReadAllText(path).ReplaceLineEndings("\n").TrimEnd(),
            "connection-purposes: a resposta mudou. Se foi deliberado, regenere com MARILIZ_UPDATE_GOLDEN=1 e ajuste o Zod da interface.");
    }
}
