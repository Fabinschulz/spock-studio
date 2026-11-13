namespace Mariliz.Core.Discovery;

// Caminho relativo, com '/'.
public sealed record Evidence(string Path, string? Detail = null);
