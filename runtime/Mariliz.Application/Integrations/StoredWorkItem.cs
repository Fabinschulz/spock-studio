namespace Mariliz.Application.Integrations;

public sealed record StoredWorkItem(WorkItem Item, string Hash, DateTimeOffset FirstSyncedAt, DateTimeOffset SyncedAt);
