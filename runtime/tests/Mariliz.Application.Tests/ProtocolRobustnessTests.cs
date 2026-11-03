using System.Text.Json.Nodes;
using Mariliz.Application.Protocol;

namespace Mariliz.Application.Tests;

/// <summary>Mensagem malformada vira erro de protocolo; nunca derruba a sessão (e com ela o motor).</summary>
public class ProtocolRobustnessTests
{
    [Theory]
    [InlineData("{\"v\":1,\"kind\":\"request\",\"id\":7,\"method\":\"engine.hello\"}", ProtocolErrorCodes.InvalidMessage)]
    [InlineData("{\"v\":\"1\",\"kind\":\"request\",\"id\":\"a\",\"method\":\"engine.hello\"}", ProtocolErrorCodes.UnsupportedProtocol)]
    [InlineData("{\"v\":1,\"kind\":3,\"id\":\"a\",\"method\":\"engine.hello\"}", ProtocolErrorCodes.InvalidMessage)]
    [InlineData("{\"v\":1,\"kind\":\"request\",\"id\":\"a\",\"method\":42}", ProtocolErrorCodes.InvalidMessage)]
    [InlineData("{\"v\":1,\"kind\":\"request\",\"id\":\"a\",\"method\":{}}", ProtocolErrorCodes.InvalidMessage)]
    public void Envelope_com_tipo_errado_vira_erro_de_protocolo(string line, string code)
    {
        var (response, shutdown) = new EngineSession("0.1.0").Handle(line);

        Assert.False(shutdown);
        Assert.Equal(code, response["error"]!["code"]!.GetValue<string>());
    }

    [Fact]
    public async Task Sessao_continua_depois_de_uma_mensagem_malformada()
    {
        var input = new StringReader(
            "{\"v\":1,\"kind\":\"request\",\"id\":7,\"method\":\"engine.hello\"}\n"
            + "{\"v\":1,\"kind\":\"request\",\"id\":\"ok\",\"method\":\"engine.hello\",\"params\":{\"protocol\":[1],\"hostVersion\":\"t\"}}\n");
        var output = new StringWriter();

        await new EngineSession("0.1.0").RunAsync(input, output, CancellationToken.None);

        var lines = output.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(l => (JsonObject)JsonNode.Parse(l)!).ToList();
        Assert.Equal(2, lines.Count);
        Assert.Equal("ok", lines[1]["id"]!.GetValue<string>());
        Assert.NotNull(lines[1]["result"]);
    }
}
