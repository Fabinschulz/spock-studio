using Mariliz.Core.Discovery;

namespace Mariliz.Application.Engineering;

/// <summary>Uma tecnologia do projeto, juntando todas as pastas em que ela aparece.</summary>
/// <param name="Versions">Versões declaradas nos arquivos de configuração, sem repetição. Vazio quando nenhum arquivo declara.</param>
/// <param name="Evidence">Os arquivos que provam a detecção.</param>
public sealed record ProfileTechnology(
    string Id,
    string Name,
    string Category,
    IReadOnlyList<string> Versions,
    IReadOnlyList<string> Roots,
    Confidence Confidence,
    IReadOnlyList<string> Evidence);

public sealed record ProfileTool(string Kind, string Name, string? Command);

/// <summary>Uma ferramenta externa ligada ao projeto pela central de integrações.</summary>
public sealed record ProfileConnection(string Role, string Provider, string Resource);

/// <summary>Como o Harness atende uma tecnologia do perfil: com uma regra própria ou ainda sem perfil de regras.</summary>
public sealed record ProfileHarnessGuidance(string Technology, string? RuleId, bool Covered, string Detail);

/// <summary>Uma diferença entre duas análises do projeto.</summary>
/// <param name="Kind"><c>created</c>, <c>added</c>, <c>removed</c> ou <c>changed</c>.</param>
public sealed record ProfileChange(DateTimeOffset At, string Kind, string Subject, string? From, string? To);

/// <summary>
/// O perfil tecnológico de um projeto: o que a descoberta encontrou, as conexões com ferramentas externas e como o
/// Harness atende cada tecnologia. Guardado no diretório de dados do motor, com o histórico das mudanças entre análises.
/// </summary>
public sealed record TechnologyProfile(
    string ProjectId,
    DateTimeOffset AnalyzedAt,
    IReadOnlyList<ProfileTechnology> Technologies,
    IReadOnlyList<ProfileTool> Tooling,
    IReadOnlyList<string> AiEnvironments,
    IReadOnlyList<ProfileConnection> Connections,
    IReadOnlyList<ProfileHarnessGuidance> Harness,
    IReadOnlyList<ProfileChange> History);
