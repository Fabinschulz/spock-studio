namespace Mariliz.Core.Discovery;

public sealed record DiscoveryWarning(string Code, string Message, IReadOnlyList<string> Paths);
