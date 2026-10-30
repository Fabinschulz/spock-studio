namespace Mariliz.Application.Ports;

/// <summary>
/// <c>Managed</c>: arquivos que a aplicação gera (.claude/, .mariliz/, mariliz.yaml, mariliz.lock).
/// <c>TlcRestore</c>: só para desfazer o efeito do instalador do TLC, que é dono dos próprios arquivos.
/// </summary>
public enum WriteScope { Managed, TlcRestore }
