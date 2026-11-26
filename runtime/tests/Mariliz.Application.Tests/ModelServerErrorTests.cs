using System.Net;
using Mariliz.Application.Execution;
using Mariliz.Application.Models;
using Mariliz.Application.Tests.Support;
using Mariliz.Infrastructure;

namespace Mariliz.Application.Tests;

public class ModelServerErrorTests
{
    private const string Key = "sk-test-chave-5xx-7c1d";

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.GatewayTimeout)]
    public async Task Erro_5xx_do_provedor_vira_sobrecarga_e_nao_repete_o_corpo(HttpStatusCode status)
    {
        using var transport = new HttpsModelTransport(new RecordingHandler("{\"error\":{\"message\":\"chave " + Key + " sobrecarregado\"}}", status));
        var request = new ChatRequest([new ChatMessage("user", "oi")], 64, false);

        foreach (var provider in new[] { ModelProviders.OpenAi, ModelProviders.Anthropic })
        {
            var error = await Assert.ThrowsAsync<EngineException>(() =>
                transport.SendChatAsync(provider.Endpoint, Key, provider.DefaultModel, request, CancellationToken.None));

            Assert.Equal("MODEL_OVERLOADED", error.Code);
            Assert.Contains(((int)status).ToString(), error.Message);
            Assert.DoesNotContain(Key, error.Message);
            Assert.DoesNotContain("sobrecarregado\"", error.Message);
        }
    }

    [Fact]
    public async Task Um_4xx_que_nao_e_de_chave_continua_fora_do_contrato()
    {
        using var transport = new HttpsModelTransport(new RecordingHandler("{\"error\":\"modelo inexistente\"}", HttpStatusCode.NotFound));

        var error = await Assert.ThrowsAsync<EngineException>(() =>
            transport.SendAsync(ModelProviders.OpenAi.Endpoint, Key, "oi", 8, CancellationToken.None));

        Assert.Equal("MODEL_INVALID_RESPONSE", error.Code);
    }
}
