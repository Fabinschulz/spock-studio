using System.Text.Json;
using System.Text.Json.Nodes;
using Mariliz.Application.Execution;
using Mariliz.Application.Integrations;
using Mariliz.Application.Models;
using Mariliz.Application.Protocol;
using Mariliz.Application.Tests.Support;
using Mariliz.Infrastructure;

namespace Mariliz.Application.Tests;

public class MultiProviderModelTests
{
    private const string OpenAiKey = "sk-test-openai-fase-i1-7c2d41";
    private const string AnthropicKey = "sk-ant-test-anthropic-fase-i1-91ab_x";
    private const string GoogleKey = "AIzaSyTestGoogleFaseI1-3f9c_x2ab7d41";

    private sealed class Capture : IModelTransport, IModelChatTransport
    {
        public List<(Uri Endpoint, string Key, string? Model)> Seen { get; } = [];
        public HashSet<Uri> Refusing { get; } = [];

        public Task<ModelTransportResult> SendAsync(Uri endpoint, string key, string prompt, int maxTokens, CancellationToken ct)
        {
            Seen.Add((endpoint, key, null));
            if (Refusing.Contains(endpoint)) throw new EngineException("MODEL_AUTH_FAILED", "O provedor recusou a chave.");
            return Task.FromResult(new ModelTransportResult("ok", 2));
        }

        public Task<ChatTransportResult> SendChatAsync(Uri endpoint, string key, string model, ChatRequest request, CancellationToken ct)
        {
            Seen.Add((endpoint, key, model));
            return Task.FromResult(new ChatTransportResult("resposta", "versao", 3, 4));
        }
    }

    private sealed class Setup : IDisposable
    {
        private readonly MemoryVault _vault = new();
        public TempProject Project { get; } = TempProject.Dotnet();
        public Capture Transport { get; } = new();
        public FakeClock Clock { get; } = new();
        public ModelGateway Gateway { get; }

        public Setup() => Gateway = New();

        // Como depois de reiniciar o aplicativo.
        public ModelGateway New() => new(new ModelKeyring(Project.DataDir, _vault), Transport, chat: Transport,
            checks: new FileModelCheckStore(Project.DataDir), clock: Clock, preferences: new FileModelPreferences(Project.DataDir));

        public async Task Connect(params (string Provider, string Key)[] keys)
        {
            foreach (var (provider, key) in keys) await Gateway.ConnectAsync(provider, key, CancellationToken.None);
        }

        public void Dispose() => Project.Dispose();
    }

    private static readonly ChatRequest Chat = new([new ChatMessage("user", "oi")], 32, false);

    [Fact]
    public async Task Conectar_o_segundo_provedor_nao_troca_o_padrao_e_os_dois_ficam_conectados()
    {
        using var s = new Setup();

        await s.Connect(("openai", OpenAiKey), ("anthropic", AnthropicKey));
        var connection = s.Gateway.Connection();

        Assert.Equal(("openai", true), (connection.Provider, connection.Configured));
        Assert.Equal(["connected", "connected", "none"], connection.Providers.Select(provider => provider.Status));
        Assert.Equal([true, false, false], connection.Providers.Select(provider => provider.IsDefault));
    }

    [Fact]
    public async Task Cada_finalidade_fala_com_o_provedor_do_modelo_escolhido_com_a_chave_dele()
    {
        using var s = new Setup();
        await s.Connect(("openai", OpenAiKey), ("anthropic", AnthropicKey));
        s.Gateway.SetPurposeModel(ModelPurposes.Judge, "claude-opus-5-5");
        s.Transport.Seen.Clear();

        var judge = await s.Gateway.ChatAsync(Chat, CancellationToken.None, ModelPurposes.Judge);
        var workflow = await s.Gateway.ChatAsync(Chat, CancellationToken.None, ModelPurposes.Workflow);

        Assert.Equal((ModelProviders.Anthropic.Endpoint, AnthropicKey, "claude-opus-5-5"), s.Transport.Seen[0]);
        Assert.Equal((ModelProviders.OpenAi.Endpoint, OpenAiKey, ModelProviders.OpenAi.DefaultModel), s.Transport.Seen[1]);
        Assert.Equal(("claude-opus-5-5", ModelProviders.OpenAi.DefaultModel), (judge.Model, workflow.Model));
        var purposes = s.Gateway.Connection().Purposes;
        Assert.Equal(("anthropic", false), (purposes.Single(item => item.Id == ModelPurposes.Judge).Provider, purposes.Single(item => item.Id == ModelPurposes.Judge).IsDefault));
        Assert.Equal(("openai", true), (purposes.Single(item => item.Id == ModelPurposes.Workflow).Provider, purposes.Single(item => item.Id == ModelPurposes.Workflow).IsDefault));
    }

    [Fact]
    public async Task A_chave_de_um_provedor_nunca_vai_para_o_endereco_de_outro()
    {
        using var s = new Setup();
        await s.Connect(("openai", OpenAiKey), ("anthropic", AnthropicKey), ("google", GoogleKey));
        s.Gateway.SetPurposeModel(ModelPurposes.Assistant, "gemini-2.5-pro");
        s.Gateway.SetPurposeModel(ModelPurposes.Judge, "claude-sonnet-5-5");
        s.Transport.Seen.Clear();

        foreach (var purpose in new[] { ModelPurposes.Assistant, ModelPurposes.Judge, ModelPurposes.Workflow })
            await s.Gateway.ChatAsync(Chat, CancellationToken.None, purpose);

        var keyOf = new Dictionary<Uri, string>
        {
            [ModelProviders.OpenAi.Endpoint] = OpenAiKey, [ModelProviders.Anthropic.Endpoint] = AnthropicKey, [ModelProviders.Google.Endpoint] = GoogleKey
        };
        Assert.All(s.Transport.Seen, call => Assert.Equal(keyOf[call.Endpoint], call.Key));
        Assert.Equal(3, s.Transport.Seen.Select(call => call.Endpoint).Distinct().Count());
    }

    [Fact]
    public async Task Modelo_de_um_provedor_sem_chave_e_recusado()
    {
        using var s = new Setup();
        await s.Connect(("openai", OpenAiKey));

        var error = Assert.Throws<EngineException>(() => s.Gateway.SetPurposeModel(ModelPurposes.Judge, "gemini-2.5-pro"));

        Assert.Equal("MODEL_CHOICE_DENIED", error.Code);
    }

    [Fact]
    public async Task Testar_um_provedor_registra_o_estado_so_dele()
    {
        using var s = new Setup();
        await s.Connect(("openai", OpenAiKey), ("anthropic", AnthropicKey));
        s.Clock.UtcNow = s.Clock.UtcNow.AddHours(1);
        s.Transport.Refusing.Add(ModelProviders.Anthropic.Endpoint);

        var connection = await s.Gateway.TestAsync("anthropic", CancellationToken.None);

        Assert.Equal(("error", "MODEL_AUTH_FAILED"), (connection.Of("anthropic").Status, connection.Of("anthropic").LastError!.Code));
        Assert.Equal(("connected", null), (connection.Of("openai").Status, connection.Of("openai").LastError?.Code));
        Assert.NotEqual(connection.Of("openai").CheckedAt, connection.Of("anthropic").CheckedAt);
    }

    [Fact]
    public async Task Testar_sem_dizer_o_provedor_testa_o_padrao_e_provedor_sem_chave_nao_e_testado()
    {
        using var s = new Setup();
        await s.Connect(("anthropic", AnthropicKey));
        s.Transport.Seen.Clear();

        await s.Gateway.TestAsync(null, CancellationToken.None);
        var error = await Assert.ThrowsAsync<EngineException>(() => s.Gateway.TestAsync("openai", CancellationToken.None));

        Assert.Equal(ModelProviders.Anthropic.Endpoint, s.Transport.Seen.Single().Endpoint);
        Assert.Equal("MODEL_NOT_CONFIGURED", error.Code);
    }

    [Fact]
    public async Task Mudar_o_padrao_so_vale_para_quem_tem_chave_e_as_finalidades_sem_escolha_o_seguem()
    {
        using var s = new Setup();
        await s.Connect(("openai", OpenAiKey), ("anthropic", AnthropicKey));

        var switched = s.Gateway.SetDefaultProvider("anthropic");

        Assert.Equal("anthropic", switched.Provider);
        Assert.All(switched.Purposes, item => Assert.Equal(("anthropic", ModelProviders.Anthropic.DefaultModel, true), (item.Provider, item.Model, item.IsDefault)));
        Assert.Equal("anthropic", s.New().Connection().Provider);
        var withoutKey = Assert.Throws<EngineException>(() => s.Gateway.SetDefaultProvider("google"));
        var unknown = Assert.Throws<EngineException>(() => s.Gateway.SetDefaultProvider("outro"));
        Assert.Equal(("MODEL_NOT_CONFIGURED", "MODEL_PROVIDER_DENIED"), (withoutKey.Code, unknown.Code));
    }

    [Fact]
    public async Task Remover_um_provedor_tira_a_chave_o_estado_e_as_escolhas_dele_e_o_padrao_passa_para_o_outro()
    {
        using var s = new Setup();
        await s.Connect(("openai", OpenAiKey), ("anthropic", AnthropicKey));
        s.Gateway.SetPurposeModel(ModelPurposes.Judge, "gpt-4o");
        s.Gateway.SetPurposeModel(ModelPurposes.Workflow, "claude-opus-5-5");

        var connection = s.Gateway.Remove("openai");

        Assert.Equal(("anthropic", true), (connection.Provider, connection.Configured));
        Assert.Equal("none", connection.Of("openai").Status);
        var purposes = connection.Purposes;
        Assert.Equal((ModelProviders.Anthropic.DefaultModel, true), (purposes.Single(item => item.Id == ModelPurposes.Judge).Model, purposes.Single(item => item.Id == ModelPurposes.Judge).IsDefault));
        Assert.Equal("claude-opus-5-5", purposes.Single(item => item.Id == ModelPurposes.Workflow).Model);
        Assert.DoesNotContain("gpt-4o", File.ReadAllText(Path.Combine(s.Project.DataDir, "model-purposes.json")));
    }

    [Fact]
    public async Task Remover_o_ultimo_provedor_deixa_o_motor_sem_modelo_e_o_chat_diz_isso()
    {
        using var s = new Setup();
        await s.Connect(("openai", OpenAiKey));

        var connection = s.Gateway.Remove("openai");

        Assert.Equal((false, "", 0), (connection.Configured, connection.Provider, connection.Purposes.Count));
        var error = await Assert.ThrowsAsync<EngineException>(() => s.Gateway.ChatAsync(Chat, CancellationToken.None));
        Assert.Equal("MODEL_NOT_CONFIGURED", error.Code);
        Assert.Throws<EngineException>(() => s.Gateway.Remove("outro"));
    }

    [Fact]
    public async Task O_estado_e_o_padrao_sobrevivem_a_reiniciar()
    {
        using var s = new Setup();
        await s.Connect(("openai", OpenAiKey), ("anthropic", AnthropicKey));
        s.Gateway.SetDefaultProvider("anthropic");
        s.Gateway.SetPurposeModel(ModelPurposes.Judge, "gpt-4o");

        var restarted = s.New().Connection();

        Assert.Equal(["connected", "connected", "none"], restarted.Providers.Select(provider => provider.Status));
        Assert.Equal("anthropic", restarted.Provider);
        Assert.Equal("gpt-4o", restarted.Purposes.Single(item => item.Id == ModelPurposes.Judge).Model);
    }

    private static JsonObject Call(EngineSession session, string method, JsonObject? parameters = null) =>
        session.Handle(new JsonObject { ["v"] = 1, ["kind"] = "request", ["id"] = "t", ["method"] = method, ["params"] = parameters ?? new JsonObject() }.ToJsonString()).Response;

    [Fact]
    public async Task O_protocolo_remove_muda_o_padrao_e_testa_um_provedor_sem_trazer_chave()
    {
        using var s = new Setup();
        await s.Connect(("openai", OpenAiKey), ("anthropic", AnthropicKey));
        var session = new EngineSession("test", models: s.Gateway);

        var defaulted = Call(session, "model.default", new JsonObject { ["provider"] = "anthropic" });
        var tested = Call(session, "model.test", new JsonObject { ["provider"] = "openai" });
        var removed = Call(session, "model.remove", new JsonObject { ["provider"] = "openai" });
        var invalid = Call(session, "model.default", new JsonObject());

        Assert.Equal("anthropic", defaulted["result"]!["provider"]!.GetValue<string>());
        Assert.Equal("connected", tested["result"]!["providers"]![0]!["status"]!.GetValue<string>());
        Assert.Equal("none", removed["result"]!["providers"]![0]!["status"]!.GetValue<string>());
        Assert.Equal(ProtocolErrorCodes.InvalidParams, invalid["error"]!["code"]!.GetValue<string>());
        Assert.DoesNotContain(OpenAiKey, defaulted.ToJsonString() + tested.ToJsonString() + removed.ToJsonString());
        Assert.DoesNotContain(AnthropicKey, defaulted.ToJsonString());
    }

    [Fact]
    public async Task Fixture_de_contrato_com_varios_provedores()
    {
        using var s = new Setup();
        await s.Connect(("openai", OpenAiKey), ("anthropic", AnthropicKey));
        s.Gateway.SetPurposeModel(ModelPurposes.Judge, "claude-opus-5-5");
        s.Clock.UtcNow = s.Clock.UtcNow.AddHours(1);
        s.Transport.Refusing.Add(ModelProviders.Anthropic.Endpoint);
        var connection = await s.Gateway.TestAsync("anthropic", CancellationToken.None);

        var text = JsonSerializer.Serialize(connection, IntegrationJson.Options);
        var pretty = JsonNode.Parse(text)!.ToJsonString(new JsonSerializerOptions
        {
            WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        }).ReplaceLineEndings("\n");
        Assert.DoesNotContain(OpenAiKey, pretty);
        Assert.DoesNotContain(AnthropicKey, pretty);

        var path = ContractFiles.Path_("fixtures", "models", "connection-multi.json");
        if (Environment.GetEnvironmentVariable("MARILIZ_UPDATE_GOLDEN") == "1" || !File.Exists(path))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, pretty + "\n");
            return;
        }

        Assert.True(pretty == File.ReadAllText(path).ReplaceLineEndings("\n").TrimEnd(),
            "connection-multi: a resposta mudou. Se foi deliberado, regenere com MARILIZ_UPDATE_GOLDEN=1 e ajuste o Zod da interface.");
    }
}
