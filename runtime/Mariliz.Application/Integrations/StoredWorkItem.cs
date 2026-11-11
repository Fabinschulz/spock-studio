namespace Mariliz.Application.Integrations;

/// <summary>Um item com a proveniência da leitura: quando chegou pela primeira vez e quando foi conferido por último.</summary>
public sealed record StoredWorkItem(WorkItem Item, string Hash, DateTimeOffset FirstSyncedAt, DateTimeOffset SyncedAt);
