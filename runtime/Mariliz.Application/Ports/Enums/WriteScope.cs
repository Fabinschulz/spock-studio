namespace Mariliz.Application.Ports;

// SpecificationRestore só serve para desfazer o efeito do instalador da estrutura de especificação, que é dono dos próprios arquivos.
// SecurityFix é o de uma correção de segurança aprovada (e da reversão dela): o gerenciado mais o .gitignore da raiz.
public enum WriteScope { Managed, SpecificationRestore, SecurityFix }
