using Mariliz.Application.Cloud;
using Mariliz.Application.Iac;
using Mariliz.Application.Tests.Support;
using Mariliz.Infrastructure.Cloud.Aws;

namespace Mariliz.Application.Tests;

// A estimativa contra a Price List Query API de verdade: prova que cada consulta do desenho completo acha um preço único
// na lista pública da AWS. Só lê preços públicos; nada da conta. Rode com MARILIZ_LIVE_AWS_PROFILE=<perfil do AWS CLI>, ou
// =default com as credenciais nas variáveis de ambiente.
public sealed class AwsPricingLiveTests(ITestOutputHelper output)
{
    private const string Variable = "MARILIZ_LIVE_AWS_PROFILE";

    [LiveFact(Variable)]
    public async Task Cada_item_do_desenho_completo_tem_preco_unico_na_lista_da_aws()
    {
        // "default" usa a cadeia padrão do SDK (variáveis de ambiente, por exemplo as de aws configure export-credentials).
        var profile = Environment.GetEnvironmentVariable(Variable)!;
        var settings = profile == "default"
            ? new CloudSettings(AwsCloudProvider.DefaultMechanism, new Dictionary<string, string> { [CloudFields.Region] = "sa-east-1" })
            : new CloudSettings(AwsCloudProvider.ProfileMechanism, new Dictionary<string, string>
            {
                [AwsCloudProvider.ProfileField] = profile,
                [CloudFields.Region] = "sa-east-1"
            });
        // A leitura mínima que a verificação de permissões da conexão faz para a capacidade aws.pricing.
        var services = await new AwsCloudProvider().ReadAsync(settings, new CloudQuery("aws.pricing", null, 200), CancellationToken.None);
        output.WriteLine($"aws.pricing: {services.Rows.Count} serviço(s) na lista, truncado={services.Truncated}");
        Assert.Contains(services.Rows, row => row[0] == "AmazonECS");

        var blueprint = IacBlueprintSamples.Full();
        var multiAz = blueprint with { Database = blueprint.Database! with { MultiAz = true, Engine = "mysql" } };

        foreach (var sample in new[] { blueprint, multiAz })
        {
            var items = IacCostEstimator.Items(sample);
            var quotes = await new AwsCloudProvider().PricesAsync(settings, items.Select(i => i.Query).ToList(), CancellationToken.None);
            var estimate = IacCostEstimator.Compose(sample, items, quotes, DateTimeOffset.UtcNow);

            foreach (var line in estimate.Fixed.Concat(estimate.UsageBased))
                output.WriteLine($"{line.Resource}: {line.UnitPrice} por {line.Unit} -> {line.Monthly} ({line.PriceDescription})");
            output.WriteLine($"Total fixo: {estimate.MonthlyFixed} {estimate.Currency}");
            Assert.All(estimate.Fixed.Concat(estimate.UsageBased), line => Assert.NotNull(line.UnitPrice));
            Assert.True(estimate.Complete);
        }
    }
}
