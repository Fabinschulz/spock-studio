using Mariliz.Application.Integrations;

namespace Mariliz.Application.Management;

public static class PortfolioSignals
{
    /// <summary>Projeto sem ferramenta vinculada: falta dado, e isso não é um ponto de atenção sobre o projeto.</summary>
    public const string NoSources = "no-sources";

    /// <summary>Dados mais antigos que isso deixam de descrever o andamento atual.</summary>
    public static readonly TimeSpan StaleAfter = TimeSpan.FromDays(7);

    public static IReadOnlyList<PortfolioSignal> For(
        IReadOnlyList<(ProjectIntegration Integration, SyncedData Data, ConnectionStatus Connection)> sources,
        IReadOnlyList<KindSnapshot> kinds,
        DateTimeOffset? oldestReadAt,
        DateTimeOffset now)
    {
        if (sources.Count == 0) return [new PortfolioSignal(NoSources, "Nenhuma ferramenta vinculada: não há dados de andamento.")];

        var signals = new List<PortfolioSignal>();
        var neverSynced = sources.Count(source => source.Data.History.All(sync => sync.FinishedAt is null));
        if (neverSynced > 0)
            signals.Add(new("never-synced", Plural(neverSynced, "fonte nunca sincronizada", "fontes nunca sincronizadas")));

        var problems = sources.Count(source => LastFinished(source.Data)?.Status is SyncStatus.Failed or SyncStatus.Partial);
        if (problems > 0)
            signals.Add(new("sync-incomplete", Plural(problems, "fonte com a última sincronização incompleta", "fontes com a última sincronização incompleta")));

        var disconnected = sources.Count(source => source.Connection != ConnectionStatus.Connected);
        if (disconnected > 0)
            signals.Add(new("connection-problem", Plural(disconnected, "fonte com a conexão sem acesso", "fontes com a conexão sem acesso")));

        if (oldestReadAt is { } readAt && now - readAt > StaleAfter)
            signals.Add(new("stale", $"Dados lidos há {(int)(now - readAt).TotalDays} dias."));

        var overdue = kinds.Sum(kind => kind.Overdue ?? 0);
        if (overdue > 0)
            signals.Add(new("overdue", Plural(overdue, "item aberto com prazo vencido", "itens abertos com prazo vencido")));

        return signals;
    }

    public static SyncExecution? LastFinished(SyncedData data) => data.History.FirstOrDefault(sync => sync.FinishedAt is not null);

    private static string Plural(int count, string one, string many) => $"{count} {(count == 1 ? one : many)}.";
}
