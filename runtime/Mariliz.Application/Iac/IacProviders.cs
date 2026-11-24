using Mariliz.Application.Cloud;

namespace Mariliz.Application.Iac;

/// <param name="TerraformName">O nome do provider no required_providers: é por ele que o state diz qual nuvem a raiz usa.</param>
/// <param name="Backend">O backend remoto de state que o bootstrap cria nessa nuvem.</param>
/// <param name="ServiceNeedsNetwork">O serviço precisa de rede existente (VPC na AWS); no Azure e no Google só o banco precisa.</param>
public sealed record IacProvider(
    string Id,
    string TerraformName,
    string Backend,
    Func<IacBlueprint, string, IReadOnlyList<IacGeneratedFile>> Generate,
    Func<IacBlueprint, IReadOnlyList<IacCostItem>> Costs,
    Action<IacBlueprint, Action<string, string, bool, string?>> Checks,
    bool ServiceNeedsNetwork,
    IacProviderNotes Notes,
    IacProviderOptions Options);

/// <param name="Backend">Completa "o state fica local até você criar ...".</param>
/// <param name="Registry">Completa "O primeiro apply precisa de uma imagem ...".</param>
public sealed record IacProviderNotes(string Backend, string Registry, string Protection);

// Uma linha por nuvem da IaC: um provedor novo entra aqui e em CloudProviders, sem caçar literais pelo código. Quem não
// acha o provedor recusa; nada cai em AWS por omissão.
public static class IacProviders
{
    public static readonly IacProvider Aws = new(CloudProviders.Aws, "aws", "s3", IacAwsGenerator.Generate, IacAwsGenerator.Costs,
        IacBlueprintChecks.Aws, ServiceNeedsNetwork: true,
        new("o bucket com o bootstrap e colar o bloco backend \"s3\"", "no ECR: o README explica a ordem (ECR, segredo e IAM primeiro)",
            "O banco nasce com proteção contra exclusão e snapshot final: apagar exige tirar a proteção num apply anterior."),
        IacProviderOptionsCatalog.Aws);

    public static readonly IacProvider Azure = new(CloudProviders.Azure, "azurerm", "azurerm", IacAzureGenerator.Generate, IacAzureGenerator.Costs,
        IacBlueprintChecks.Azure, ServiceNeedsNetwork: false,
        new("a storage account com o bootstrap e colar o bloco backend \"azurerm\"",
            "no Azure Container Registry: o README explica a ordem (registro e Key Vault primeiro)",
            "O banco nasce com prevent_destroy: apagar exige tirar a proteção do código antes."),
        IacProviderOptionsCatalog.Azure);

    public static readonly IacProvider Gcp = new(CloudProviders.Gcp, "google", "gcs", IacGoogleGenerator.Generate, IacGoogleGenerator.Costs,
        IacBlueprintChecks.Google, ServiceNeedsNetwork: false,
        new("o bucket com o bootstrap e colar o bloco backend \"gcs\"", "no Artifact Registry: o README explica a ordem (APIs, repositório e segredo primeiro)",
            "O banco nasce com deletion_protection: apagar exige tirar a proteção num apply anterior."),
        IacProviderOptionsCatalog.Gcp);

    public static readonly IReadOnlyList<IacProvider> All = [Aws, Azure, Gcp];

    public static IacProvider? Find(string id) => All.FirstOrDefault(provider => provider.Id == id);

    public static IacProvider Require(string id) => Find(id) ?? throw IacErrors.ProviderUnknown(id);

    public static IacProvider? ByTerraformName(string name) => All.FirstOrDefault(provider => provider.TerraformName == name);

    public static CloudProcessNames ProcessNames(CloudHub cloud, CloudConnection connection) =>
        cloud.Providers.Find(connection.Provider) is ICloudProcessCredentials credentials
            ? credentials.ProcessNames(connection.Settings)
            : throw IacErrors.ProviderWithoutProcessNames(connection.Provider);
}
