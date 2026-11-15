using Mariliz.Application.Cloud;
using Mariliz.Application.Iac;
using Mariliz.Application.Tests.Support;
using Mariliz.Infrastructure.Cloud.Aws;
using Xunit.Abstractions;

namespace Mariliz.Application.Tests;

// A estimativa contra a Price List Query API de verdade: prova que cada consulta do desenho completo acha um preço único
// na lista pública da AWS. Só lê preços públicos; nada da conta. Rode com MARILIZ_LIVE_AWS_PROFILE=<perfil do AWS CLI>.
public sealed class AwsPricingLiveTests(ITestOutputHelper output)
{
    private const string Variable = "MARILIZ_LIVE_AWS_PROFILE";

    [LiveFact(Variable)]
    public async Task Cada_item_do_desenho_completo_tem_preco_unico_na_lista_da_aws()
    {
        var settings = new CloudSettings(AwsCloudProvider.ProfileMechanism, new Dictionary<string, string>
        {
            [AwsCloudProvider.ProfileField] = Environment.GetEnvironmentVariable(Variable)!,
            [CloudFields.Region] = "sa-east-1"
        });
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
