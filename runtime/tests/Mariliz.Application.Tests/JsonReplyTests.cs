using Mariliz.Infrastructure;

namespace Mariliz.Application.Tests;

public class JsonReplyTests
{
    [Theory]
    [InlineData("{\"a\":1}", "{\"a\":1}")]
    [InlineData("  \n{\"a\":1}\n ", "{\"a\":1}")]
    [InlineData("```json\n{\"a\":1}\n```", "{\"a\":1}")]
    [InlineData("Aqui está o desenho:\n{\"a\":1}", "{\"a\":1}")]
    [InlineData("Aqui está:\n```json\n{\"a\":{\"b\":[1,2]}}\n```\nQualquer ajuste, é só dizer.", "{\"a\":{\"b\":[1,2]}}")]
    [InlineData("{\"a\":1}\n\nObservação: usei {chaves} no texto.", "{\"a\":1}")]
    [InlineData("Use {chaves} assim. Resultado: {\"a\":\"}\"}", "{\"a\":\"}\"}")]
    [InlineData("{\"a\":\"aspas \\\" e } dentro\"} fim", "{\"a\":\"aspas \\\" e } dentro\"}")]
    public void Devolve_so_o_objeto_JSON(string reply, string expected) => Assert.Equal(expected, JsonReply.Extract(reply));

    [Theory]
    [InlineData("não há JSON aqui")]
    [InlineData("{\"a\":1")]
    [InlineData("[1,2,3]")]
    [InlineData("")]
    public void O_que_nao_traz_objeto_JSON_volta_como_veio_para_quem_le_recusar(string reply) => Assert.Equal(reply, JsonReply.Extract(reply));

    [Fact]
    public void Resposta_cortada_no_meio_nao_vira_um_objeto_menor()
    {
        // O objeto de fora não fecha: o de dentro, que por acaso está completo, não é o que o modelo quis dizer.
        var truncated = "{\"a\":{\"b\":1},\"c\":[1,2";

        Assert.Equal(truncated, JsonReply.Extract(truncated));
    }

    [Fact]
    public void Chave_repetida_vale_a_ultima_em_qualquer_nivel_e_ler_a_chave_nao_lanca()
    {
        var reply = "Segue:\n```json\n{\"assumptions\":[\"a\"],\"x\":{\"k\":1,\"k\":[true,null,2.5,\"t\"]},\"assumptions\":[\"b\",\"c\"],\"list\":[{\"i\":1,\"i\":2}]}\n```";

        var json = System.Text.Json.Nodes.JsonNode.Parse(JsonReply.Extract(reply))!.AsObject();

        Assert.Equal(["b", "c"], json["assumptions"]!.AsArray().Select(item => item!.GetValue<string>()));
        Assert.Equal(4, json["x"]!["k"]!.AsArray().Count);
        Assert.Equal(2, json["list"]![0]!["i"]!.GetValue<int>());
    }

    [Fact]
    public void Sem_chave_repetida_o_texto_nao_e_reescrito()
    {
        var reply = "{\"a\": 1,  \"b\": [1, 2]}";

        Assert.Equal(reply, JsonReply.Extract(reply));
    }
}
