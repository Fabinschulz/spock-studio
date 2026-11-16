using Mariliz.Application.Iac;

namespace Mariliz.Application.Tests.Support;

// Desenhos de exemplo com ids de mentira no formato real da AWS.
internal static class IacBlueprintSamples
{
    public const string Certificate = "arn:aws:acm:sa-east-1:123456789012:certificate/0f1e2d3c-4b5a-6978-8a9b-0c1d2e3f4a5b";

    public static IacBlueprint Full() => new(
        "aws", "loja-api", "sa-east-1", "123456789012", "loja-sso",
        new IacNetwork("vpc-0a1b2c3d4e5f60718",
            ["subnet-0aaaaaaaaaaaaaaa1", "subnet-0bbbbbbbbbbbbbbb2"],
            ["subnet-0ccccccccccccccc3", "subnet-0ddddddddddddddd4"]),
        new IacServiceSpec("api", 8080, "/health", 512, 1024, 2, Certificate, PublicIp: false),
        new IacDatabaseSpec("postgres", "16", "db.t4g.micro", 20, MultiAz: false),
        ["pedidos", "estoque"],
        "alertas@example.com",
        new IacBackendSpec("loja-terraform-state-123456789012-sa-east-1", "sa-east-1"));

    public const string AzureSubscription = "0f1e2d3c-4b5a-6978-8a9b-0c1d2e3f4a5b";

    // Container App, PostgreSQL só com Entra ID na subnet delegada, filas no Service Bus e state no Blob Storage.
    public static IacBlueprint AzureFull() => new(
        "azure", "loja-api", "brazilsouth", AzureSubscription, null,
        new IacNetwork("",
            [$"/subscriptions/{AzureSubscription}/resourceGroups/rede/providers/Microsoft.Network/virtualNetworks/vnet-loja/subnets/postgres"], [],
            $"/subscriptions/{AzureSubscription}/resourceGroups/rede/providers/Microsoft.Network/privateDnsZones/loja.postgres.database.azure.com"),
        new IacServiceSpec("api", 8080, "/health", 512, 1024, 2, "", PublicIp: false),
        new IacDatabaseSpec("postgres", "16", "GP_Standard_D2ds_v5", 32, MultiAz: true),
        ["pedidos", "estoque"],
        "alertas@example.com",
        new IacBackendSpec("lojatfstate", "brazilsouth", "loja-tfstate", "tfstate"));

    // Cloud Run, Cloud SQL só com IP privado e login IAM, Pub/Sub e state no GCS.
    public static IacBlueprint GoogleFull() => new(
        "gcp", "loja-api", "southamerica-east1", "loja-producao-123", null,
        new IacNetwork("projects/loja-producao-123/global/networks/loja", [], []),
        new IacServiceSpec("api", 8080, "/health", 1024, 2048, 2, "", PublicIp: false),
        new IacDatabaseSpec("postgres", "16", "db-custom-1-3840", 20, MultiAz: true),
        ["pedidos", "estoque"],
        "alertas@example.com",
        new IacBackendSpec("loja-tfstate-123", "southamerica-east1"));

    public static IacBlueprint QueuesOnly() => new(
        "aws", "eventos", "us-east-1", null, null,
        new IacNetwork("vpc-0a1b2c3d4e5f60718", ["subnet-0aaaaaaaaaaaaaaa1"], []),
        null, null, ["notas-fiscais"], null, null);
}
