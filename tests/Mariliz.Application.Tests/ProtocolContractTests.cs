using System.Text.Json.Nodes;
using Mariliz.Application.Protocol;

namespace Mariliz.Application.Tests;

public class ProtocolContractTests
{
    public static IEnumerable<object[]> Valid() => ContractFiles.Fixtures("valid");
    public static IEnumerable<object[]> Invalid() => ContractFiles.Fixtures("invalid");

    [Theory, MemberData(nameof(Valid))]
    public void Fixture_valida_passa_no_schema_do_envelope(string name)
    {
        var schema = ContractFiles.Schema("protocol", "envelope.v1.schema.json");
        Assert.True(ContractFiles.IsValid(schema, ContractFiles.ReadFixture("valid", name)), name);
    }

    [Theory, MemberData(nameof(Invalid))]
    public void Fixture_invalida_falha_no_schema_do_envelope(string name)
    {
        var schema = ContractFiles.Schema("protocol", "envelope.v1.schema.json");
        Assert.False(ContractFiles.IsValid(schema, ContractFiles.ReadFixture("invalid", name)), name);
    }

    [Fact]
    public void Hello_do_motor_respeita_os_schemas_de_envelope_e_de_resultado()
    {
        var session = new EngineSession("0.1.0");
        var line = ContractFiles.ReadFixture("valid", "request.engine.hello.json")!.ToJsonString();

        var (response, _) = session.Handle(line);

        Assert.True(ContractFiles.IsValid(ContractFiles.Schema("protocol", "envelope.v1.schema.json"), response));
        Assert.True(ContractFiles.IsValid(
            ContractFiles.Schema("protocol", "methods", "engine.hello.result.schema.json"),
            response["result"]));
    }

    [Fact]
    public void Params_da_fixture_de_hello_respeitam_o_schema_de_params()
    {
        var req = ContractFiles.ReadFixture("valid", "request.engine.hello.json")!;
        Assert.True(ContractFiles.IsValid(
            ContractFiles.Schema("protocol", "methods", "engine.hello.params.schema.json"), req["params"]));
    }

    [Theory]
    [InlineData("não é json", ProtocolErrorCodes.InvalidMessage)]
    [InlineData("[1,2]", ProtocolErrorCodes.InvalidMessage)]
    [InlineData("{\"v\":2,\"kind\":\"request\",\"id\":\"a\",\"method\":\"engine.hello\"}", ProtocolErrorCodes.UnsupportedProtocol)]
    [InlineData("{\"v\":1,\"kind\":\"request\",\"id\":\"a\",\"method\":\"nada.existe\"}", ProtocolErrorCodes.MethodNotFound)]
    [InlineData("{\"v\":1,\"kind\":\"request\",\"id\":\"a\",\"method\":\"engine.hello\",\"params\":{\"protocol\":[7],\"hostVersion\":\"1\"}}", ProtocolErrorCodes.UnsupportedProtocol)]
    [InlineData("{\"v\":1,\"kind\":\"request\",\"id\":\"a\",\"method\":\"engine.hello\"}", ProtocolErrorCodes.InvalidParams)]
    public void Mensagem_invalida_gera_erro_tipado_que_tambem_respeita_o_envelope(string line, string code)
    {
        var (response, shutdown) = new EngineSession("0.1.0").Handle(line);

        Assert.False(shutdown);
        Assert.Equal(code, response["error"]!["code"]!.GetValue<string>());
        Assert.True(ContractFiles.IsValid(ContractFiles.Schema("protocol", "envelope.v1.schema.json"), response));
    }

    [Fact]
    public void Shutdown_encerra_a_sessao()
    {
        var (_, shutdown) = new EngineSession("0.1.0").Handle("{\"v\":1,\"kind\":\"request\",\"id\":\"s\",\"method\":\"engine.shutdown\"}");
        Assert.True(shutdown);
    }

    [Fact]
    public async Task Sessao_responde_uma_linha_por_mensagem_e_ignora_linhas_vazias()
    {
        var input = new StringReader(
            "{\"v\":1,\"kind\":\"request\",\"id\":\"1\",\"method\":\"engine.hello\",\"params\":{\"protocol\":[1],\"hostVersion\":\"x\"}}\n\n" +
            "{\"v\":1,\"kind\":\"request\",\"id\":\"2\",\"method\":\"engine.shutdown\"}\n" +
            "{\"v\":1,\"kind\":\"request\",\"id\":\"3\",\"method\":\"engine.hello\"}\n");
        var output = new StringWriter { NewLine = "\n" };

        await new EngineSession("0.1.0").RunAsync(input, output, CancellationToken.None);

        var lines = output.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, lines.Length);
        Assert.Equal("1", JsonNode.Parse(lines[0])!["id"]!.GetValue<string>());
        Assert.Equal("2", JsonNode.Parse(lines[1])!["id"]!.GetValue<string>());
    }
}
