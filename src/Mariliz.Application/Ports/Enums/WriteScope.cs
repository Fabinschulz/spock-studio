namespace Mariliz.Application.Ports;

/// <summary>
/// <c>Managed</c>: arquivos que a aplicação gera (.claude/, .mariliz/, mariliz.yaml, mariliz.lock).
/// <c>SpecificationRestore</c>: só para desfazer o efeito do instalador da estrutura de especificação, que é dono dos próprios arquivos.
/// </summary>
public enum WriteScope { Managed, SpecificationRestore }
