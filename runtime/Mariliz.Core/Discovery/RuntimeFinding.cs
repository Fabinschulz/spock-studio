namespace Mariliz.Core.Discovery;

// Versão de runtime ou SDK que o projeto fixa além da versão da stack: .nvmrc num projeto React, global.json, o
// TargetFramework de cada projeto numa solução com versões diferentes. É o que limita o código que o agente pode escrever ali.
public sealed record RuntimeFinding(
    string TechnologyId,
    string Name,
    string Root,
    string Version,
    IReadOnlyList<Evidence> Evidence);
