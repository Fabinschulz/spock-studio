using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Mariliz.Application.Execution;
using Mariliz.Application.Integrations;
using Mariliz.Application.Models;
using Mariliz.Application.Protocol;
using Mariliz.Application.Tests.Support;
using Mariliz.Infrastructure;

namespace Mariliz.Application.Tests;

/// <summary>
/// O provedor de modelo como uma conexão: a chave só é guardada depois de o provedor aceitá-la, pode ser conferida a qualquer
/// momento, e o estado diz se funciona e onde ela está guardada. Nada disso mostra a chave.
/// </summary>
public class ModelConnectionTests
{
    private const string Key = "sk-test-chave-fase-f-7c2d41";
    private const string OtherKey = "sk-test-outra-chave-fase-f-91ab";

    private sealed class ScriptedTransport : IModelTransport
    {
        public int Calls { get; private set; }
        public string SeenKey { get; private set; } = "";
        public string SeenPrompt { get; private set; } = "";
        public int SeenMaxTokens { get; private set; }

        /// <summary>O que o provedor faz com cada chamada. Padrão: aceita.</summary>
        public Func<string, CancellationToken, Task>? Behavior { get; set; }

        public async Task<ModelTransportResult> SendAsync(Uri endpoint, string key, string prompt, int maxTokens, CancellationToken ct)
        {
            Calls++;
            SeenKey = key;
            SeenPrompt = prompt;
            SeenMaxTokens = maxTokens;
            if (Behavior is not null) await Behavior(key, ct);
            return new ModelTransportResult("ok", 2);
        }

        public void Refuse(string code = "MODEL_AUTH_FAILED", string message = "O provedor recusou a chave.") =>
            Behavior = (_, _) => throw new EngineException(code, message);

        public void Accept() => Behavior = null;
    }

    private sealed class Setup : IDisposable
    {
        public TempProject Project { get; } = TempProject.Dotnet();
        public ScriptedTransport Transport { get; } = new();
        public FakeClock Clock { get; } = new();
        public ModelGateway Gateway { get; }
        public string DataDir => Project.DataDir;

        public Setup(TimeSpan? timeout = null)
        {
            Gateway = New(timeout);
        }

        /// <summary>Um motor novo sobre o mesmo diretório de dados, como depois de reiniciar o aplicativo.</summary>
        public ModelGateway New(TimeSpan? timeout = null) =>
            new(new FileModelSettings(DataDir), Transport, timeout, checks: new FileModelCheckStore(DataDir), clock: Clock);

        public void Dispose() => Project.Dispose();
    }

    private static string Serialized(ModelConnection connection) => JsonSerializer.Serialize(connection, IntegrationJson.Options);

    // ------------------------------------------------------------------ Sem chave

    [Fact]
    public async Task Sem_chave_a_conexao_diz_que_nao_ha_nada_e_testar_nao_inventa_estado()
    {
        using var s = new Setup();

        var connection = s.Gateway.Connection();

        Assert.Equal(("none", false, "", null), (connection.Status, connection.Configured, connection.Provider, connection.Protection));
        Assert.Equal(("", ""), (connection.Model, connection.ProviderName));
        Assert.Equal(["openai", "anthropic"], connection.Available.Select(provider => provider.Id));
        var error = await Assert.ThrowsAsync<EngineException>(() => s.Gateway.TestAsync(CancellationToken.None));
        Assert.Equal("MODEL_NOT_CONFIGURED", error.Code);
        Assert.Equal(0, s.Transport.Calls);
    }

    // ------------------------------------------------------------------ Conectar

    [Fact]
    public async Task Conectar_confere_com_o_provedor_antes_de_guardar_e_depois_diz_que_funciona()
    {
        using var s = new Setup();

        var connection = await s.Gateway.ConnectAsync("openai", Key, CancellationToken.None);

        Assert.Equal(1, s.Transport.Calls);
        Assert.Equal((Key, ModelGateway.ProbePrompt, ModelGateway.ProbeMaxTokens), (s.Transport.SeenKey, s.Transport.SeenPrompt, s.Transport.SeenMaxTokens));
        Assert.Equal(("connected", true, "openai", ModelKeyProtection.File), (connection.Status, connection.Configured, connection.Provider, connection.Protection));
        Assert.Equal(s.Clock.UtcNow, connection.CheckedAt);
        Assert.Null(connection.LastError);
        Assert.True(new FileModelSettings(s.DataDir).TryGetKey(out var stored));
        Assert.Equal(Key, stored);
    }

    [Fact]
    public async Task Se_o_provedor_recusa_nada_e_gravado()
    {
        using var s = new Setup();
        s.Transport.Refuse();

        var error = await Assert.ThrowsAsync<EngineException>(() => s.Gateway.ConnectAsync("openai", Key, CancellationToken.None));

        Assert.Equal("MODEL_AUTH_FAILED", error.Code);
        Assert.DoesNotContain(Key, error.Message);
        Assert.False(File.Exists(Path.Combine(s.DataDir, "model.json")));
        Assert.False(File.Exists(Path.Combine(s.DataDir, "model-check.json")));
        Assert.Equal("none", s.Gateway.Connection().Status);
    }

    [Fact]
    public async Task Trocar_a_chave_por_uma_recusada_mantem_a_antiga_valendo_e_o_estado_dela()
    {
        using var s = new Setup();
        await s.Gateway.ConnectAsync("openai", Key, CancellationToken.None);
        s.Clock.UtcNow = s.Clock.UtcNow.AddHours(1);
        s.Transport.Refuse();

        await Assert.ThrowsAsync<EngineException>(() => s.Gateway.ConnectAsync("openai", OtherKey, CancellationToken.None));

        Assert.True(new FileModelSettings(s.DataDir).TryGetKey(out var stored));
        Assert.Equal(Key, stored);
        var connection = s.Gateway.Connection();
        Assert.Equal("connected", connection.Status);
        Assert.Equal(new DateTimeOffset(2026, 11, 10, 12, 0, 0, TimeSpan.Zero), connection.CheckedAt);
    }

    [Fact]
    public async Task Trocar_a_chave_por_uma_aceita_passa_a_valer_e_renova_a_conferencia()
    {
        using var s = new Setup();
        await s.Gateway.ConnectAsync("openai", Key, CancellationToken.None);
        s.Clock.UtcNow = s.Clock.UtcNow.AddHours(1);

        var connection = await s.Gateway.ConnectAsync("openai", OtherKey, CancellationToken.None);

        Assert.True(new FileModelSettings(s.DataDir).TryGetKey(out var stored));
        Assert.Equal(OtherKey, stored);
        Assert.Equal(s.Clock.UtcNow, connection.CheckedAt);
    }

    [Fact]
    public async Task Chave_fora_do_formato_ou_provedor_desconhecido_nem_chega_ao_provedor()
    {
        using var s = new Setup();

        var format = await Assert.ThrowsAsync<EngineException>(() => s.Gateway.ConnectAsync("openai", "curta", CancellationToken.None));
        var provider = await Assert.ThrowsAsync<EngineException>(() => s.Gateway.ConnectAsync("outro", Key, CancellationToken.None));

        Assert.Equal("MODEL_KEY_REJECTED", format.Code);
        Assert.Equal("MODEL_PROVIDER_DENIED", provider.Code);
        Assert.Equal(0, s.Transport.Calls);
    }

    [Fact]
    public async Task Provedor_que_nao_responde_no_prazo_nao_grava_nada()
    {
        using var s = new Setup(TimeSpan.FromMilliseconds(30));
        s.Transport.Behavior = async (_, ct) => await Task.Delay(TimeSpan.FromSeconds(5), ct);

        var error = await Assert.ThrowsAsync<EngineException>(() => s.Gateway.ConnectAsync("openai", Key, CancellationToken.None));

        Assert.Equal("MODEL_TIMEOUT", error.Code);
        Assert.False(File.Exists(Path.Combine(s.DataDir, "model.json")));
    }

    [Fact]
    public async Task Cancelar_a_conexao_nao_vira_timeout_nem_grava_nada()
    {
        using var s = new Setup();
        using var cancel = new CancellationTokenSource();
        s.Transport.Behavior = async (_, ct) => await Task.Delay(TimeSpan.FromSeconds(5), ct);
        var pending = s.Gateway.ConnectAsync("openai", Key, cancel.Token);

        await cancel.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        Assert.False(File.Exists(Path.Combine(s.DataDir, "model.json")));
    }

    // ------------------------------------------------------------------ Testar

    [Fact]
    public async Task Testar_registra_a_recusa_na_conexao_sem_lancar_e_uma_conferencia_boa_limpa_o_erro()
    {
        using var s = new Setup();
        await s.Gateway.ConnectAsync("openai", Key, CancellationToken.None);
        s.Clock.UtcNow = s.Clock.UtcNow.AddHours(2);
        s.Transport.Refuse();

        var refused = await s.Gateway.TestAsync(CancellationToken.None);

        Assert.Equal("error", refused.Status);
        Assert.Equal(("MODEL_AUTH_FAILED", "O provedor recusou a chave."), (refused.LastError!.Code, refused.LastError.Message));
        Assert.Equal(s.Clock.UtcNow, refused.CheckedAt);
        Assert.True(refused.Configured);

        s.Transport.Accept();
        s.Clock.UtcNow = s.Clock.UtcNow.AddHours(1);
        var accepted = await s.Gateway.TestAsync(CancellationToken.None);

        Assert.Equal("connected", accepted.Status);
        Assert.Null(accepted.LastError);
        Assert.Equal(s.Clock.UtcNow, accepted.CheckedAt);
    }

    [Theory]
    [InlineData("MODEL_RATE_LIMITED")]
    [InlineData("MODEL_UNAVAILABLE")]
    [InlineData("MODEL_INVALID_RESPONSE")]
    public async Task Cada_motivo_de_falha_do_provedor_fica_registrado_com_o_proprio_codigo(string code)
    {
        using var s = new Setup();
        await s.Gateway.ConnectAsync("openai", Key, CancellationToken.None);
        s.Transport.Refuse(code, "motivo");

        var connection = await s.Gateway.TestAsync(CancellationToken.None);

        Assert.Equal(("error", code), (connection.Status, connection.LastError!.Code));
    }

    [Fact]
    public async Task Testar_com_prazo_estourado_registra_timeout()
    {
        using var s = new Setup(TimeSpan.FromMilliseconds(30));
        s.Gateway.Save("openai", Key);
        s.Transport.Behavior = async (_, ct) => await Task.Delay(TimeSpan.FromSeconds(5), ct);

        var connection = await s.Gateway.TestAsync(CancellationToken.None);

        Assert.Equal(("error", "MODEL_TIMEOUT"), (connection.Status, connection.LastError!.Code));
    }

    // ------------------------------------------------------------------ Estado

    [Fact]
    public async Task Chave_que_ja_existia_aparece_como_nunca_conferida_ate_ser_testada()
    {
        using var s = new Setup();
        new FileModelSettings(s.DataDir).Save("openai", Key);

        Assert.Equal("untested", s.Gateway.Connection().Status);
        Assert.Equal("connected", (await s.Gateway.TestAsync(CancellationToken.None)).Status);
    }

    [Fact]
    public async Task O_estado_sobrevive_a_reiniciar_e_salvar_ou_remover_a_chave_o_apaga()
    {
        using var s = new Setup();
        await s.Gateway.ConnectAsync("openai", Key, CancellationToken.None);
        Assert.Equal("connected", s.New().Connection().Status);

        s.Gateway.Save("openai", OtherKey);
        Assert.Equal("untested", s.New().Connection().Status);

        await s.Gateway.TestAsync(CancellationToken.None);
        s.Gateway.Clear();
        Assert.Equal("none", s.New().Connection().Status);
        Assert.False(File.Exists(Path.Combine(s.DataDir, "model-check.json")));
    }

    [Fact]
    public async Task Arquivo_de_estado_corrompido_conta_como_nunca_conferida()
    {
        using var s = new Setup();
        s.Gateway.Save("openai", Key);
        File.WriteAllText(Path.Combine(s.DataDir, "model-check.json"), "{ nao e json");

        Assert.Equal("untested", s.Gateway.Connection().Status);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task A_chave_nunca_aparece_no_estado_nem_no_arquivo_dele()
    {
        using var s = new Setup();
        s.Transport.Refuse(message: "O provedor recusou a chave.");
        await Assert.ThrowsAsync<EngineException>(() => s.Gateway.ConnectAsync("openai", Key, CancellationToken.None));
        s.Transport.Accept();
        await s.Gateway.ConnectAsync("openai", Key, CancellationToken.None);
        s.Transport.Refuse();
        var failed = await s.Gateway.TestAsync(CancellationToken.None);

        Assert.DoesNotContain(Key, Serialized(failed));
        Assert.DoesNotContain(Key, File.ReadAllText(Path.Combine(s.DataDir, "model-check.json")));
    }

    // ------------------------------------------------------------------ Onde a chave está

    [Fact]
    public void Com_cofre_a_chave_diz_que_esta_no_cofre_e_sem_cofre_diz_que_esta_em_arquivo()
    {
        using var project = TempProject.Dotnet();
        var vault = new MemoryVault();
        var local = new LocalModelSettings(project.DataDir, vault);

        Assert.Null(local.Protection());

        local.Save("openai", Key);
        Assert.Equal(ModelKeyProtection.Vault, local.Protection());

        vault.Available = false;
        Assert.Null(local.Protection());
        new FileModelSettings(project.DataDir).Save("openai", Key);
        Assert.Equal(ModelKeyProtection.File, local.Protection());
    }

    // ------------------------------------------------------------------ O transporte

    private sealed class StatusHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, "MODEL_AUTH_FAILED")]
    [InlineData(HttpStatusCode.Forbidden, "MODEL_AUTH_FAILED")]
    [InlineData(HttpStatusCode.TooManyRequests, "MODEL_RATE_LIMITED")]
    [InlineData(HttpStatusCode.InternalServerError, "MODEL_INVALID_RESPONSE")]
    public async Task O_transporte_distingue_chave_recusada_de_uso_limitado_sem_repetir_o_corpo(HttpStatusCode status, string code)
    {
        // O corpo de uma recusa pode citar a chave: ele nunca vai para a mensagem.
        using var transport = new HttpsModelTransport(new StatusHandler(status, "{\"error\":{\"message\":\"chave " + Key + " invalida\"}}"));

        var simple = await Assert.ThrowsAsync<EngineException>(() => transport.SendAsync(ModelGateway.Endpoint, Key, "oi", 8, CancellationToken.None));
        var chat = await Assert.ThrowsAsync<EngineException>(() =>
            transport.SendChatAsync(ModelGateway.Endpoint, Key, ModelGateway.DefaultModel, new ChatRequest([new ChatMessage("user", "oi")], 8, false), CancellationToken.None));

        Assert.Equal((code, code), (simple.Code, chat.Code));
        Assert.DoesNotContain(Key, simple.Message + chat.Message);
    }

    // ------------------------------------------------------------------ Protocolo e contrato

    private static JsonObject Call(EngineSession session, string method, JsonObject? parameters = null) =>
        session.Handle(new JsonObject { ["v"] = 1, ["kind"] = "request", ["id"] = "t", ["method"] = method, ["params"] = parameters ?? new JsonObject() }.ToJsonString()).Response;

    [Fact]
    public async Task O_protocolo_guarda_so_depois_de_o_provedor_aceitar_e_responde_o_estado_de_sempre()
    {
        using var s = new Setup();
        var session = new EngineSession("test", models: s.Gateway);

        s.Transport.Refuse();
        var refused = Call(session, "model.save", new JsonObject { ["provider"] = "openai", ["key"] = Key });
        Assert.Equal("MODEL_AUTH_FAILED", refused["error"]!["code"]!.GetValue<string>());
        Assert.DoesNotContain(Key, refused.ToJsonString());
        Assert.False(Call(session, "model.status")["result"]!["configured"]!.GetValue<bool>());

        s.Transport.Accept();
        var saved = Call(session, "model.save", new JsonObject { ["provider"] = "openai", ["key"] = Key });
        Assert.True(ContractFiles.IsValid(ContractFiles.Schema("protocol", "methods", "model.status.result.schema.json"), saved["result"]));
        Assert.True(saved["result"]!["configured"]!.GetValue<bool>());
        Assert.DoesNotContain(Key, saved.ToJsonString());
    }

    [Fact]
    public async Task Os_metodos_de_conexao_e_teste_respondem_e_nao_trazem_a_chave()
    {
        using var s = new Setup();
        var session = new EngineSession("test", models: s.Gateway);

        var none = Call(session, "model.connection");
        Assert.Equal("none", none["result"]!["status"]!.GetValue<string>());

        var noKey = Call(session, "model.test");
        Assert.Equal("MODEL_NOT_CONFIGURED", noKey["error"]!["code"]!.GetValue<string>());

        s.Gateway.Save("openai", Key);
        var tested = Call(session, "model.test");
        Assert.Equal("connected", tested["result"]!["status"]!.GetValue<string>());
        Assert.DoesNotContain(Key, tested.ToJsonString());
        Assert.Equal("connected", Call(session, "model.connection")["result"]!["status"]!.GetValue<string>());
        await Task.CompletedTask;
    }

    [Fact]
    public async Task Fixtures_de_contrato_da_conexao_do_modelo()
    {
        using var s = new Setup();
        await s.Gateway.ConnectAsync("openai", Key, CancellationToken.None);
        var connected = s.Gateway.Connection();
        s.Transport.Refuse();
        var failed = await s.Gateway.TestAsync(CancellationToken.None);

        foreach (var (name, connection) in new[] { ("connection", connected), ("connection-error", failed) })
        {
            var pretty = JsonNode.Parse(Serialized(connection))!.ToJsonString(new JsonSerializerOptions
            {
                WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }).ReplaceLineEndings("\n");
            Assert.DoesNotContain(Key, pretty);

            var path = ContractFiles.Path_("fixtures", "models", name + ".json");
            if (Environment.GetEnvironmentVariable("MARILIZ_UPDATE_GOLDEN") == "1" || !File.Exists(path))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, pretty + "\n");
                continue;
            }

            Assert.True(pretty == File.ReadAllText(path).ReplaceLineEndings("\n").TrimEnd(),
                $"{name}: a resposta mudou. Se foi deliberado, regenere com MARILIZ_UPDATE_GOLDEN=1 e ajuste o Zod da interface.");
        }
    }
}
