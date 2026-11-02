namespace Mariliz.Application.Tests;

/// <summary>
/// Contratos v2 entre o motor e a inteligência e os dados dos eventos novos da interface. Cada fixture se chama
/// <c>&lt;schema&gt;--&lt;caso&gt;.json</c> e é conferida contra <c>contracts/schemas/&lt;pasta&gt;/&lt;schema&gt;.schema.json</c>.
/// </summary>
public class IntelligenceContractTests
{
    private static readonly string[] Folders = ["intelligence", "ai"];

    public static IEnumerable<object[]> Valid() => Cases("valid");
    public static IEnumerable<object[]> Invalid() => Cases("invalid");

    [Theory, MemberData(nameof(Valid))]
    public void Fixture_valida_passa_no_schema(string folder, string name)
    {
        Assert.True(ContractFiles.IsValid(SchemaOf(folder, name), ContractFiles.ReadFixture(Path.Combine(folder, "valid"), name)), name);
    }

    [Theory, MemberData(nameof(Invalid))]
    public void Fixture_invalida_falha_no_schema(string folder, string name)
    {
        Assert.False(ContractFiles.IsValid(SchemaOf(folder, name), ContractFiles.ReadFixture(Path.Combine(folder, "invalid"), name)), name);
    }

    [Fact]
    public void Todo_schema_da_fronteira_tem_ao_menos_uma_fixture_valida_e_uma_invalida()
    {
        foreach (var schema in Directory.GetFiles(ContractFiles.Path_("schemas", "intelligence"), "*.schema.json"))
        {
            var name = Path.GetFileName(schema)[..^".schema.json".Length];
            Assert.Contains(Valid(), item => (string)item[0] == "intelligence" && ((string)item[1]).StartsWith(name + "--", StringComparison.Ordinal));
            Assert.Contains(Invalid(), item => (string)item[0] == "intelligence" && ((string)item[1]).StartsWith(name + "--", StringComparison.Ordinal));
        }
    }

    private static IEnumerable<object[]> Cases(string kind) =>
        Folders.SelectMany(folder => ContractFiles.Fixtures(Path.Combine(folder, kind)).Select(item => new object[] { folder, (string)item[0] }));

    private static Json.Schema.JsonSchema SchemaOf(string folder, string fixture)
    {
        var separator = fixture.IndexOf("--", StringComparison.Ordinal);
        Assert.True(separator > 0, $"A fixture {fixture} não segue <schema>--<caso>.json.");
        return ContractFiles.Schema(folder, fixture[..separator] + ".schema.json");
    }
}
