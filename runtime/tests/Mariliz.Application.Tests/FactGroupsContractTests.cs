using System.Text.Json.Nodes;
using Mariliz.Application.Management.Assistant;
using Mariliz.Application.Tests.Support;

namespace Mariliz.Application.Tests;

/// <summary>
/// O catálogo de grupos de fatos é do motor; a fixture <c>contracts/fixtures/management/fact-groups.json</c> é a cópia que os
/// testes e a avaliação do runtime de IA leem. Os dois não podem divergir.
/// </summary>
public class FactGroupsContractTests
{
    private static JsonArray Catalog() => new(FactGroups.All.Select(group => (JsonNode)new JsonObject
    {
        ["name"] = group.Name,
        ["roles"] = new JsonArray(group.Roles.Select(role => (JsonNode)role).ToArray()),
        ["keywords"] = new JsonArray(group.Keywords.Select(keyword => (JsonNode)keyword).ToArray())
    }).ToArray());

    [Fact]
    public void A_fixture_dos_grupos_e_igual_ao_catalogo_do_motor()
    {
        var fixture = ContractFiles.ReadFixture("management", "fact-groups.json");

        Assert.True(JsonNode.DeepEquals(Catalog(), fixture), "Atualize contracts/fixtures/management/fact-groups.json junto com FactGroups.");
    }

    [Fact]
    public void Todo_grupo_que_o_pacote_de_evidencias_usa_esta_no_catalogo()
    {
        var names = FactGroups.All.Select(group => group.Name).ToHashSet(StringComparer.Ordinal);

        Assert.Equal(names.Count, FactGroups.All.Count);
        Assert.Contains(FactGroups.All, group => group.Roles.Contains(FactGroupRoles.Base));
        Assert.Contains(FactGroups.All, group => group.Roles.Contains(FactGroupRoles.Default));
        Assert.Contains(FactGroups.All, group => group.Roles.Contains(FactGroupRoles.Sign));
    }

    [Fact]
    public void O_pedido_leva_so_os_descritores_dos_grupos_presentes_na_ordem_do_catalogo()
    {
        var facts = new List<EvidenceFact>
        {
            new("risk.none", FactGroups.Risks, "Nenhum risco."),
            new("today", FactGroups.Context, "Hoje."),
            new("flow.items", FactGroups.Flow, "Itens.")
        };

        Assert.Equal([FactGroups.Context, FactGroups.Flow, FactGroups.Risks], FactGroups.For(facts).Select(group => group.Name));
    }
}
