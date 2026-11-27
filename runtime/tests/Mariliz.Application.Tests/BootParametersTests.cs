using Mariliz.Application.Boot;

namespace Mariliz.Application.Tests;

public class BootParametersTests
{
    private static (string Name, int Value, int Requested)? One(string capability, string text) =>
        CapabilityParameters.Extract(capability, text).Select(p => (p.Parameter.Name, p.Value, p.Requested)).Cast<(string, int, int)?>().FirstOrDefault();

    [Theory]
    [InlineData("Indicadores das últimas 12 semanas", 12)]
    [InlineData("Quero ver os últimos 30 dias", 5)]
    [InlineData("Resultado dos últimos 2 meses", 8)]
    [InlineData("Relatório do trimestre", 13)]
    [InlineData("Relatório do semestre", 26)]
    [InlineData("Como foi o último mês?", 4)]
    public void O_periodo_vira_semanas(string text, int weeks) =>
        Assert.Equal(("weeks", weeks, weeks), One("management.tracking", text));

    [Fact]
    public void Periodo_fora_da_faixa_do_motor_e_ajustado_e_o_ajuste_fica_dito()
    {
        var long_ = CapabilityParameters.Extract("management.report", "Relatório das últimas 40 semanas").Single();
        var short_ = CapabilityParameters.Extract("management.report", "Relatório de 1 semana").Single();

        Assert.Equal((26, 40, true), (long_.Value, long_.Requested, long_.Clamped));
        Assert.Contains("pedido 40", long_.Note);
        Assert.Contains("de 2 a 26", long_.Note);
        Assert.Equal((2, 1), (short_.Value, short_.Requested));
    }

    [Theory]
    [InlineData("platform.timeline", "Mostre os últimos 30 eventos", 30)]
    [InlineData("platform.timeline", "Quero 500 eventos da linha do tempo", 200)]
    [InlineData("platform.audit", "Os últimos 20 registros da auditoria", 20)]
    [InlineData("platform.runs", "As últimas 10 execuções", 10)]
    [InlineData("platform.runs", "As últimas 400 execuções", 100)]
    public void A_quantidade_so_vale_com_o_substantivo_certo_e_dentro_da_faixa(string capability, string text, int expected) =>
        Assert.Equal(expected, CapabilityParameters.Extract(capability, text).Single().Value);

    [Fact]
    public void Numero_sem_unidade_da_capacidade_nao_vira_parametro()
    {
        Assert.Empty(CapabilityParameters.Extract("platform.timeline", "As últimas 12 semanas"));
        Assert.Empty(CapabilityParameters.Extract("platform.runs", "Mostre 10 eventos"));
        Assert.Empty(CapabilityParameters.Extract("management.tracking", "Quero 10 eventos"));
        Assert.Empty(CapabilityParameters.Extract("management.tracking", null));
        Assert.Empty(CapabilityParameters.Extract("engineering.catalog", "Os últimos 12 semanas"));
    }

    [Fact]
    public void Argumento_de_registro_so_passa_se_a_ficha_declara_e_sempre_dentro_da_faixa()
    {
        Assert.Equal(200, CapabilityParameters.Bound("platform.timeline", "limit", 9999));
        Assert.Equal(1, CapabilityParameters.Bound("platform.timeline", "limit", -5));
        Assert.Null(CapabilityParameters.Bound("platform.timeline", "weeks", 8));
        Assert.Null(CapabilityParameters.Bound("platform.timeline", "projectId", 1));
        Assert.Null(CapabilityParameters.Bound("engineering.catalog", "limit", 10));
    }

    [Fact]
    public void So_capacidade_direta_declara_parametro_e_o_motor_conhece_cada_nome()
    {
        foreach (var (id, specs) in CapabilityParameters.ByCapability)
        {
            var capability = CapabilityCatalog.Find(id);
            Assert.NotNull(capability);
            Assert.Equal(ExecutionMode.Direct, capability!.Mode);
            Assert.All(specs, spec => Assert.Contains(spec.Name, new[] { "weeks", "limit" }));
        }
    }
}
