using System.Net;
using System.Text.Json.Nodes;
using Mariliz.Application.Execution;
using Mariliz.Application.Models;
using Mariliz.Application.Tests.Support;
using Mariliz.Infrastructure;

namespace Mariliz.Application.Tests;

public class GoogleModelTests
{
    private const string Key = "AIzaSyTestGoogleFaseI1-3f9c_x2ab7d41";
    private static readonly ModelProvider Google = ModelProviders.Google;

    private static string Reply(string text, int input = 11, int output = 7, int thoughts = 0, string version = "gemini-versao-exata") =>
        new JsonObject
        {
            ["candidates"] = new JsonArray(new JsonObject { ["content"] = new JsonObject { ["parts"] = new JsonArray(new JsonObject { ["text"] = text }) } }),
            ["modelVersion"] = version,
            ["usageMetadata"] = new JsonObject
            {
                ["promptTokenCount"] = input, ["candidatesTokenCount"] = output, ["thoughtsTokenCount"] = thoughts,
                ["totalTokenCount"] = input + output + thoughts
            }
        }.ToJsonString();

    private static ChatRequest Chat(bool json = false, params ChatMessage[] messages) =>
        new(messages.Length == 0 ? [new ChatMessage("system", "Seja breve."), new ChatMessage("user", "oi")] : messages, 64, json);

    [Fact]
    public async Task O_Google_recebe_a_chave_no_cabecalho_dele_e_o_modelo_no_endereco()
    {
        var handler = new RecordingHandler(Reply("olá"));
        using var transport = new HttpsModelTransport(handler);

        var result = await transport.SendChatAsync(Google.Endpoint, Key, "gemini-2.5-pro",
            Chat(messages: [new ChatMessage("system", "Seja breve."), new ChatMessage("user", "oi"), new ChatMessage("assistant", "olá"), new ChatMessage("user", "e agora?")]),
            CancellationToken.None);

        Assert.Equal("https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-pro:generateContent", handler.Request!.RequestUri!.AbsoluteUri);
        Assert.Equal(Key, handler.Request.Headers.GetValues("x-goog-api-key").Single());
        Assert.Null(handler.Request.Headers.Authorization);
        Assert.DoesNotContain(Key, handler.Body.ToJsonString());
        Assert.Equal(["user", "model", "user"], handler.Body["contents"]!.AsArray().Select(c => c!["role"]!.GetValue<string>()));
        Assert.Equal("Seja breve.", handler.Body["systemInstruction"]!["parts"]![0]!["text"]!.GetValue<string>());
        Assert.Equal(64, handler.Body["generationConfig"]!["maxOutputTokens"]!.GetValue<int>());
        Assert.Equal(("olá", "gemini-versao-exata", 11, 7), (result.Text, result.ModelVersion, result.InputTokens, result.OutputTokens));
    }

    [Fact]
    public async Task O_pedido_de_json_vai_na_configuracao_e_nao_na_instrucao()
    {
        var handler = new RecordingHandler(Reply("```json\n{\"a\": 1}\n```"));
        using var transport = new HttpsModelTransport(handler);

        var json = await transport.SendChatAsync(Google.Endpoint, Key, Google.DefaultModel, Chat(json: true), CancellationToken.None);

        Assert.Equal("application/json", handler.Body["generationConfig"]!["responseMimeType"]!.GetValue<string>());
        Assert.DoesNotContain("objeto JSON", handler.Body["systemInstruction"]!["parts"]![0]!["text"]!.GetValue<string>());
        Assert.Equal("{\"a\": 1}", json.Text);
    }

    [Fact]
    public async Task Conversa_so_de_instrucoes_ainda_vira_uma_mensagem_do_usuario()
    {
        var handler = new RecordingHandler(Reply("ok"));
        using var transport = new HttpsModelTransport(handler);

        await transport.SendChatAsync(Google.Endpoint, Key, Google.DefaultModel, Chat(messages: [new ChatMessage("system", "Faça isto.")]), CancellationToken.None);

        var contents = handler.Body["contents"]!.AsArray();
        Assert.Equal(("user", "Faça isto."), (contents[0]!["role"]!.GetValue<string>(), contents[0]!["parts"]![0]!["text"]!.GetValue<string>()));
        Assert.Null(handler.Body["systemInstruction"]);
    }

    [Fact]
    public async Task A_pergunta_curta_usa_o_modelo_padrao_do_Google_e_conta_os_tokens()
    {
        var handler = new RecordingHandler(Reply("ok", input: 4, output: 2));
        using var transport = new HttpsModelTransport(handler);

        var result = await transport.SendAsync(Google.Endpoint, Key, "oi", 8, CancellationToken.None);

        Assert.EndsWith($"/{Google.DefaultModel}:generateContent", handler.Request!.RequestUri!.AbsolutePath);
        Assert.Equal(("ok", 6), (result.Text, result.Tokens));
    }

    [Fact]
    public async Task O_raciocinio_do_modelo_conta_como_saida()
    {
        var handler = new RecordingHandler(Reply("ok", input: 10, output: 5, thoughts: 20));
        using var transport = new HttpsModelTransport(handler);

        var result = await transport.SendChatAsync(Google.Endpoint, Key, Google.DefaultModel, Chat(), CancellationToken.None);

        Assert.Equal((10, 25), (result.InputTokens, result.OutputTokens));
    }

    [Theory]
    [InlineData("gemini-9.9")]
    [InlineData("gemini-2.5-flash:evil")]
    [InlineData("../../outro")]
    public async Task Modelo_fora_da_lista_nao_entra_no_endereco_e_a_chave_nao_sai(string model)
    {
        var handler = new RecordingHandler(Reply("ok"));
        using var transport = new HttpsModelTransport(handler);

        var error = await Assert.ThrowsAsync<EngineException>(() => transport.SendChatAsync(Google.Endpoint, Key, model, Chat(), CancellationToken.None));

        Assert.Equal("MODEL_CHOICE_DENIED", error.Code);
        Assert.Null(handler.Request);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task Recusa_de_chave_vale_para_o_Google_sem_repetir_o_corpo(HttpStatusCode status)
    {
        using var transport = new HttpsModelTransport(new RecordingHandler("{\"error\":{\"message\":\"chave " + Key + " recusada\"}}", status));

        var error = await Assert.ThrowsAsync<EngineException>(() => transport.SendAsync(Google.Endpoint, Key, "oi", 8, CancellationToken.None));

        Assert.Equal("MODEL_AUTH_FAILED", error.Code);
        Assert.DoesNotContain(Key, error.Message);
    }

    [Fact]
    public async Task O_Google_responde_400_a_uma_chave_invalida_e_isso_vira_recusa_de_chave()
    {
        using var transport = new HttpsModelTransport(new RecordingHandler("{\"error\":{\"status\":\"INVALID_ARGUMENT\",\"details\":[{\"reason\":\"API_KEY_INVALID\"}]}}", HttpStatusCode.BadRequest));

        var error = await Assert.ThrowsAsync<EngineException>(() => transport.SendAsync(Google.Endpoint, Key, "oi", 8, CancellationToken.None));

        Assert.Equal("MODEL_AUTH_FAILED", error.Code);
    }

    [Fact]
    public async Task Um_400_que_nao_e_de_chave_nao_vira_recusa_de_chave()
    {
        using var transport = new HttpsModelTransport(new RecordingHandler("{\"error\":{\"status\":\"INVALID_ARGUMENT\"}}", HttpStatusCode.BadRequest));

        var error = await Assert.ThrowsAsync<EngineException>(() => transport.SendAsync(Google.Endpoint, Key, "oi", 8, CancellationToken.None));

        Assert.Equal("MODEL_INVALID_RESPONSE", error.Code);
    }

    [Fact]
    public async Task Pedido_barrado_antes_de_gerar_texto_nao_tem_candidato_e_e_resposta_invalida()
    {
        using var transport = new HttpsModelTransport(new RecordingHandler("{\"promptFeedback\":{\"blockReason\":\"SAFETY\"}}"));

        var error = await Assert.ThrowsAsync<EngineException>(() => transport.SendAsync(Google.Endpoint, Key, "oi", 8, CancellationToken.None));

        Assert.Equal("MODEL_INVALID_RESPONSE", error.Code);
    }

    [Fact]
    public async Task Texto_vazio_com_candidato_nao_e_erro()
    {
        var handler = new RecordingHandler("{\"candidates\":[{\"finishReason\":\"MAX_TOKENS\"}],\"usageMetadata\":{\"promptTokenCount\":3,\"totalTokenCount\":3}}");
        using var transport = new HttpsModelTransport(handler);

        var result = await transport.SendAsync(Google.Endpoint, Key, "oi", 8, CancellationToken.None);

        Assert.Equal(("", 3), (result.Text, result.Tokens));
    }
}
