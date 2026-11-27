namespace Mariliz.Application.Boot;

/// <summary>
/// Quantos registros de Boot ficam em cada escopo (um projeto, ou o Boot sem projeto). Passando do limite, saem os mais antigos
/// que já terminaram; um Boot ainda vivo ou interrompido e não recuperado nunca sai, porque é o que a recuperação precisa.
/// </summary>
public static class BootRetention
{
    public const int Keep = 200;

    // Poda em lote: contar arquivos é barato, e reler a pasta inteira a cada Boot que termina não é. Só passa disso é que se lê.
    public const int Slack = 20;

    public static bool CanDelete(BootRecord record) =>
        record.State is BootStates.Completed or BootStates.Failed or BootStates.Cancelled or BootStates.AwaitingClarification
        || record is { State: BootStates.Interrupted, RecoveredByBootId: not null };

    public static IReadOnlyList<BootRecord> Expired(IEnumerable<BootRecord> records, int keep = Keep) =>
        [.. records.OrderByDescending(record => record.CreatedAt).ThenByDescending(record => record.Id, StringComparer.Ordinal).Skip(keep).Where(CanDelete)];

    // Ao terminar um Boot: só olha o escopo dele, e só quando a pasta passou do limite com folga.
    public static void PruneIfNeeded(IBootStore store, string? projectId)
    {
        try
        {
            if (store.Count(projectId) <= Keep + Slack) return;
            foreach (var record in Expired(store.List(projectId))) store.Delete(projectId, record.Id);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Arquivo travado agora: a poda é um favor e o Boot já terminou. A próxima tentativa recolhe o que sobrou.
        }
    }

    // Ao subir o motor: todos os escopos, até o limite exato. Devolve quantos registros saíram.
    public static int Apply(IBootStore store)
    {
        var removed = 0;
        try
        {
            foreach (var scope in store.ListAll().GroupBy(record => record.ProjectId))
                foreach (var record in Expired(scope))
                    if (store.Delete(scope.Key, record.Id)) removed++;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Idem: fica para a próxima subida.
        }
        return removed;
    }
}
