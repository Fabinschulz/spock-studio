using Mariliz.Application.Management.Investigation;
using Mariliz.Application.Models;
using Mariliz.Application.Protocol;
using Mariliz.Infrastructure;

namespace Mariliz.Application.Tests;

public class ModelTimeoutsTests
{
    [Fact]
    public void O_padrao_e_de_120_s_e_o_cliente_python_espera_5_s_a_mais()
    {
        Assert.Equal(TimeSpan.FromSeconds(120), ModelTimeouts.DefaultChat);
        Assert.Equal(ModelTimeouts.Chat + TimeSpan.FromSeconds(5), ModelTimeouts.Client);
    }

    [Theory]
    [InlineData("90", 90)]
    [InlineData("10", 10)]
    [InlineData("600", 600)]
    public void Valor_na_faixa_vale(string raw, int seconds) =>
        Assert.Equal(TimeSpan.FromSeconds(seconds), ModelTimeouts.Resolve(raw));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("9")]
    [InlineData("601")]
    [InlineData("-30")]
    [InlineData("99999999999")]
    public void Valor_ausente_ilegivel_ou_fora_da_faixa_volta_ao_padrao(string? raw) =>
        Assert.Equal(ModelTimeouts.DefaultChat, ModelTimeouts.Resolve(raw));

    [Fact]
    public void O_prazo_da_investigacao_cobre_o_orcamento_mais_uma_chamada_de_modelo()
    {
        var budget = TimeSpan.FromMilliseconds(InvestigationService.RuntimeBudgetMilliseconds);

        // A conclusão pode começar no fim do orçamento e levar o prazo de uma chamada: cortar antes jogava fora a resposta pronta.
        Assert.True(InvestigationService.StandardDeadline >= budget + ModelTimeouts.Chat);
        Assert.True(ProtocolDeadlines.ByMethod[ProtocolMethods.ManagementInvestigate] > InvestigationService.StandardDeadline);
    }

    [Fact]
    public void O_runtime_de_ia_recebe_o_prazo_do_cliente_derivado_do_do_motor()
    {
        var environment = LoopbackAiRuntime.PythonEnvironment("t");

        Assert.Equal(((int)ModelTimeouts.Client.TotalSeconds).ToString(), environment["MARILIZ_MODEL_CLIENT_TIMEOUT_SECONDS"]);
    }
}
