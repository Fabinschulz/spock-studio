using Mariliz.Application.Iac;
using Mariliz.Application.Tests.Support;

namespace Mariliz.Application.Tests;

// Entrega 10: o mesmo desenho gera o modelo de pasta no Azure (Container Apps, PostgreSQL só com Entra ID, Service Bus)
// e no Google Cloud (Cloud Run, Cloud SQL com IP privado e login IAM, Pub/Sub). A prova com os providers reais está em
// IacRuntimeLiveTests (fmt e validate).
public sealed class IacMultiCloudTests
{
    private static string Find(IReadOnlyList<IacGeneratedFile> files, string path) => files.Single(f => f.Path == path).Content;

    [Fact]
    public void Desenhos_completos_de_azure_e_google_passam_nas_checagens()
    {
        Assert.All(IacBlueprintChecks.Check(IacBlueprintSamples.AzureFull()), c => Assert.True(c.Passed, c.Id));
        Assert.All(IacBlueprintChecks.Check(IacBlueprintSamples.GoogleFull()), c => Assert.True(c.Passed, c.Id));
    }

    [Fact]
    public void Azure_gera_o_modelo_com_container_apps_postgres_sem_senha_e_service_bus()
    {
        var files = IacGenerators.Generate(IacBlueprintSamples.AzureFull(), "infra");

        var modules = files.Select(f => f.Path).Where(p => p.StartsWith("infra/modules/", StringComparison.Ordinal)).Select(p => p.Split('/')[2]).Distinct().Order();
        Assert.Equal(["acr", "containerapp", "keyvault", "monitor", "postgres", "servicebus"], modules);
        Assert.Contains(files, f => f.Path == "infra/terraform.tfvars.example");
        var providers = Find(files, "infra/providers.tf");
        Assert.Contains("version = \"" + IacBootstrapTemplates.AzurermVersion + "\"", providers);
        Assert.Contains("backend \"azurerm\"", providers);
        Assert.Contains("storage_account_name = \"lojatfstate\"", providers);
        Assert.Contains("features {}", providers);
        var postgres = Find(files, "infra/modules/postgres/resource.tf");
        Assert.Contains("password_auth_enabled         = false", postgres);
        Assert.Contains("public_network_access_enabled = false", postgres);
        Assert.Contains("prevent_destroy = true", postgres);
        Assert.Contains("local_auth_enabled  = false", Find(files, "infra/modules/servicebus/resource.tf"));
        Assert.Contains("admin_enabled       = false", Find(files, "infra/modules/acr/resource.tf"));
        var root = Find(files, "infra/resource.tf");
        Assert.Contains("resource \"azurerm_resource_group\" \"main\"", root);
        Assert.Matches("cpu +[=] 0[.]5", root);
        Assert.Matches("memory +[=] .1Gi.", root);
        Assert.Contains("zone_redundant", root);
        Assert.DoesNotMatch("(?m)^\\s*(administrator_)?password\\s*=", string.Concat(files.Select(f => f.Content)));
        Assert.All(files, f => Assert.DoesNotContain("\r", f.Content));
        Assert.DoesNotContain(((char)0x2014).ToString(), string.Concat(files.Select(f => f.Content)));
    }

    [Fact]
    public void Google_gera_o_modelo_com_cloud_run_cloud_sql_privado_e_pubsub()
    {
        var files = IacGenerators.Generate(IacBlueprintSamples.GoogleFull(), "infra");
        var paths = files.Select(f => f.Path).ToList();

        Assert.Contains("infra/modules/cloudrun/resource.tf", paths);
        Assert.Contains("infra/modules/cloudsql/resource.tf", paths);
        Assert.Contains("infra/modules/pubsub/resource.tf", paths);
        Assert.Contains("infra/modules/artifact/resource.tf", paths);
        Assert.Contains("infra/modules/monitoring/resource.tf", paths);
        var providers = Find(files, "infra/providers.tf");
        Assert.Contains("backend \"gcs\"", providers);
        Assert.Contains("prefix = \"loja-api\"", providers);
        var sql = Find(files, "infra/modules/cloudsql/resource.tf");
        Assert.Contains("ipv4_enabled    = false", sql);
        Assert.Contains("cloudsql.iam_authentication", sql);
        Assert.Contains("point_in_time_recovery_enabled = true", sql);
        Assert.Contains("deletion_protection = true", sql);
        var root = Find(files, "infra/resource.tf");
        Assert.Matches("database_version +[=] .POSTGRES_16.", root);
        Assert.Contains("\"run.googleapis.com\"", root);
        Assert.Matches("cpu +[=] .1.", root);
        Assert.Matches("memory +[=] .2Gi.", root);
        Assert.Contains("max_delivery_attempts = 5", Find(files, "infra/modules/pubsub/resource.tf"));
        Assert.DoesNotMatch("(?m)^\\s*(root_)?password\\s*=", string.Concat(files.Select(f => f.Content)));
        Assert.All(files, f => Assert.DoesNotContain("\r", f.Content));
    }

    [Fact]
    public void Google_com_mysql_usa_log_binario_e_a_flag_de_login_iam_do_mysql()
    {
        var blueprint = IacBlueprintSamples.GoogleFull() with { Database = IacBlueprintSamples.GoogleFull().Database! with { Engine = "mysql", EngineVersion = "8.0" } };

        var files = IacGenerators.Generate(blueprint, "infra");

        var sql = Find(files, "infra/modules/cloudsql/resource.tf");
        Assert.Contains("binary_log_enabled", sql);
        Assert.Contains("cloudsql_iam_authentication", sql);
        Assert.Matches("database_version +[=] .MYSQL_8_0.", Find(files, "infra/resource.tf"));
    }

    [Fact]
    public void So_filas_no_azure_e_no_google_nao_pedem_rede_nem_conta_de_servico()
    {
        var azure = IacBlueprintSamples.AzureFull() with { Service = null, Database = null, Network = new IacNetwork("", [], []) };
        var google = IacBlueprintSamples.GoogleFull() with { Service = null, Database = null, Network = new IacNetwork("", [], []) };

        Assert.All(IacBlueprintChecks.Check(azure), c => Assert.True(c.Passed, c.Id));
        Assert.All(IacBlueprintChecks.Check(google), c => Assert.True(c.Passed, c.Id));
        Assert.DoesNotContain(IacGenerators.Generate(azure, "infra"), f => f.Path.Contains("containerapp"));
        Assert.DoesNotContain(IacGenerators.Generate(google, "infra"), f => f.Path.Contains("cloudrun"));
        Assert.Contains(IacGenerators.Costs(google), c => c.Resource.StartsWith("Pub/Sub", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("azure-subscription", "account")]
    [InlineData("azure-size", "service-size")]
    [InlineData("azure-mysql", "database-engine")]
    [InlineData("azure-storage", "database-storage")]
    [InlineData("azure-ha-burstable", "database-ha")]
    [InlineData("azure-dns", "database-dns")]
    [InlineData("azure-certificate", "certificate")]
    [InlineData("gcp-project", "account")]
    [InlineData("gcp-size", "service-size")]
    [InlineData("gcp-tier", "database-class")]
    [InlineData("gcp-network", "database-network")]
    [InlineData("gcp-region", "region")]
    public void Checagens_de_azure_e_google_apontam_o_que_cada_nuvem_nao_aceita(string scenario, string failing)
    {
        var azure = IacBlueprintSamples.AzureFull();
        var google = IacBlueprintSamples.GoogleFull();
        var blueprint = scenario switch
        {
            "azure-subscription" => azure with { Account = "123456789012" },
            "azure-size" => azure with { Service = azure.Service! with { Cpu = 512, Memory = 2048 } },
            "azure-mysql" => azure with { Database = azure.Database! with { Engine = "mysql" } },
            "azure-storage" => azure with { Database = azure.Database! with { StorageGb = 20 } },
            "azure-ha-burstable" => azure with { Database = azure.Database! with { InstanceClass = "B_Standard_B1ms", MultiAz = true } },
            "azure-dns" => azure with { Network = azure.Network with { PrivateDnsZone = null } },
            "azure-certificate" => azure with { Service = azure.Service! with { CertificateArn = IacBlueprintSamples.Certificate } },
            "gcp-project" => google with { Account = null },
            "gcp-size" => google with { Service = google.Service! with { Cpu = 256, Memory = 512 } },
            "gcp-tier" => google with { Database = google.Database! with { InstanceClass = "db.t4g.micro" } },
            "gcp-network" => google with { Network = new IacNetwork("vpc-0a1b2c3d4e5f60718", [], []) },
            _ => google with { Region = "sa-east-1" }
        };

        var failed = IacBlueprintChecks.Check(blueprint).Where(c => !c.Passed).Select(c => c.Id).ToList();

        Assert.Equal([failing], failed);
    }
}
