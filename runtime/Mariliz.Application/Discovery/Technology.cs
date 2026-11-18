using System.Text.RegularExpressions;

namespace Mariliz.Application.Discovery;

/// <param name="Area">A área que o agente de arquitetura do Python atribui à stack (servico, interface); nula quando não há.</param>
/// <param name="Architecture">Como a stack aparece escrita no documento de arquitetura aprovado; nulo quando o documento não a nomeia.</param>
public sealed record Technology(string Id, string Name, string Category, RuleFamily Rules, string? Area, Regex? Architecture);
