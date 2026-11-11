using System.Text.Json;
using Mariliz.Core.Text;

namespace Mariliz.Application.Integrations;

/// <summary>
/// O que uma integração de projeto já leu: os itens, o ponto de retomada e o histórico de sincronizações. Um arquivo por
/// integração, dentro da pasta de dados do projeto dela: dados de projetos diferentes nunca compartilham arquivo.
/// </summary>
public sealed class SyncedData
{
    public const int MaxHistory = 50;

    /// <summary>A maior data de atualização já gravada. A próxima sincronização pede ao provedor só o que mudou desde então.</summary>
    public DateTimeOffset? Checkpoint { get; set; }

    public Dictionary<string, StoredWorkItem> Items { get; set; } = new(StringComparer.Ordinal);

    /// <summary>Mais recente primeiro.</summary>
    public List<SyncExecution> History { get; set; } = [];

    /// <summary>
    /// Grava um item pela identidade no provedor (tipo e identificador). Repetir a mesma leitura não duplica nem altera nada:
    /// o item só muda quando o conteúdo mudou.
    /// </summary>
    public UpsertOutcome Upsert(WorkItem item, DateTimeOffset at)
    {
        var key = KeyOf(item);
        var hash = Hashing.Sha256(JsonSerializer.Serialize(item, IntegrationJson.Options));
        if (!Items.TryGetValue(key, out var stored))
        {
            Items[key] = new StoredWorkItem(item, hash, at, at);
            return UpsertOutcome.Created;
        }

        var changed = stored.Hash != hash;
        Items[key] = changed ? new StoredWorkItem(item, hash, stored.FirstSyncedAt, at) : stored with { SyncedAt = at };
        return changed ? UpsertOutcome.Updated : UpsertOutcome.Unchanged;
    }

    /// <summary>Apaga o que ficou fora do recorte. Devolve quantos itens saíram.</summary>
    public int RemoveKindsOutside(IReadOnlyList<string> kinds)
    {
        var outside = Items.Where(pair => !kinds.Contains(pair.Value.Item.Kind)).Select(pair => pair.Key).ToList();
        foreach (var key in outside) Items.Remove(key);
        return outside.Count;
    }

    /// <summary>Insere ou substitui uma execução pelo id e mantém só as <see cref="MaxHistory"/> mais recentes.</summary>
    public void Record(SyncExecution execution)
    {
        var index = History.FindIndex(e => e.Id == execution.Id);
        if (index >= 0) History[index] = execution;
        else History.Insert(0, execution);
        if (History.Count > MaxHistory) History.RemoveRange(MaxHistory, History.Count - MaxHistory);
    }

    private static string KeyOf(WorkItem item) => $"{item.Kind}:{item.ExternalId}";
}
