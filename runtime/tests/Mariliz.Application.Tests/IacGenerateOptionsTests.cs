using System.Text.Json.Nodes;
using Mariliz.Application.Iac;
using Mariliz.Application.Integrations;
using Mariliz.Application.Tests.Support;

namespace Mariliz.Application.Tests;

// A tela monta o formulário com o que o motor anuncia em iac.generate.options: nada oferecido ali pode cair nas checagens.
public sealed class IacGenerateOptionsTests
{
    public static TheoryData<string> Providers => new(IacProviders.All.Select(provider => provider.Id));

    private static IacBlueprint Sample(string provider) => provider switch
    {
        "aws" => IacBlueprintSamples.Full(),
        "azure" => IacBlueprintSamples.AzureFull(),
        "gcp" => IacBlueprintSamples.GoogleFull(),
        _ => throw new ArgumentOutOfRangeException(nameof(provider))
    };

    private static IacBlueprintCheck CheckOf(IacBlueprint blueprint, string id) =>
        IacBlueprintChecks.Check(blueprint).Single(check => check.Id == id);

    [Theory]
    [MemberData(nameof(Providers))]
    public void Todo_tamanho_oferecido_passa_na_checagem_da_nuvem(string provider)
    {
        var sample = Sample(provider);
        var options = IacProviders.Require(provider).Options;

        Assert.NotEmpty(options.Service.Sizes);
        foreach (var size in options.Service.Sizes)
        {
            var blueprint = sample with { Service = sample.Service! with { Cpu = size.Cpu, Memory = size.Memory } };
            Assert.True(CheckOf(blueprint, "service-size").Passed, $"{provider} {size.Cpu}/{size.Memory}");
        }
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public void O_banco_padrao_e_cada_motor_oferecido_passam_nas_checagens(string provider)
    {
        var sample = Sample(provider);
        var database = IacProviders.Require(provider).Options.Database;
        var standard = sample with
        {
            Database = sample.Database! with { InstanceClass = database.InstanceClass, StorageGb = database.Storage.DefaultGb, MultiAz = false }
        };

        Assert.All(["database-class", "database-storage", "database-ha"], id =>
            Assert.True(IacBlueprintChecks.Check(standard).Where(check => check.Id == id).All(check => check.Passed), $"{provider} {id}"));
        foreach (var engine in database.Engines)
            Assert.True(CheckOf(standard with { Database = standard.Database! with { Engine = engine } }, "database-engine").Passed, $"{provider} {engine}");
        Assert.False(CheckOf(standard with { Database = standard.Database! with { Engine = "oracle" } }, "database-engine").Passed);
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public void A_faixa_de_armazenamento_anunciada_e_a_que_a_checagem_aceita(string provider)
    {
        var sample = Sample(provider);
        var storage = IacProviders.Require(provider).Options.Database.Storage;
        bool Accepted(int gb) => CheckOf(sample with { Database = sample.Database! with { StorageGb = gb } }, "database-storage").Passed;

        Assert.True(Accepted(storage.MinGb) && Accepted(storage.MaxGb) && Accepted(storage.DefaultGb));
        Assert.False(Accepted(storage.MinGb - 1));
        Assert.False(Accepted(storage.MaxGb + 1));
        Assert.All(storage.AllowedGb, gb => Assert.True(Accepted(gb)));
        if (storage.AllowedGb.Count > 0) Assert.False(Accepted(storage.AllowedGb[0] + 1));
    }

    [Fact]
    public void Proposta_com_ia_e_estimativa_de_custo_so_onde_o_motor_sabe_fazer()
    {
        var withDraft = IacProviders.All.Where(provider => provider.Options.ArchitectDraft).Select(provider => provider.Id);
        var withCost = IacProviders.All.Where(provider => provider.Options.CostEstimate).Select(provider => provider.Id);

        Assert.Equal(["aws"], withDraft);
        Assert.Equal(["aws"], withCost);
        Assert.Equal(IacProviders.All.Select(provider => provider.Id), IacGenerateOptions.Current.Providers.Select(options => options.Id));
    }

    // A interface lê a mesma fixture no Zod.
    [Fact]
    public void Contrato_das_opcoes_tem_fixture_gerada_pelo_motor()
    {
        var result = new JsonObject { ["options"] = IntegrationJson.Node(IacGenerateOptions.Current) };
        var pretty = result.ToJsonString(new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        }).ReplaceLineEndings("\n");
        var path = ContractFiles.Path_("fixtures", "iac", "generate-options.json");
        if (Environment.GetEnvironmentVariable("MARILIZ_UPDATE_GOLDEN") == "1" || !File.Exists(path))
        {
            File.WriteAllText(path, pretty + "\n");
            return;
        }

        Assert.True(pretty == File.ReadAllText(path).ReplaceLineEndings("\n").TrimEnd(),
            "iac.generate.options: o resultado mudou. Se foi deliberado, regenere com MARILIZ_UPDATE_GOLDEN=1 e ajuste o Zod da interface.");
    }
}
