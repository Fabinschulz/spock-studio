using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Mariliz.Application.Execution;
using Mariliz.Application.Ports;
using Mariliz.Application.Protocol;
using Mariliz.Core.Text;

namespace Mariliz.Application.Workflows;

/// <summary>
/// A governança das ações que os agentes propõem. O motor aplica a política (quais tipos, quais alvos), escreve ele mesmo o
/// conteúdo, liga a aprovação ao hash das ações e confere cada retomada contra ela. O agente propõe; nada executa sem aprovação.
/// </summary>
internal static partial class ActionGovernance
{
    public const string FileWrite = "file.write";
    private static readonly HashSet<string> ProposalKeys = new(StringComparer.Ordinal) { "id", "kind", "summary", "target", "evidence", "requiresApproval" };

    /// <summary>
    /// A política do motor para esta execução: <c>file.write</c> só em execuções que a pessoa pediu para pausar e que pedem
    /// impacto. Aprovar é a decisão sobre as ações propostas, então sem pausa não há onde aprovar.
    /// </summary>
    public static IReadOnlyList<string> AllowedKinds(JsonObject input) =>
        input["requiresApproval"]?.GetValue<bool>() == true && input["impact"]?.GetValue<bool>() == true ? [FileWrite] : [];

    [GeneratedRegex(@"^\.mariliz/decisions/ex-[0-9a-f]{12}-impacto\.md$")]
    private static partial Regex DecisionTarget();

    [GeneratedRegex("^act-[0-9a-z-]{1,32}$")]
    private static partial Regex ActionId();

    public static bool IsActionId(string value) => ActionId().IsMatch(value);

    public static bool IsDecisionTarget(string target) => DecisionTarget().IsMatch(target);

    /// <summary>
    /// Transforma as propostas do runtime nas ações que o motor aceita. O que fugir da política, do formato ou da recuperação
    /// desta execução é descartado (nunca corrigido); <c>requiresApproval</c> do agente é ignorado: toda ação exige aprovação.
    /// </summary>
    public static List<JournalAction> Review(WorkflowAck ack, IReadOnlyList<string> allowed)
    {
        var accepted = new List<JournalAction>();
        if (allowed.Count == 0 || ack.Output["impact"] is not JsonObject impact) return accepted;
        var known = KnownChunks(ack.Output);

        foreach (var proposal in ack.Actions)
        {
            if (!proposal.Select(pair => pair.Key).All(ProposalKeys.Contains)) continue;
            var kind = proposal.Text("kind");
            var id = proposal.Text("id");
            var target = proposal.Text("target");
            var summary = proposal.Text("summary");
            if (kind != FileWrite || !allowed.Contains(kind) || !IsActionId(id) || accepted.Any(a => a.Id == id)) continue;
            if (target != $".mariliz/decisions/{ack.ExecutionId}-impacto.md" || !IsDecisionTarget(target)) continue;
            if (summary.Length is 0 or > 200) continue;
            if (!TryEvidence(proposal["evidence"], known, out var evidence)) continue;

            var content = DecisionRecordRenderer.Render(ack, impact, evidence, known);
            accepted.Add(new JournalAction
            {
                Id = id,
                Kind = kind,
                Summary = summary,
                Target = target,
                Evidence = evidence,
                Content = content,
                ContentSha256 = Hashing.Sha256(content)
            });
        }
        return accepted;
    }

    /// <summary>Marca o que foi aprovado; tudo o que não foi nomeado fica de fora.</summary>
    public static void ApplyDecision(RunJournal journal, IReadOnlyList<string> actionIds)
    {
        foreach (var action in journal.Actions)
            action.Status = actionIds.Contains(action.Id) ? JournalAction.Approved : JournalAction.Skipped;
    }

    /// <summary>Executa só o que está aprovado e ainda não foi executado: uma retomada repetida nunca grava duas vezes.</summary>
    public static void ExecuteApproved(RunJournal journal, string projectId, IActionExecutor? executor, Action saved)
    {
        foreach (var action in journal.Actions.Where(a => a.Status == JournalAction.Approved))
        {
            action.Status = Run(executor, projectId, action);
            saved();
        }
    }

    /// <summary>Quando a execução falha depois da aprovação, nada aprovado chega a executar.</summary>
    public static void SkipPending(RunJournal journal)
    {
        foreach (var action in journal.Actions.Where(a => a.Status is JournalAction.Approved or JournalAction.Proposed))
            action.Status = JournalAction.Skipped;
    }

    public static JsonArray ForEvent(IEnumerable<JournalAction> actions) =>
        new(actions.Select(a => (JsonNode)new JsonObject { ["id"] = a.Id, ["kind"] = a.Kind, ["summary"] = a.Summary, ["target"] = a.Target }).ToArray());

    public static JsonArray ForResult(IEnumerable<JournalAction> actions) =>
        new(actions.Select(a => (JsonNode)new JsonObject { ["id"] = a.Id, ["kind"] = a.Kind, ["summary"] = a.Summary, ["target"] = a.Target, ["status"] = a.Status }).ToArray());

    private static string Run(IActionExecutor? executor, string projectId, JournalAction action)
    {
        if (executor is null) return JournalAction.Failed;
        try
        {
            return executor.Execute(projectId, action).Status;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or EngineException or ArgumentException)
        {
            return JournalAction.Failed;
        }
    }

    private static Dictionary<string, string> KnownChunks(JsonObject output)
    {
        var known = new Dictionary<string, string>(StringComparer.Ordinal);
        if (output["evidence"] is not JsonArray items) return known;
        foreach (var item in items.OfType<JsonObject>())
            if (item.Text("chunkId") is { Length: > 0 } chunk && WorkflowResult.TryEvidence(item, out var safe))
                known[chunk] = safe.Text("filePath");
        return known;
    }

    private static bool TryEvidence(JsonNode? node, Dictionary<string, string> known, out List<string> evidence)
    {
        evidence = [];
        if (node is not JsonArray refs) return node is null;
        foreach (var item in refs)
        {
            if (item is not JsonValue value || !value.TryGetValue<string>(out var chunk) || !known.ContainsKey(chunk)) return false;
            if (!evidence.Contains(chunk)) evidence.Add(chunk);
        }
        return true;
    }
}
