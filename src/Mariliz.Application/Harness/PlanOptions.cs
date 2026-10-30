namespace Mariliz.Application.Harness;

/// <param name="UpdateSpecification">Pedido explícito: re-executar o instalador oficial para trazer a versão mais recente da estrutura de especificação.</param>
public sealed record PlanOptions(string? AiEnvironment, bool AllowSpecificationReinstall = false, bool UpdateSpecification = false);
