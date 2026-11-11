using System.Globalization;
using Mariliz.Application.Integrations;
using Mariliz.Core.Text;

namespace Mariliz.Application.Management;

/// <summary>
/// Previsão de entrega por simulação de Monte Carlo sobre o throughput semanal observado: cada simulação sorteia,
/// semana a semana, uma das semanas do histórico até zerar o trabalho aberto. A semente vem dos próprios dados, então
/// os mesmos itens no mesmo instante dão sempre a mesma previsão. Nenhum modelo de linguagem participa do cálculo.
/// </summary>
public static class ForecastCalculator
{
    public const int Trials = 10_000;
    public const int MaxHistoryWeeks = 12;
    public const int MinHistoryWeeks = 6;
    public const int MinDelivered = 5;

    /// <summary>Simulações que passam disso param: um ritmo que não conclui em 5 anos não é uma previsão útil.</summary>
    public const int HorizonWeeks = 260;

    public const string Method =
        "Simulação de Monte Carlo com 10.000 rodadas sobre o número de itens entregues por semana nas últimas semanas.";

    public static readonly IReadOnlyList<string> Assumptions =
    [
        "O ritmo das próximas semanas se parece com o das semanas usadas no histórico.",
        "O trabalho restante é o que está aberto hoje; nada novo entra no escopo.",
        "Os itens restantes têm, em média, o tamanho dos que já foram entregues.",
        "A equipe e a capacidade continuam as mesmas."
    ];

    public static readonly IReadOnlyList<string> Limitations =
    [
        "Não considera dependências nem bloqueios entre itens.",
        "Não considera férias, feriados nem mudança de equipe.",
        "Item aberto que na prática foi abandonado conta como trabalho restante.",
        "Tipos de dado são previstos separadamente: uma issue e o pull request que a fecha não são somados."
    ];

    private static readonly (string Id, string Label, int Percentile)[] ScenarioDefinitions =
    [
        ("optimistic", "Otimista", 15),
        ("likely", "Provável", 50),
        ("conservative", "Conservador", 85)
    ];

    public static DeliveryForecast Compute(string kind, IReadOnlyCollection<WorkItem> items, DateTimeOffset now, bool sourcesComplete)
    {
        var remaining = items.Count(DeliveryRules.IsOpen);
        var historyWeeks = Math.Min(MaxHistoryWeeks, WeeklyHistory.Covered(items, now));
        var history = historyWeeks == 0 ? [] : WeeklyHistory.Delivered(items, now, historyWeeks);

        if (remaining == 0) return Unavailable(kind, 0, historyWeeks, history, "Não há itens abertos: nada a prever.");
        if (historyWeeks < MinHistoryWeeks)
            return Unavailable(kind, remaining, historyWeeks, history,
                $"São precisas ao menos {MinHistoryWeeks} semanas de histórico; os dados cobrem {historyWeeks}.");
        if (history.Sum() < MinDelivered)
            return Unavailable(kind, remaining, historyWeeks, history,
                $"São precisos ao menos {MinDelivered} itens entregues nas últimas {historyWeeks} semanas; há {history.Sum()}.");

        var weeks = Simulate(kind, remaining, history);
        var scenarios = ScenarioDefinitions
            .Select(definition => (definition, weeks: FlowCalculator.Percentile(weeks, definition.Percentile / 100.0)))
            .ToList();
        if (scenarios.Any(scenario => scenario.weeks >= HorizonWeeks))
            return Unavailable(kind, remaining, historyWeeks, history,
                "No ritmo observado, parte relevante das simulações não conclui em 5 anos.");

        var created = WeeklyHistory.Created(items, now, historyWeeks);
        var (confidence, reasons) = Confidence(history, created, historyWeeks, sourcesComplete);
        return new DeliveryForecast(
            kind,
            remaining,
            historyWeeks,
            history,
            Trials,
            scenarios.Select(s => new ForecastScenario(s.definition.Id, s.definition.Label, s.definition.Percentile, (int)s.weeks,
                DateOnly.FromDateTime(now.UtcDateTime.AddDays(7 * s.weeks)))).ToList(),
            confidence,
            reasons,
            RiskFactors(history, created),
            null);
    }

    private static List<double> Simulate(string kind, int remaining, int[] history)
    {
        var random = new Random(Seed(kind, remaining, history));
        var results = new List<double>(Trials);
        for (var trial = 0; trial < Trials; trial++)
        {
            var left = remaining;
            var weeks = 0;
            while (left > 0 && weeks < HorizonWeeks)
            {
                left -= history[random.Next(history.Length)];
                weeks++;
            }
            results.Add(weeks);
        }
        results.Sort();
        return results;
    }

    /// <summary>Semente derivada só dos dados de entrada: mudar um item muda a previsão, repetir o pedido não.</summary>
    private static int Seed(string kind, int remaining, int[] history)
    {
        var text = $"{kind}|{remaining}|{string.Join(',', history)}";
        return int.Parse(Hashing.Sha256(text)[..7], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
    }

    /// <summary>Começa alta e cai um nível por motivo encontrado, até baixa. Cada motivo vai junto da resposta.</summary>
    private static (ForecastConfidence, List<string>) Confidence(int[] history, int[] created, int historyWeeks, bool sourcesComplete)
    {
        var reasons = new List<string>();
        if (historyWeeks < 10) reasons.Add($"Histórico curto: {historyWeeks} semanas.");
        var zeroWeeks = history.Count(count => count == 0);
        if (zeroWeeks * 3 > history.Length) reasons.Add($"{zeroWeeks} de {history.Length} semanas sem nenhuma entrega.");
        var mean = history.Average();
        var deviation = Math.Sqrt(history.Average(count => Math.Pow(count - mean, 2)));
        if (mean > 0 && deviation / mean > 1.0) reasons.Add("O número de entregas varia muito de uma semana para outra.");
        if (ScopeGrowing(created.Sum(), history.Sum())) reasons.Add("Chegaram mais itens do que saíram no período: o escopo está crescendo.");
        if (!sourcesComplete) reasons.Add("Ao menos uma fonte teve a última sincronização incompleta.");

        var level = Math.Max(0, (int)ForecastConfidence.High - reasons.Count);
        return ((ForecastConfidence)level, reasons);
    }

    private static List<string> RiskFactors(int[] history, int[] created)
    {
        var factors = new List<string>();
        var arrivals = created.Sum();
        var deliveries = history.Sum();
        if (ScopeGrowing(arrivals, deliveries))
            factors.Add($"Nas últimas {history.Length} semanas chegaram {arrivals} itens e saíram {deliveries}; se continuar assim, a data se afasta.");
        var half = history.Length / 2;
        var before = history.Take(half).Sum();
        var after = history.Skip(history.Length - half).Sum();
        if (half > 0 && before >= 4 && after * 2 <= before)
            factors.Add($"O ritmo caiu: {before} entregas na primeira metade do histórico e {after} na segunda.");
        return factors;
    }

    /// <summary>Chegadas ao menos 20% acima das entregas e com 3 itens de diferença: abaixo disso é oscilação normal.</summary>
    private static bool ScopeGrowing(int arrivals, int deliveries) => arrivals - deliveries >= 3 && arrivals >= deliveries * 1.2;

    private static DeliveryForecast Unavailable(string kind, int remaining, int historyWeeks, int[] history, string reason) =>
        new(kind, remaining, historyWeeks, history, 0, [], null, [], [], reason);
}
