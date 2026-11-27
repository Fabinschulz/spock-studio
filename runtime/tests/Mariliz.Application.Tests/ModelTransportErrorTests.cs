using System.Net;
using Mariliz.Application.Execution;
using Mariliz.Application.Models;
using Mariliz.Application.Tests.Support;
using Mariliz.Infrastructure;

namespace Mariliz.Application.Tests;

public class ModelTransportErrorTests
{
    private const string Key = "chave-falsa-transporte-0000";

    private sealed class Throwing(Exception error) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => throw error;
    }

    private static Task<EngineException> Fail(HttpMessageHandler handler)
    {
        var transport = new HttpsModelTransport(handler);
        var request = new ChatRequest([new ChatMessage("user", "oi")], 32, false);
        return Assert.ThrowsAsync<EngineException>(() =>
            transport.SendChatAsync(ModelProviders.OpenAi.Endpoint, Key, ModelProviders.OpenAi.DefaultModel, request, CancellationToken.None));
    }

    [Theory]
    [InlineData(HttpRequestError.NameResolutionError, "NameResolutionError")]
    [InlineData(HttpRequestError.SecureConnectionError, "SecureConnectionError")]
    [InlineData(HttpRequestError.ConnectionError, "ConnectionError")]
    public async Task Falha_de_conexao_e_indisponivel_e_diz_a_causa_sem_repetir_a_mensagem_da_excecao(HttpRequestError error, string expected)
    {
        var failure = await Fail(new Throwing(new HttpRequestException(error, "detalhe com " + Key)));

        Assert.Equal("MODEL_UNAVAILABLE", failure.Code);
        Assert.Contains(expected, failure.Message);
        Assert.DoesNotContain(Key, failure.Message);
    }

    [Fact]
    public async Task Excecao_de_conexao_sem_causa_conhecida_diz_o_tipo()
    {
        var failure = await Fail(new Throwing(new HttpRequestException("x")));

        Assert.Equal("MODEL_UNAVAILABLE", failure.Code);
        Assert.Contains("HttpRequestException", failure.Message);
    }

    [Theory]
    [InlineData("<html>Bad Gateway page</html>")]
    [InlineData("")]
    [InlineData("data: {\"id\":1}\n\n")]
    public async Task Resposta_que_chegou_mas_nao_e_JSON_nao_e_falha_de_conexao(string body)
    {
        var failure = await Fail(new RecordingHandler(body));

        Assert.Equal("MODEL_INVALID_RESPONSE", failure.Code);
        Assert.Contains("fora do contrato", failure.Message);
        Assert.DoesNotContain("Bad Gateway", failure.Message);
    }

    [Fact]
    public async Task Json_com_o_formato_errado_tambem_e_resposta_invalida_e_nao_conexao()
    {
        var failure = await Fail(new RecordingHandler("""{"choices":[{"message":{"content":123}}],"usage":{"total_tokens":"muitos"}}"""));

        Assert.Equal("MODEL_INVALID_RESPONSE", failure.Code);
    }
}
