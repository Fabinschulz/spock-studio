using System.Text;
using Mariliz.Application.Execution;
using Mariliz.Core.Text;

namespace Mariliz.Application.Workflows;

internal static class ApprovalBinding
{
    public static string HashOf(IEnumerable<JournalAction> actions)
    {
        var canonical = new StringBuilder();
        foreach (var action in actions.OrderBy(a => a.Id, StringComparer.Ordinal))
            canonical.Append(action.Id).Append('\n').Append(action.Kind).Append('\n').Append(action.Target).Append('\n').Append(action.ContentSha256).Append('\n');
        return Hashing.Sha256(canonical.ToString());
    }

    // Aprovação de outra execução, de outra pausa ou de outras ações não coincide com este identificador.
    public static string ApprovalIdFor(string runId, string executionId, string interruptId, string actionsHash) =>
        "apr-" + Hashing.Sha256($"{runId}|{executionId}|{interruptId}|{actionsHash}")[..12];

    public static void EnsureDecision(RunJournal journal, string? approvalId, IReadOnlyList<string> actionIds)
    {
        var expectedHash = HashOf(journal.Actions);
        if (journal.ActionsHash != expectedHash
            || journal.ApprovalId != ApprovalIdFor(journal.RunId, journal.ExecutionId ?? "", journal.InterruptId ?? "", expectedHash))
            throw new EngineException("APPROVAL_HASH_MISMATCH", "As ações propostas mudaram depois de a aprovação ser aberta. Nada foi executado.");

        // Aprovar uma ação exige o identificador da aprovação. Continuar sem aprovar nenhuma não exige: é sempre o caminho seguro,
        // e um identificador apresentado, certo ou errado, é sempre conferido.
        if ((actionIds.Count > 0 || approvalId is not null)
            && !string.Equals(approvalId, journal.ApprovalId, StringComparison.Ordinal))
            throw new EngineException("APPROVAL_MISMATCH", "Esta aprovação não é a desta execução.");
        var proposed = journal.Actions.Select(a => a.Id).ToHashSet(StringComparer.Ordinal);
        if (actionIds.Any(id => !proposed.Contains(id)) || actionIds.Distinct().Count() != actionIds.Count)
            throw new EngineException("APPROVAL_ACTION_UNKNOWN", "A aprovação cita ações que não foram propostas.");
    }
}
