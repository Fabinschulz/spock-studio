using Mariliz.Application.Ports;

namespace Mariliz.Application.Tests;

public class AckStatusTests
{
    [Theory]
    [InlineData("completed", AckStatus.Completed)]
    [InlineData("interrupted", AckStatus.Interrupted)]
    [InlineData("failed", AckStatus.Failed)]
    public void Os_tres_valores_do_contrato_viram_o_tipo_e_voltam_iguais(string wire, AckStatus status)
    {
        Assert.Equal(status, AckStatusWire.Parse(wire));
        Assert.Equal(wire, status.ToWire());
    }

    [Theory]
    [InlineData("")]
    [InlineData("Completed")]
    [InlineData("cancelled")]
    [InlineData("completed ")]
    public void Qualquer_outro_valor_e_resposta_fora_do_contrato(string wire) => Assert.Null(AckStatusWire.Parse(wire));
}
