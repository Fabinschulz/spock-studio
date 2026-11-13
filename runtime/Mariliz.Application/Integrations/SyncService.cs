using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using Mariliz.Application.Execution;
using Mariliz.Application.Ports;

namespace Mariliz.Application.Integrations;

/// <summary>
/// Sincroniza o recorte de uma integração, em segundo plano. Só roda quando a pessoa pede: não há sondagem automática.
/// Cada página gravada avança o ponto de retomada, então uma falha no meio não perde o que já chegou nem repete o trabalho.
/// </summary>
public sealed class SyncService(
    ProjectIntegrationService integrations,
    ConnectionService connections,
    ConnectorRegistry connectors,
    ISyncedDataStore data,
    SyncActivity activity,
    IClock clock,
    IAuditLog audit,
    Action<string, int, string, JsonObject, DateTimeOffset>? emit = null)
{
    /// <summary>
    /// Avisa, no fim de cada sincronização, qual integração e como terminou. Quem ouve (por exemplo, o registro de risco) roda na
    /// mesma linha da sincronização, depois de tudo gravado, e uma falha dele nunca desfaz nem atrapalha a sincronização.
    /// </summary>
    public event Action<ProjectIntegration, SyncExecution>? Finished;

    /// <summary>Os eventos de uma sincronização. O <c>runId</c> do evento é o id da sincronização.</summary>
    public const string StartedEvent = "sync.started";
    public const string ProgressEvent = "sync.progress";
    public const string FinishedEvent = "sync.finished";

    /// <summary>Teto de páginas por execução: limita as chamadas que um pedido pode disparar no provedor.</summary>
    public const int MaxPages = 50;

    private readonly CancellationTokenSource _shutdown = new();
    private readonly ConcurrentDictionary<string, Task> _tasks = new(StringComparer.Ordinal);

    /// <summary>Começa uma sincronização e devolve na hora. Se já há uma em andamento na integração, devolve essa.</summary>
    public SyncExecution Start(string projectId, string integrationId)
    {
        var integration = integrations.Require(projectId, integrationId);
        var (execution, started) = Begin(integration);
        if (started)
            _tasks[execution.Id] = Task.Run(async () =>
            {
                try { await RunAsync(integration, execution, _shutdown.Token); }
                finally { _tasks.TryRemove(execution.Id, out _); }
            });
        return execution;
    }

    /// <summary>A mesma sincronização, até o fim. É o que os testes usam para não depender de tempo.</summary>
    internal async Task<SyncExecution> RunToEndAsync(string projectId, string integrationId, CancellationToken ct)
    {
        var integration = integrations.Require(projectId, integrationId);
        var (execution, started) = Begin(integration);
        if (!started) throw IntegrationErrors.SyncRunning();
        return await RunAsync(integration, execution, ct);
    }

    public IReadOnlyList<SyncExecution> History(string projectId, string integrationId)
    {
        var integration = integrations.Require(projectId, integrationId);
        return Reconciled(integration).History;
    }

    public async Task ShutdownAsync()
    {
        await _shutdown.CancelAsync();
        try { await Task.WhenAll(_tasks.Values).WaitAsync(TimeSpan.FromSeconds(10)); }
        catch (Exception ex) when (ex is TimeoutException or OperationCanceledException) { }
    }

    /// <summary>Reserva a integração para uma execução nova; se outra já a reservou, devolve a que está em andamento.</summary>
    private (SyncExecution Execution, bool Started) Begin(ProjectIntegration integration)
    {
        var synced = Reconciled(integration);
        var execution = new SyncExecution(
            "sync-" + Guid.NewGuid().ToString("N")[..12], clock.UtcNow, FinishedAt: null, SyncStatus.Running,
            Incremental: synced.Checkpoint is not null, Pages: 0, Fetched: 0, Created: 0, Updated: 0, Unchanged: 0, Error: null);
        if (!activity.TryBegin(integration.Id, execution.Id))
            return (synced.History.FirstOrDefault(e => e.Status == SyncStatus.Running) ?? throw IntegrationErrors.SyncRunning(), false);

        try
        {
            synced.Record(execution);
            data.Write(integration.ProjectId, integration.Id, synced);
        }
        catch
        {
            activity.End(integration.Id);
            throw;
        }
        return (execution, true);
    }

    private async Task<SyncExecution> RunAsync(ProjectIntegration integration, SyncExecution execution, CancellationToken ct)
    {
        var synced = data.Read(integration.ProjectId, integration.Id);
        var seq = 0;
        Publish(integration, execution, StartedEvent, ++seq);
        try
        {
            var connection = connections.Require(integration.ConnectionId);
            var access = connections.AccessFor(connection);
            var connector = connectors.Require(integration.Provider);
            var cursor = new SyncCursor(synced.Checkpoint, Page: null);

            while (true)
            {
                var page = await connector.FetchPageAsync(access, integration.Scope, cursor, ct);
                execution = Apply(integration.Scope, page, synced, execution);
                data.Write(integration.ProjectId, integration.Id, synced);
                Publish(integration, execution, ProgressEvent, ++seq);

                if (page.Next is null)
                {
                    execution = execution with { Status = SyncStatus.Succeeded };
                    break;
                }
                if (execution.Pages >= MaxPages)
                {
                    execution = execution with { Status = SyncStatus.Partial };
                    break;
                }
                cursor = cursor with { Page = page.Next };
            }
        }
        catch (EngineException ex)
        {
            execution = execution with { Status = SyncStatus.Failed, Error = new IntegrationError(ex.Code, ex.Message, clock.UtcNow) };
        }
        catch (OperationCanceledException)
        {
            execution = execution with
            {
                Status = SyncStatus.Failed, Error = new IntegrationError("SYNC_CANCELLED", "A sincronização foi interrompida antes de terminar.", clock.UtcNow)
            };
        }
        catch (Exception ex)
        {
            // O texto de uma exceção inesperada pode carregar dado do provedor: só o tipo fica registrado.
            execution = execution with
            {
                Status = SyncStatus.Failed, Error = new IntegrationError("SYNC_FAILED", $"Falha interna na sincronização ({ex.GetType().Name}).", clock.UtcNow)
            };
        }

        try
        {
            execution = execution with { FinishedAt = clock.UtcNow };
            synced.Record(execution);
            data.Write(integration.ProjectId, integration.Id, synced);
        }
        finally
        {
            // Mesmo que o disco falhe ao gravar o desfecho, a integração não fica presa como "sincronizando".
            activity.End(integration.Id);
        }
        connections.RecordSync(integration.ConnectionId, execution.FinishedAt!.Value, execution.Error);
        Audit(integration, execution);
        // Por último: quem ouve o fim da sincronização já encontra a conexão e a trilha atualizadas.
        NotifyFinished(integration, execution);
        Publish(integration, execution, FinishedEvent, ++seq);
        return execution;
    }

    private void NotifyFinished(ProjectIntegration integration, SyncExecution execution)
    {
        foreach (var handler in (Finished?.GetInvocationList() ?? []).Cast<Action<ProjectIntegration, SyncExecution>>())
        {
            try
            {
                handler(integration, execution);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Um ouvinte com defeito não pode transformar uma sincronização gravada em falha: o tipo fica na trilha.
                audit.Record("integration.sync_listener_failed", new JsonObject { ["integrationId"] = integration.Id, ["error"] = ex.GetType().Name });
            }
        }
    }

    /// <summary>
    /// Avisa quem acompanha, sem esperar que perguntem. Só contagens e estado: nenhum item, título ou credencial sai por aqui.
    /// Um aviso que falha (a saída do motor fechou, por exemplo) nunca derruba a sincronização que ele descreve.
    /// </summary>
    private void Publish(ProjectIntegration integration, SyncExecution execution, string type, int seq)
    {
        if (emit is null) return;
        try
        {
            emit(execution.Id, seq, type, new JsonObject
            {
                ["projectId"] = integration.ProjectId,
                ["integrationId"] = integration.Id,
                ["status"] = IntegrationJson.Node(execution.Status),
                ["incremental"] = execution.Incremental,
                ["pages"] = execution.Pages,
                ["fetched"] = execution.Fetched,
                ["created"] = execution.Created,
                ["updated"] = execution.Updated,
                ["unchanged"] = execution.Unchanged,
                ["error"] = execution.Error?.Code
            }, clock.UtcNow);
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or InvalidOperationException)
        {
            // A sincronização já foi gravada; o aviso é só cortesia.
        }
    }

    /// <summary>Grava uma página. O que o conector devolver fora do recorte é descartado aqui também: o recorte não depende do conector.</summary>
    private SyncExecution Apply(DataScope scope, SyncPage page, SyncedData synced, SyncExecution execution)
    {
        var now = clock.UtcNow;
        int created = 0, updated = 0, unchanged = 0;
        foreach (var item in page.Items.Where(item => scope.DataKinds.Contains(item.Kind)))
        {
            switch (synced.Upsert(item, now))
            {
                case UpsertOutcome.Created: created++; break;
                case UpsertOutcome.Updated: updated++; break;
                default: unchanged++; break;
            }
            if (synced.Checkpoint is null || item.UpdatedAt > synced.Checkpoint) synced.Checkpoint = item.UpdatedAt;
        }

        execution = execution with
        {
            Pages = execution.Pages + 1,
            Fetched = execution.Fetched + created + updated + unchanged,
            Created = execution.Created + created,
            Updated = execution.Updated + updated,
            Unchanged = execution.Unchanged + unchanged
        };
        synced.Record(execution);
        return execution;
    }

    /// <summary>
    /// Uma execução que ficou "em andamento" sem ninguém a rodando foi cortada por uma queda do motor: vira falha, e o ponto
    /// de retomada já gravado continua valendo para a próxima.
    /// </summary>
    private SyncedData Reconciled(ProjectIntegration integration)
    {
        var synced = data.Read(integration.ProjectId, integration.Id);
        var running = activity.RunningSync(integration.Id);
        var stale = synced.History.Where(e => e.Status == SyncStatus.Running && e.Id != running).ToList();
        if (stale.Count == 0) return synced;

        foreach (var execution in stale)
            synced.Record(execution with
            {
                Status = SyncStatus.Failed,
                FinishedAt = clock.UtcNow,
                Error = new IntegrationError("SYNC_INTERRUPTED", "O aplicativo foi encerrado durante a sincronização.", clock.UtcNow)
            });
        data.Write(integration.ProjectId, integration.Id, synced);
        return synced;
    }

    private void Audit(ProjectIntegration integration, SyncExecution execution) =>
        audit.Record("integration.synced", new JsonObject
        {
            ["projectId"] = integration.ProjectId,
            ["integrationId"] = integration.Id,
            ["syncId"] = execution.Id,
            ["status"] = IntegrationJson.Node(execution.Status),
            ["fetched"] = execution.Fetched,
            ["error"] = execution.Error?.Code
        });
}
