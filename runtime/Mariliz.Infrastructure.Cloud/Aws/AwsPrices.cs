using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Amazon.Pricing;
using Amazon.Pricing.Model;
using Amazon.Runtime;
using Mariliz.Application.Cloud;

namespace Mariliz.Infrastructure.Cloud.Aws;

// Price List Query API: os preços públicos sob demanda, filtrados no servidor pelos atributos e escolhidos aqui pelo tipo de
// uso e pela unidade. O tipo de uso vem com o prefixo da região (SAE1-Fargate-GB-Hours) fora de us-east-1, por isso a
// comparação é feita sem ele.
internal static class AwsPrices
{
    // A API só responde nestas regiões; us-east-1 serve preços de todas.
    private const string Endpoint = "us-east-1";
    private const int MaxPages = 5;

    public static async Task<IReadOnlyList<CloudPriceQuote>> QuoteAsync(AWSCredentials credentials, IReadOnlyList<CloudPriceQuery> queries,
        CancellationToken ct)
    {
        using var pricing = new AmazonPricingClient(credentials, AwsClients.Config<AmazonPricingConfig>(Endpoint));
        var quotes = new List<CloudPriceQuote>();
        foreach (var query in queries)
        {
            var products = new List<string>();
            string? token = null;
            for (var page = 0; page < MaxPages; page++)
            {
                var request = new GetProductsRequest
                {
                    ServiceCode = query.Service,
                    FormatVersion = "aws_v1",
                    MaxResults = 100,
                    NextToken = token,
                    Filters = [Term("regionCode", query.Region), .. query.Attributes.Select(a => Term(a.Key, a.Value))]
                };
                var response = await pricing.GetProductsAsync(request, ct);
                products.AddRange(response.PriceList ?? []);
                token = response.NextToken;
                if (string.IsNullOrEmpty(token)) break;
            }
            quotes.Add(Pick(query, products));
        }
        return quotes;
    }

    private static Amazon.Pricing.Model.Filter Term(string field, string value) => new() { Type = FilterType.TERM_MATCH, Field = field, Value = value };

    // Um preço só quando todos os produtos que batem concordam; dois preços diferentes para a mesma consulta viram null, em vez
    // de um número escolhido ao acaso. Em preço por faixa vale a primeira faixa.
    internal static CloudPriceQuote Pick(CloudPriceQuery query, IEnumerable<string> priceList)
    {
        var found = new List<(decimal Price, string Description)>();
        foreach (var item in priceList)
        {
            JsonNode? product;
            try
            {
                product = JsonNode.Parse(item);
            }
            catch (JsonException)
            {
                continue;
            }
            var attributes = product?["product"]?["attributes"] as JsonObject;
            if (attributes is null || !SameUsage(Text(attributes["usagetype"]), query.UsageType)) continue;
            // productFamily vem ao lado de attributes, não dentro; o filtro do servidor aceita os dois.
            if (query.Attributes.Any(a => (Text(attributes[a.Key]) ?? Text(product?["product"]?[a.Key])) != a.Value)) continue;

            var dimensions = (product?["terms"]?["OnDemand"] as JsonObject ?? [])
                .SelectMany(term => term.Value?["priceDimensions"] as JsonObject ?? [])
                .Select(d => d.Value as JsonObject)
                .OfType<JsonObject>()
                .Where(d => string.Equals(Text(d["unit"]), query.Unit, StringComparison.OrdinalIgnoreCase))
                .Select(d => (Begin: Number(Text(d["beginRange"])) ?? 0, Price: Number(Text(d["pricePerUnit"]?["USD"])), Description: Text(d["description"]) ?? ""))
                .Where(d => d.Price is not null)
                .OrderBy(d => d.Begin)
                .ToList();
            if (dimensions.Count > 0) found.Add((dimensions[0].Price!.Value, dimensions[0].Description));
        }
        var prices = found.Select(f => f.Price).Distinct().ToList();
        return prices.Count == 1 ? new CloudPriceQuote(query, prices[0], found[0].Description) : new CloudPriceQuote(query, null, null);
    }

    private static bool SameUsage(string? usage, string wanted)
    {
        if (usage is null) return false;
        if (usage == wanted) return true;
        return usage.EndsWith("-" + wanted, StringComparison.Ordinal) && !usage[..^(wanted.Length + 1)].Contains('-');
    }

    private static string? Text(JsonNode? node) => node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

    private static decimal? Number(string? text) =>
        decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ? number : null;
}
