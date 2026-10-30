namespace Mariliz.Application.Harness;

/// <param name="UpdateTlc">Pedido explícito: re-executar o instalador oficial para trazer a versão mais recente do TLC.</param>
public sealed record PlanOptions(string? AiEnvironment, bool AllowTlcForceReinstall = false, bool UpdateTlc = false);
