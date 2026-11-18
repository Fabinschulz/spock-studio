using Mariliz.Application.Discovery;

namespace Mariliz.Application.Harness;

/// <param name="Name">Como o plano e o perfil chamam a família ("Stack .NET detectada").</param>
/// <param name="Guidance">A frase que o perfil tecnológico mostra para uma stack desta família.</param>
public sealed record StackRuleProfile(RuleFamily Family, string Component, string Asset, string[] Globs, string Name, string Guidance);
