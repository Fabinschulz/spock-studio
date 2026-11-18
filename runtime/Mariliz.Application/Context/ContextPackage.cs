namespace Mariliz.Application.Context;

// Vocabulário do modelo canônico (CONTEXT_ENGINE.md §2 e §5). "stale" não é uma classificação: o item mantém a de origem e
// ganha freshness = stale.
public static class ContextClassifications
{
    public const string Discovered = "discovered";
    public const string Declared = "declared";
    public const string Approved = "approved";
    public const string Integration = "integration";
    public const string Calculated = "calculated";
    public const string Inferred = "inferred";
    // Padrão da pasta Organização: argumento de engenharia vindo de outros projetos, nunca convenção do projeto atual.
    public const string Reference = "reference";
}

public static class ContextFreshness
{
    public const string Fresh = "fresh";
    public const string Stale = "stale";
    public const string Unknown = "unknown";
}

public static class ContextIntents
{
    public const string Bootstrap = "bootstrap";
    public const string Architecture = "architecture";

    // As demais intenções do estudo (development, management, infrastructure, investigation, delivery) entram nas próximas
    // entregas; pedir uma delas hoje é erro, não um pacote vazio.
    public static readonly IReadOnlyList<string> Available = [Bootstrap, Architecture];
}

/// <param name="Section">identity, policies, connections, harness ou warnings (bootstrap); identity, stacks, references ou
/// warnings (architecture). Na ordem de prioridade da intenção.</param>
/// <param name="Source">De onde veio: spock, mariliz.yaml, harness, o provedor da integração (jira, github…) ou cloud:aws.</param>
/// <param name="Ref">O que identifica o item na origem: id da cópia, do vínculo, do componente.</param>
public sealed record ContextItem(
    string Section,
    string Text,
    string Source,
    string Ref,
    string Classification,
    DateTimeOffset? ObservedAt,
    string Freshness);

public sealed record ContextOmission(string Section, string Ref, string Reason);

/// <param name="ContextVersion">Hash das partes persistentes (identidade, políticas, nomes das conexões, declaração do Harness):
/// muda quando algo declarado ou aprovado muda, não a cada sincronização.</param>
/// <param name="UsedTokens">Tokens do texto renderizado, que é o que a IDE recebe.</param>
public sealed record ContextPackage(
    string ProjectId,
    string? StableId,
    string Intent,
    int BudgetTokens,
    int UsedTokens,
    string TokenMethod,
    string ContextVersion,
    DateTimeOffset ComposedAt,
    IReadOnlyList<ContextItem> Items,
    IReadOnlyList<ContextOmission> Omitted,
    IReadOnlyList<string> Limitations,
    string Text);
