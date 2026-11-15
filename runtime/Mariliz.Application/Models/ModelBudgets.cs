using System.Globalization;

namespace Mariliz.Application.Models;

public sealed record ModelBudget(string Purpose, string Label, string Description, int DefaultTokens, int MinTokens, int MaxTokens)
{
    public bool Accepts(int tokens) => tokens >= MinTokens && tokens <= MaxTokens;
}

// O teto soma entrada e resposta de todas as chamadas de uma execução; o broker cobra antes e depois de cada chamada. O juiz roda dentro da execução do fluxo e gasta do mesmo teto.
// Teto em tokens, não em dinheiro (D4, 2026-11-05). Os padrões são os tetos fixos de antes: 8.000 no fluxo e 30.000 na investigação, que faz até quatro chamadas.
public static class ModelBudgets
{
    public static IReadOnlyList<ModelBudget> All { get; } =
    [
        new(ModelPurposes.Workflow, "Fluxos de engenharia",
            "Por execução de um fluxo de engenharia: classificação, plano, arquitetura, buscas reformuladas e a segunda opinião do juiz somadas.",
            8_000, 2_000, 64_000),
        new(ModelPurposes.Assistant, "Investigação do assistente",
            "Por investigação em etapas do assistente gerencial. Uma pergunta direta ao assistente é uma chamada só, com resposta curta, e não passa por este teto.",
            30_000, 5_000, 120_000)
    ];

    public static ModelBudget? Find(string? purpose) => All.FirstOrDefault(budget => string.Equals(budget.Purpose, purpose, StringComparison.Ordinal));

    public static int Default(string purpose) => Find(purpose)?.DefaultTokens ?? throw new ArgumentException($"Finalidade sem teto: {purpose}.", nameof(purpose));

    // Um valor fora da faixa (arquivo editado à mão, faixa que mudou de uma versão para outra) vale como não escolhido.
    public static int Resolve(ModelBudget budget, IReadOnlyDictionary<string, int>? chosen) =>
        chosen is not null && chosen.TryGetValue(budget.Purpose, out var tokens) && budget.Accepts(tokens) ? tokens : budget.DefaultTokens;

    public static string Format(int tokens) => tokens.ToString("N0", CultureInfo.InvariantCulture).Replace(',', '.');
}

// Só no diretório de dados do motor. Sem escolha, vale o padrão da finalidade.
public interface IModelBudgetStore
{
    IReadOnlyDictionary<string, int> Read();

    // Nulo volta ao padrão.
    void Write(string purpose, int? tokens);
}
