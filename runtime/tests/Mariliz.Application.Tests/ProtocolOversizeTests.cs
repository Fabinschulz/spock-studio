using System.Text.Json.Nodes;
using Mariliz.Application.Protocol;

namespace Mariliz.Application.Tests;

// Uma mensagem acima do limite precisa voltar com o id do pedido: quem correlaciona resposta por id (o host, a interface) nunca recebe
// um erro sem id e fica esperando até o prazo. Achado da varredura rota por rota com o motor real.
public class ProtocolOversizeTests
{
    private static readonly string Huge = new('A', ProtocolMessages.MaxMessageBytes + 10);

    private static JsonObject Send(string line) => new EngineSession("teste").Handle(line).Response;

    private static string? Id(JsonObject response) => response["id"]?.GetValue<string>();

    private static string? Code(JsonObject response) => response["error"]?["code"]?.GetValue<string>();

    [Fact]
    public void Mensagem_grande_demais_volta_com_o_id_do_pedido()
    {
        var response = Send("{\"v\":1,\"kind\":\"request\",\"id\":\"t42\",\"method\":\"engine.hello\",\"params\":{\"texto\":\"" + Huge + "\"}}");

        Assert.Equal("MESSAGE_TOO_LARGE", Code(response));
        Assert.Equal("t42", Id(response));
    }

    [Fact]
    public void O_id_do_envelope_vale_mesmo_quando_vem_depois_de_outros_campos_curtos()
    {
        var response = Send("{\"v\":1,\"kind\":\"request\",\"method\":\"engine.hello\",\"id\":\"t7\",\"params\":{\"texto\":\"" + Huge + "\"}}");

        Assert.Equal("t7", Id(response));
    }

    [Fact]
    public void Id_dentro_dos_params_nunca_e_tomado_pelo_id_do_pedido()
    {
        var response = Send("{\"v\":1,\"kind\":\"request\",\"params\":{\"id\":\"falso\"},\"method\":\"engine.hello\",\"texto\":\"" + Huge + "\"}");

        Assert.Equal("MESSAGE_TOO_LARGE", Code(response));
        Assert.Null(Id(response));
    }

    [Fact]
    public void Sem_id_no_comeco_da_linha_o_erro_continua_sem_id_e_nao_quebra()
    {
        var response = Send("{\"v\":1,\"kind\":\"request\",\"method\":\"engine.hello\",\"texto\":\"" + Huge + "\",\"id\":\"tarde-demais\"}");

        Assert.Equal("MESSAGE_TOO_LARGE", Code(response));
        Assert.Null(Id(response));
    }

    [Theory]
    [InlineData("não é json nenhum")]
    [InlineData("{\"v\":1,\"id\":123,\"texto\":\"x")]
    [InlineData("{\"id\":")]
    public void Prefixo_que_nao_e_json_ou_id_que_nao_e_texto_nao_gera_id(string start)
    {
        Assert.Null(EngineSession.TryReadId(start + Huge));
    }

    [Fact]
    public void Id_gigante_nao_e_devolvido()
    {
        var response = Send("{\"v\":1,\"kind\":\"request\",\"id\":\"" + new string('x', 500) + "\",\"method\":\"x\",\"texto\":\"" + Huge + "\"}");

        Assert.Equal("MESSAGE_TOO_LARGE", Code(response));
        Assert.Null(Id(response));
    }

    [Fact]
    public void Mensagem_dentro_do_limite_segue_o_caminho_normal()
    {
        var response = Send("{\"v\":1,\"kind\":\"request\",\"id\":\"t1\",\"method\":\"metodo.que.nao.existe\"}");

        Assert.Equal("METHOD_NOT_FOUND", Code(response));
        Assert.Equal("t1", Id(response));
    }
}
