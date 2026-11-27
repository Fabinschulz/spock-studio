using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Mariliz.Application.Execution;
using Mariliz.Application.Integrations;
using Mariliz.Application.Models;
using Mariliz.Application.Protocol;
using Mariliz.Application.Tests.Support;
using Mariliz.Infrastructure;
using Mariliz.Infrastructure.ModelFormats;

namespace Mariliz.Application.Tests;

public class ModelProvidersTests
{
    private const string OpenAiKey = "sk-test-openai-fase-f-7c2d41";
    private const string AnthropicKey = "sk-ant-test-anthropic-fase-f-91ab_x";
    private const string GoogleKey = "AIzaSyTestGoogleFaseI1-3f9c_x2ab7d41";

    [Fact]
    public void A_lista_tem_os_tres_provedores_com_endereco_https_proprio_e_modelo()
    {
        Assert.Equal(["openai", "anthropic", "google"], ModelProviders.All.Select(provider => provider.Id));
        Assert.All(ModelProviders.All, provider =>
        {
            Assert.Equal("https", provider.Endpoint.Scheme);
            Assert.NotEmpty(provider.DefaultModel);
            Assert.NotEmpty(provider.Name);
        });
        Assert.Equal(3, ModelProviders.All.Select(provider => provider.Endpoint.Host).Distinct().Count());
        Assert.Equal("openai", ModelProviders.Default.Id);
    }

    [Theory]
    [InlineData("https://api.openai.com/v1/chat/completions", "openai")]
    [InlineData("https://API.OPENAI.COM/v1/chat/completions", "openai")]
    [InlineData("https://api.anthropic.com/v1/messages", "anthropic")]
    [InlineData("http://api.anthropic.com/v1/messages", null)]
    [InlineData("https://api.anthropic.com/v1/outro", null)]
    [InlineData("https://api.anthropic.com.evil.example/v1/messages", null)]
    [InlineData("https://evil.example/v1/messages", null)]
    [InlineData("https://api.openai.com/v1/messages", null)]
    [InlineData("https://generativelanguage.googleapis.com/v1beta/models", "google")]
    [InlineData("https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent", null)]
    [InlineData("https://generativelanguage.googleapis.com.evil.example/v1beta/models", null)]
    public void O_endereco_so_vale_se_esquema_servidor_e_caminho_batem_com_um_provedor(string endpoint, string? expected)
    {
        Assert.Equal(expected, ModelProviders.ByEndpoint(new Uri(endpoint))?.Id);
    }

    [Fact]
    public void Provedor_desconhecido_nao_existe()
    {
        Assert.Null(ModelProviders.Find("outro"));
        Assert.Null(ModelProviders.Find(null));
    }

    [Fact]
    public void Cada_modelo_pertence_a_um_provedor_so_e_cada_provedor_tem_o_seu_lugar_no_cofre()
    {
        var models = ModelProviders.All.SelectMany(provider => provider.Models.Select(option => option.Id)).ToList();

        Assert.Equal(models.Count, models.Distinct().Count());
        Assert.All(ModelProviders.All, provider => Assert.Equal(provider.Id, ModelProviders.ByModel(provider.DefaultModel)?.Id));
        Assert.Null(ModelProviders.ByModel("modelo-que-nao-existe"));
        Assert.Equal(ModelProviders.All.Count, ModelProviders.All.Select(provider => ReservedSecretIds.ModelKeyOf(provider.Id)).Distinct().Count());
        Assert.All(ModelProviders.All, provider => Assert.True(ReservedSecretIds.IsReserved(ReservedSecretIds.ModelKeyOf(provider.Id))));
    }

    private static string AnthropicReply(string text, int input = 11, int output = 7) =>
        new JsonObject
        {
            ["model"] = "claude-versao-exata",
            ["content"] = new JsonArray(new JsonObject { ["type"] = "text", ["text"] = text }),
            ["usage"] = new JsonObject { ["input_tokens"] = input, ["output_tokens"] = output }
        }.ToJsonString();

    private static ChatRequest Chat(bool json = false, params ChatMessage[] messages) =>
        new(messages.Length == 0 ? [new ChatMessage("system", "Seja breve."), new ChatMessage("user", "oi")] : messages, 64, json);

    [Fact]
    public async Task A_anthropic_recebe_a_chave_no_cabecalho_dela_e_a_instrucao_de_sistema_a_parte()
    {
        var handler = new RecordingHandler(AnthropicReply("olá"));
        using var transport = new HttpsModelTransport(handler);

        var result = await transport.SendChatAsync(ModelProviders.Anthropic.Endpoint, AnthropicKey, ModelProviders.Anthropic.DefaultModel,
            Chat(messages: [new ChatMessage("system", "Seja breve."), new ChatMessage("user", "oi"), new ChatMessage("assistant", "olá"), new ChatMessage("user", "e agora?")]),
            CancellationToken.None);

        Assert.Equal(AnthropicKey, handler.Request!.Headers.GetValues("x-api-key").Single());
        Assert.Equal(AnthropicFormat.Version, handler.Request.Headers.GetValues("anthropic-version").Single());
        Assert.Null(handler.Request.Headers.Authorization);
        Assert.DoesNotContain(AnthropicKey, handler.Body.ToJsonString());
        Assert.Equal(ModelProviders.Anthropic.DefaultModel, handler.Body["model"]!.GetValue<string>());
        Assert.Equal(64, handler.Body["max_tokens"]!.GetValue<int>());
        Assert.Equal("Seja breve.", handler.Body["system"]!.GetValue<string>());
        Assert.Equal(["user", "assistant", "user"], handler.Body["messages"]!.AsArray().Select(m => m!["role"]!.GetValue<string>()));
        Assert.Equal(("olá", "claude-versao-exata", 11, 7), (result.Text, result.ModelVersion, result.InputTokens, result.OutputTokens));
    }

    [Fact]
    public async Task Na_anthropic_o_que_entrou_no_cache_conta_como_entrada()
    {
        var body = new JsonObject
        {
            ["model"] = "claude-versao-exata",
            ["content"] = new JsonArray(new JsonObject { ["type"] = "text", ["text"] = "ok" }),
            ["usage"] = new JsonObject { ["input_tokens"] = 5, ["cache_creation_input_tokens"] = 42, ["cache_read_input_tokens"] = 3, ["output_tokens"] = 64 }
        }.ToJsonString();
        using var transport = new HttpsModelTransport(new RecordingHandler(body));

        var result = await transport.SendChatAsync(ModelProviders.Anthropic.Endpoint, AnthropicKey, ModelProviders.Anthropic.DefaultModel, Chat(), CancellationToken.None);

        Assert.Equal((50, 64), (result.InputTokens, result.OutputTokens));
    }

    [Fact]
    public async Task A_openai_continua_com_bearer_e_sem_cabecalhos_da_anthropic()
    {
        var handler = new RecordingHandler("""{"choices":[{"message":{"content":"ok"}}],"model":"gpt-exato","usage":{"prompt_tokens":5,"completion_tokens":3,"total_tokens":8}}""");
        using var transport = new HttpsModelTransport(handler);

        var result = await transport.SendChatAsync(ModelProviders.OpenAi.Endpoint, OpenAiKey, ModelProviders.OpenAi.DefaultModel, Chat(json: true), CancellationToken.None);

        Assert.Equal("Bearer " + OpenAiKey, handler.Request!.Headers.Authorization!.ToString());
        Assert.False(handler.Request.Headers.Contains("x-api-key"));
        Assert.False(handler.Request.Headers.Contains("anthropic-version"));
        Assert.Equal("json_object", handler.Body["response_format"]!["type"]!.GetValue<string>());
        Assert.Equal("system", handler.Body["messages"]![0]!["role"]!.GetValue<string>());
        Assert.Equal(("ok", "gpt-exato", 5, 3), (result.Text, result.ModelVersion, result.InputTokens, result.OutputTokens));
    }

    [Fact]
    public async Task Na_anthropic_o_pedido_de_json_vai_na_instrucao_e_a_cerca_de_codigo_e_tirada_na_volta()
    {
        var handler = new RecordingHandler(AnthropicReply("```json\n{\"a\": 1}\n```"));
        using var transport = new HttpsModelTransport(handler);

        var json = await transport.SendChatAsync(ModelProviders.Anthropic.Endpoint, AnthropicKey, "m", Chat(json: true), CancellationToken.None);

        Assert.Equal("{\"a\": 1}", json.Text);
        Assert.Contains("objeto JSON", handler.Body["system"]!.GetValue<string>());
        Assert.Null(handler.Body["response_format"]);

        var plain = await transport.SendChatAsync(ModelProviders.Anthropic.Endpoint, AnthropicKey, "m", Chat(json: false), CancellationToken.None);
        Assert.Contains("```", plain.Text);
        Assert.DoesNotContain("objeto JSON", handler.Body["system"]!.GetValue<string>());
    }

    [Fact]
    public async Task Na_anthropic_uma_frase_antes_e_depois_do_JSON_nao_derruba_a_resposta()
    {
        var handler = new RecordingHandler(AnthropicReply("Segue o desenho:\n```json\n{\"a\": 1}\n```\nSe quiser, ajusto."));
        using var transport = new HttpsModelTransport(handler);

        var json = await transport.SendChatAsync(ModelProviders.Anthropic.Endpoint, AnthropicKey, "m", Chat(json: true), CancellationToken.None);

        Assert.Equal("{\"a\": 1}", json.Text);
    }

    [Fact]
    public async Task Conversa_so_de_instrucoes_ainda_vira_uma_mensagem_do_usuario_na_anthropic()
    {
        var handler = new RecordingHandler(AnthropicReply("ok"));
        using var transport = new HttpsModelTransport(handler);

        await transport.SendChatAsync(ModelProviders.Anthropic.Endpoint, AnthropicKey, "m", Chat(messages: [new ChatMessage("system", "Faça isto.")]), CancellationToken.None);

        var messages = handler.Body["messages"]!.AsArray();
        Assert.Equal(("user", "Faça isto."), (messages[0]!["role"]!.GetValue<string>(), messages[0]!["content"]!.GetValue<string>()));
        Assert.Null(handler.Body["system"]);
    }

    [Fact]
    public async Task A_pergunta_curta_usa_o_modelo_do_provedor_do_endereco_e_conta_os_tokens_dele()
    {
        var anthropic = new RecordingHandler(AnthropicReply("ok", 4, 2));
        using var transport = new HttpsModelTransport(anthropic);

        var result = await transport.SendAsync(ModelProviders.Anthropic.Endpoint, AnthropicKey, "oi", 8, CancellationToken.None);

        Assert.Equal(ModelProviders.Anthropic.DefaultModel, anthropic.Body["model"]!.GetValue<string>());
        Assert.Equal(("ok", 6), (result.Text, result.Tokens));
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, "MODEL_AUTH_FAILED")]
    [InlineData(HttpStatusCode.Forbidden, "MODEL_AUTH_FAILED")]
    [InlineData(HttpStatusCode.TooManyRequests, "MODEL_RATE_LIMITED")]
    [InlineData(HttpStatusCode.InternalServerError, "MODEL_OVERLOADED")]
    public async Task Os_codigos_de_recusa_valem_tambem_para_a_anthropic_sem_repetir_o_corpo(HttpStatusCode status, string code)
    {
        using var transport = new HttpsModelTransport(new RecordingHandler("{\"error\":{\"message\":\"chave " + AnthropicKey + " invalida\"}}", status));

        var error = await Assert.ThrowsAsync<EngineException>(() => transport.SendAsync(ModelProviders.Anthropic.Endpoint, AnthropicKey, "oi", 8, CancellationToken.None));

        Assert.Equal(code, error.Code);
        Assert.DoesNotContain(AnthropicKey, error.Message);
    }

    [Fact]
    public async Task Endereco_fora_da_lista_e_recusado_antes_de_a_chave_sair()
    {
        var handler = new RecordingHandler(AnthropicReply("ok"));
        using var transport = new HttpsModelTransport(handler);

        var error = await Assert.ThrowsAsync<EngineException>(() =>
            transport.SendAsync(new Uri("https://api.anthropic.com.evil.example/v1/messages"), AnthropicKey, "oi", 8, CancellationToken.None));

        Assert.Equal("MODEL_ENDPOINT_DENIED", error.Code);
        Assert.Null(handler.Request);
    }

    private sealed class Capture : IModelTransport, IModelChatTransport
    {
        public List<(Uri Endpoint, string Key, string? Model)> Seen { get; } = [];
        public Func<Uri, Exception?>? Fail { get; set; }

        public Task<ModelTransportResult> SendAsync(Uri endpoint, string key, string prompt, int maxTokens, CancellationToken ct)
        {
            Seen.Add((endpoint, key, null));
            if (Fail?.Invoke(endpoint) is { } error) throw error;
            return Task.FromResult(new ModelTransportResult("ok", 2));
        }

        public Task<ChatTransportResult> SendChatAsync(Uri endpoint, string key, string model, ChatRequest request, CancellationToken ct)
        {
            Seen.Add((endpoint, key, model));
            return Task.FromResult(new ChatTransportResult("resposta", "versao", 3, 4));
        }
    }

    private static ModelGateway Gateway(TempProject project, Capture capture) =>
        new(new FileModelSettings(project.DataDir), capture, chat: capture, checks: new FileModelCheckStore(project.DataDir));

    [Fact]
    public async Task Conectar_a_anthropic_confere_no_endereco_dela_e_a_conexao_passa_a_ser_dela()
    {
        using var project = TempProject.Dotnet();
        var capture = new Capture();
        var gateway = Gateway(project, capture);

        var connection = await gateway.ConnectAsync("anthropic", AnthropicKey, CancellationToken.None);

        Assert.Equal(ModelProviders.Anthropic.Endpoint, capture.Seen.Single().Endpoint);
        var anthropic = connection.Of("anthropic");
        Assert.Equal(("anthropic", "Anthropic", ModelProviders.Anthropic.DefaultModel, "connected"), (connection.Provider, anthropic.Name, anthropic.Model, anthropic.Status));
        Assert.Equal(["openai", "anthropic", "google"], connection.Providers.Select(provider => provider.Id));
    }

    [Fact]
    public async Task As_conversas_dos_agentes_vao_ao_provedor_e_ao_modelo_da_chave_guardada()
    {
        using var project = TempProject.Dotnet();
        var capture = new Capture();
        var gateway = Gateway(project, capture);
        var request = new ChatRequest([new ChatMessage("user", "oi")], 32, false);

        await gateway.ConnectAsync("openai", OpenAiKey, CancellationToken.None);
        var first = await gateway.ChatAsync(request, CancellationToken.None);
        await gateway.ConnectAsync("anthropic", AnthropicKey, CancellationToken.None);
        var second = await gateway.ChatAsync(request, CancellationToken.None);

        Assert.Equal((ModelProviders.OpenAi.Endpoint, OpenAiKey, ModelProviders.OpenAi.DefaultModel), capture.Seen[1]);
        Assert.Equal((ModelProviders.Anthropic.Endpoint, AnthropicKey, ModelProviders.Anthropic.DefaultModel), capture.Seen[3]);
        Assert.Equal((ModelProviders.OpenAi.DefaultModel, ModelProviders.Anthropic.DefaultModel), (first.Model, second.Model));
    }

    [Fact]
    public async Task Chave_de_antes_da_escolha_de_provedor_e_da_openai()
    {
        using var project = TempProject.Dotnet();
        File.WriteAllText(Path.Combine(project.DataDir, "model.json"), new JsonObject { ["provider"] = "openai", ["key"] = OpenAiKey }.ToJsonString());
        var capture = new Capture();

        var connection = await Gateway(project, capture).TestAsync(null, CancellationToken.None);

        Assert.Equal("openai", connection.Provider);
        Assert.Equal(ModelProviders.OpenAi.Endpoint, capture.Seen.Single().Endpoint);
    }

    [Fact]
    public async Task Trocar_para_um_provedor_que_recusa_mantem_o_provedor_e_a_chave_de_antes()
    {
        using var project = TempProject.Dotnet();
        var capture = new Capture();
        var gateway = Gateway(project, capture);
        await gateway.ConnectAsync("openai", OpenAiKey, CancellationToken.None);
        capture.Fail = endpoint => endpoint == ModelProviders.Anthropic.Endpoint ? new EngineException("MODEL_AUTH_FAILED", "O provedor recusou a chave.") : null;

        await Assert.ThrowsAsync<EngineException>(() => gateway.ConnectAsync("anthropic", AnthropicKey, CancellationToken.None));

        var connection = gateway.Connection();
        Assert.Equal(("openai", true), (connection.Provider, connection.Configured));
        Assert.True(new FileModelSettings(project.DataDir).TryGetKey(out var key));
        Assert.Equal(OpenAiKey, key);
    }

    [Fact]
    public async Task Provedor_desconhecido_nao_chega_a_nenhum_provedor_e_o_protocolo_so_aceita_os_da_lista()
    {
        using var project = TempProject.Dotnet();
        var capture = new Capture();
        var gateway = Gateway(project, capture);

        var error = await Assert.ThrowsAsync<EngineException>(() => gateway.ConnectAsync("outro", AnthropicKey, CancellationToken.None));

        Assert.Equal("MODEL_PROVIDER_DENIED", error.Code);
        Assert.Empty(capture.Seen);
        var schema = ContractFiles.Schema("protocol", "methods", "model.save.params.schema.json");
        Assert.True(ContractFiles.IsValid(schema, new JsonObject { ["provider"] = "anthropic", ["key"] = AnthropicKey }));
        Assert.True(ContractFiles.IsValid(schema, new JsonObject { ["provider"] = "openai", ["key"] = OpenAiKey }));
        Assert.True(ContractFiles.IsValid(schema, new JsonObject { ["provider"] = "google", ["key"] = GoogleKey }));
        Assert.False(ContractFiles.IsValid(schema, new JsonObject { ["provider"] = "outro", ["key"] = AnthropicKey }));
    }

    [Fact]
    public void A_chave_da_anthropic_passa_na_regra_de_formato_e_a_regra_nao_deixa_passar_provedor_fora_da_lista()
    {
        var settings = new FileModelSettings(TempProject.Dotnet().DataDir);

        settings.Validate("anthropic", AnthropicKey);
        var denied = Assert.Throws<EngineException>(() => settings.Validate("outro", AnthropicKey));
        var rejected = Assert.Throws<EngineException>(() => settings.Validate("anthropic", "curta"));

        Assert.Equal(("MODEL_PROVIDER_DENIED", "MODEL_KEY_REJECTED"), (denied.Code, rejected.Code));
    }

    private sealed class FailingVault : Mariliz.Application.Integrations.ISecretVault
    {
        public bool Available => true;
        public void Store(string connectionId, string secret) => throw new IOException("cofre indisponível");
        public bool TryRead(string connectionId, out string secret)
        {
            secret = "";
            return false;
        }
        public void Remove(string connectionId) { }
    }

    [Fact]
    public void No_cofre_o_provedor_fica_ao_lado_da_chave_e_sobrevive_a_um_motor_novo()
    {
        using var project = TempProject.Dotnet();
        var vault = new MemoryVault();
        new LocalModelSettings(project.DataDir, vault).Save("anthropic", AnthropicKey);

        var reopened = new LocalModelSettings(project.DataDir, vault);

        Assert.Equal(new ModelStatus(true, "anthropic"), reopened.Status());
        Assert.DoesNotContain(AnthropicKey, File.ReadAllText(Path.Combine(project.DataDir, "model-provider.json")));
        Assert.Equal(ModelKeyProtection.Vault, reopened.Protection());
    }

    [Fact]
    public void Remover_a_chave_apaga_o_provedor_e_arquivo_de_provedor_ilegivel_vale_openai()
    {
        using var project = TempProject.Dotnet();
        var vault = new MemoryVault();
        var settings = new LocalModelSettings(project.DataDir, vault);
        settings.Save("anthropic", AnthropicKey);

        File.WriteAllText(Path.Combine(project.DataDir, "model-provider.json"), "{ nao e json");
        Assert.Equal("openai", settings.Status().Provider);

        settings.Clear();
        Assert.False(File.Exists(Path.Combine(project.DataDir, "model-provider.json")));
        Assert.Equal(new ModelStatus(false, ""), settings.Status());
    }

    [Fact]
    public void Se_o_cofre_falha_o_provedor_volta_ao_que_era_e_a_chave_antiga_segue_valendo()
    {
        using var project = TempProject.Dotnet();
        var vault = new MemoryVault();
        new LocalModelSettings(project.DataDir, vault).Save("openai", OpenAiKey);
        var failing = new LocalModelSettings(project.DataDir, new FailingVault());
        // Um cofre que falha ao guardar não pode deixar o arquivo de provedor apontando para outro provedor.
        File.WriteAllText(Path.Combine(project.DataDir, "model-provider.json"), new JsonObject { ["provider"] = "openai" }.ToJsonString());

        var error = Assert.Throws<EngineException>(() => failing.Save("anthropic", AnthropicKey));

        Assert.Equal("MODEL_KEY_STORE_FAILED", error.Code);
        Assert.Contains("openai", File.ReadAllText(Path.Combine(project.DataDir, "model-provider.json")));
        Assert.Equal(new ModelStatus(true, "openai"), new LocalModelSettings(project.DataDir, vault).Status());
    }

    [Fact]
    public void A_chave_do_arquivo_antigo_migra_para_o_cofre_com_o_provedor_dela()
    {
        using var project = TempProject.Dotnet();
        new FileModelSettings(project.DataDir).Save("anthropic", AnthropicKey);
        var vault = new MemoryVault();

        var status = new LocalModelSettings(project.DataDir, vault).Status();

        Assert.Equal(new ModelStatus(true, "anthropic"), status);
        Assert.Equal(AnthropicKey, vault.Secrets[Mariliz.Application.Integrations.ReservedSecretIds.ModelKey]);
        Assert.False(File.Exists(Path.Combine(project.DataDir, "model.json")));
    }

    [Fact]
    public void Sem_cofre_o_provedor_fica_no_proprio_arquivo_da_chave()
    {
        using var project = TempProject.Dotnet();
        var settings = new LocalModelSettings(project.DataDir, new MemoryVault { Available = false });

        settings.Save("anthropic", AnthropicKey);

        Assert.Equal(new ModelStatus(true, "anthropic"), settings.Status());
        Assert.Equal(ModelKeyProtection.File, settings.Protection());
    }

    [Fact]
    public async Task O_metodo_de_conexao_traz_a_lista_de_provedores_e_nenhum_endereco_nem_chave()
    {
        using var project = TempProject.Dotnet();
        var capture = new Capture();
        var gateway = Gateway(project, capture);
        await gateway.ConnectAsync("anthropic", AnthropicKey, CancellationToken.None);
        var session = new EngineSession("test", models: gateway);

        var response = session.Handle(new JsonObject
            { ["v"] = 1, ["kind"] = "request", ["id"] = "t", ["method"] = "model.connection", ["params"] = new JsonObject() }.ToJsonString()).Response;

        var result = response["result"]!;
        Assert.Equal("anthropic", result["provider"]!.GetValue<string>());
        Assert.Equal(["openai", "anthropic", "google"], result["providers"]!.AsArray().Select(item => item!["id"]!.GetValue<string>()));
        Assert.DoesNotContain("https://", response.ToJsonString());
        Assert.DoesNotContain(AnthropicKey, response.ToJsonString());
    }
}
