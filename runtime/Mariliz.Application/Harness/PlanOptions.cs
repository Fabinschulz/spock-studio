namespace Mariliz.Application.Harness;

/// <param name="StableId">F1: o id que o plano grava em project.id quando a declaração ainda não tem um. Vem do registro local, então replanejar não troca o id.</param>
/// <param name="UpdateSpecification">Pedido explícito: re-executar o instalador oficial para trazer a versão mais recente da estrutura de especificação.</param>
/// <param name="AiEnvironments">C3: as IDEs escolhidas na tela. Diferente do que a declaração diz, o plano grava a lista em aiEnvironments.</param>
public sealed record PlanOptions(
    string? AiEnvironment,
    bool AllowSpecificationReinstall = false,
    bool UpdateSpecification = false,
    IReadOnlyList<string>? ExcludeComponents = null,
    IReadOnlyList<string>? IncludeComponents = null,
    string? StableId = null,
    IReadOnlyList<string>? AiEnvironments = null);
