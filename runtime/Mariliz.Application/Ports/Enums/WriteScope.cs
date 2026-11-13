namespace Mariliz.Application.Ports;

// SpecificationRestore só serve para desfazer o efeito do instalador da estrutura de especificação, que é dono dos próprios arquivos.
public enum WriteScope { Managed, SpecificationRestore }
