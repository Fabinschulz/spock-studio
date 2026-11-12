namespace Mariliz.Application.Harness;

/// <param name="UpdateSpecification">Pedido explícito: re-executar o instalador oficial para trazer a versão mais recente da estrutura de especificação.</param>
/// <param name="ExcludeComponents">Componentes que a pessoa pediu, na tela, para dispensar (ver <see cref="ComponentRequest"/>).</param>
/// <param name="IncludeComponents">Componentes excluídos que a pessoa pediu para voltar a aplicar.</param>
public sealed record PlanOptions(
    string? AiEnvironment,
    bool AllowSpecificationReinstall = false,
    bool UpdateSpecification = false,
    IReadOnlyList<string>? ExcludeComponents = null,
    IReadOnlyList<string>? IncludeComponents = null);
