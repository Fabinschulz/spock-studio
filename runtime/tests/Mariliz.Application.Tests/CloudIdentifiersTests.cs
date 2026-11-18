using Mariliz.Application.Iac;

namespace Mariliz.Application.Tests;

// O blueprint e o bootstrap usam os mesmos formatos; antes, o blueprint aceitava um container que o bootstrap recusava.
public sealed class CloudIdentifiersTests
{
    [Theory]
    [InlineData("tfstate", true)]
    [InlineData("tf-state", true)]
    [InlineData("tf--state", false)]
    [InlineData("-tfstate", false)]
    public void Container_do_azure_recusa_hifen_duplo(string name, bool valid) =>
        Assert.Equal(valid, CloudIdentifiers.AzureContainer().IsMatch(name));

    [Theory]
    [InlineData("brazilsouth", true)]
    [InlineData("eastus2", true)]
    [InlineData("2eastus", false)]
    [InlineData("ab", false)]
    public void Regiao_do_azure_comeca_com_letra(string location, bool valid) =>
        Assert.Equal(valid, CloudIdentifiers.AzureLocation().IsMatch(location));
}
