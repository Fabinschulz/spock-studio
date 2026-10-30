namespace Mariliz.Core.Discovery;

public sealed record GitState(bool IsRepository, bool? HasUncommittedChanges, IReadOnlyList<string> ChangedPaths);
