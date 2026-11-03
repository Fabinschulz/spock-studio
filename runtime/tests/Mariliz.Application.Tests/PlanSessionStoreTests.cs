using Mariliz.Application.Discovery;
using Mariliz.Application.Execution;
using Mariliz.Application.Harness;
using Mariliz.Core.Plan;

namespace Mariliz.Application.Tests;

public class PlanSessionStoreTests
{
    [Fact]
    public void Plano_recem_gerado_nunca_e_o_descartado_ao_passar_da_capacidade()
    {
        var store = new PlanSessionStore();
        for (var i = 0; i < 25; i++)
        {
            store.Save(Session($"plan-{i:00}"));
            Assert.NotNull(store.Find($"plan-{i:00}"));
        }

        Assert.Null(store.Find("plan-04"));
        Assert.NotNull(store.Find("plan-05"));
        Assert.NotNull(store.Find("plan-24"));
    }

    [Fact]
    public void Salvar_de_novo_o_mesmo_plano_o_mantem_como_o_mais_novo()
    {
        var store = new PlanSessionStore();
        for (var i = 0; i < 20; i++) store.Save(Session($"plan-{i:00}"));

        store.Save(Session("plan-00"));
        store.Save(Session("plan-20"));

        Assert.NotNull(store.Find("plan-00"));
        Assert.Null(store.Find("plan-01"));
    }

    [Theory]
    [InlineData(new[] { "net9.0", "net10.0" }, "net10.0")]
    [InlineData(new[] { "netstandard2.0", "net10.0" }, "net10.0")]
    [InlineData(new[] { "net48", "net8.0" }, "net8.0")]
    [InlineData(new[] { "netcoreapp3.1", "netstandard2.1" }, "netcoreapp3.1")]
    [InlineData(new[] { "net8.0-windows", "net6.0" }, "net8.0-windows")]
    [InlineData(new[] { "net472", "net48" }, "net48")]
    [InlineData(new[] { "lixo" }, null)]
    public void Framework_mais_novo_compara_familia_e_versao(string[] frameworks, string? expected)
    {
        Assert.Equal(expected, DotnetDetector.Newest(frameworks));
    }

    private static PlanSession Session(string planId) => new(
        "p-0123456789ab", "C:/x", new PlanOptions(null),
        new ConfigurationPlan(planId, "p-0123456789ab", "claude-code", "mariliz", "none", [], [], [], [], [], [], []),
        null!);
}
