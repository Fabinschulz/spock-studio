using System.Text.Json.Nodes;

namespace Mariliz.Application.Tests;

public class JsonReadTests
{
    private static readonly JsonObject Sample = JsonNode.Parse("""
        { "name": "spock", "blank": "  ", "count": 3, "flag": true, "wrong": 5, "nil": null }
        """)!.AsObject();

    [Fact]
    public void Campo_ausente_nulo_ou_de_outro_tipo_vira_null()
    {
        Assert.Equal("spock", JsonRead.Text(Sample, "name"));
        Assert.Null(JsonRead.Text(Sample, "missing"));
        Assert.Null(JsonRead.Text(Sample, "nil"));
        Assert.Null(JsonRead.Text(Sample, "wrong"));
        Assert.Null(JsonRead.Text(null, "name"));
    }

    [Fact]
    public void Texto_so_de_espacos_conta_como_ausente_onde_ele_nao_diz_nada()
    {
        Assert.Equal("  ", JsonRead.Text(Sample, "blank"));
        Assert.Null(JsonRead.NonBlank(Sample["blank"]));
        Assert.Equal("spock", JsonRead.NonBlank(Sample["name"]));
    }

    [Fact]
    public void Numero_e_marcador_leem_so_o_tipo_certo()
    {
        Assert.Equal(3, JsonRead.Int(Sample, "count"));
        Assert.Null(JsonRead.Int(Sample, "name"));
        Assert.True(JsonRead.Flag(Sample, "flag"));
        Assert.False(JsonRead.Flag(Sample, "count"));
        Assert.False(JsonRead.Flag(Sample, "missing"));
    }
}
