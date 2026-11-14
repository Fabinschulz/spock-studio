namespace Mariliz.Application.AiTools;

/// <summary>
/// O caminho do executável não sai do motor. <paramref name="Version"/> é nulo quando a ferramenta está instalada mas não
/// respondeu a versão: instalada e com versão desconhecida é um estado, não um erro.
/// </summary>
public sealed record AiToolStatus(string Id, string Name, string Binary, bool Installed, string? Version, bool HarnessSupport);
