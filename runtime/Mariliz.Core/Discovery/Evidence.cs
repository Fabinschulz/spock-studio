namespace Mariliz.Core.Discovery;

/// <summary>Um fato observado: o arquivo (caminho relativo, com '/') e o valor lido dele.</summary>
public sealed record Evidence(string Path, string? Detail = null);
