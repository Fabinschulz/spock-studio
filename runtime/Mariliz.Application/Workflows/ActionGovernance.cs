using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Mariliz.Application.Execution;
using Mariliz.Application.Models;
using Mariliz.Application.Ports;
using Mariliz.Application.Protocol;
using Mariliz.Core.Policy;
using Mariliz.Core.Text;

namespace Mariliz.Application.Workflows;

// O agente só diz a intenção; o motor aplica a política, escolhe o alvo, escreve o conteúdo e liga a aprovação ao hash das ações. Nada executa sem aprovação.
internal static partial class ActionGovernance
{
    public const string FileWrite = "file.write";

    // A única intenção que o runtime pode propor hoje. Vira uma ação file.write.
    public const string RecordDecision = "record-decision";

    private static string DecisionSummary(HarnessNaming naming) => $"Registrar a decisão de arquitetura (áreas, riscos e evidências) em {naming.DecisionsFolder}";
    private static readonly HashSet<string> IntentKeys = new(StringComparer.Ordinal) { "id", "intent", "evidence" };

    // Só o motor conhece este caminho.
    public static string DecisionTargetFor(string executionId, HarnessNaming naming) => $"{naming.DecisionsFolder}/{executionId}-impacto.md";

    // Aprovar é a decisão sobre as ações propostas: sem pausa não há onde aprovar, então file.write só em execuções pausáveis que pedem impacto.
    public static IReadOnlyList<string> AllowedKinds(JsonObject input) =>
        input["requiresApproval"]?.GetValue<bool>() == true && input["impact"]?.GetValue<bool>() == true ? [FileWrite] : [];

    [GeneratedRegex(@"^\.(?:spock|mariliz)/decisions/ex-[0-9a-f]{12}-impacto\.md$")]
    private static partial Regex DecisionTarget();

    [GeneratedRegex("^act-[0-9a-z-]{1,32}$")]
    private static partial Regex ActionId();

    public static bool IsActionId(string value) => ActionId().IsMatch(value);

    public static bool IsDecisionTarget(string target) => DecisionTarget().IsMatch(target);

    // O que fugir da política, do formato ou da recuperação desta execução é descartado, nunca corrigido. Intenção com alvo, resumo ou conteúdo próprios também: isso é decisão do motor.
    // O impacto é do modelo só se o proxy viu a chamada da arquitetura e o runtime não voltou às regras depois dela.
    public static bool ImpactFromModel(WorkflowAck ack, IReadOnlyList<ModelCallRecord> calls) =>
        calls.Any(call => call.Purpose == "architecture") && ack.Limitations.All(limitation => limitation.Code != "IMPACT_MODEL_FALLBACK");

    public static List<JournalAction> Review(WorkflowAck ack, IReadOnlyList<string> allowed, bool impactFromModel = false, HarnessNaming? naming = null)
    {
        var accepted = new List<JournalAction>();
        if (allowed.Count == 0 || ack.Output["impact"] is not JsonObject impact) return accepted;
        var known = KnownChunks(ack.Output);

        foreach (var intent in ack.Actions)
        {
            if (!intent.Select(pair => pair.Key).All(IntentKeys.Contains)) continue;
            var id = intent.Text("id");
            if (intent.Text("intent") != RecordDecision || !allowed.Contains(FileWrite)) continue;
            if (!IsActionId(id) || accepted.Any(a => a.Id == id)) continue;
            var target = DecisionTargetFor(ack.ExecutionId, naming ?? HarnessNaming.Spock);
            if (!IsDecisionTarget(target)) continue;
            if (!TryEvidence(intent["evidence"], known, out var evidence)) continue;

            var content = DecisionRecordRenderer.Render(ack, impact, evidence, known, impactFromModel);
            accepted.Add(new JournalAction
            {
                Id = id,
                Kind = FileWrite,
                Summary = DecisionSummary(naming ?? HarnessNaming.Spock),
                Target = target,
                Evidence = evidence,
                Content = content,
                ContentSha256 = Hashing.Sha256(content)
            });
        }
        return accepted;
    }

    public static void ApplyDecision(RunJournal journal, IReadOnlyList<string> actionIds)
    {
        foreach (var action in journal.Actions)
            action.Status = actionIds.Contains(action.Id) ? JournalAction.Approved : JournalAction.Skipped;
    }

    // Uma retomada repetida nunca grava duas vezes.
    public static void ExecuteApproved(RunJournal journal, string projectId, IActionExecutor? executor, Action saved)
    {
        foreach (var action in journal.Actions.Where(a => a.Status == JournalAction.Approved))
        {
            var outcome = Run(executor, projectId, action);
            action.Status = outcome.Status;
            // O arquivo criado é uma alteração da execução como qualquer outra: aparece na lista e volta por harness.revert.
            if (outcome is { Status: JournalAction.Executed, CreatedPath: { } created })
            {
                journal.Ops.Add(new JournalOp { Path = created, Op = OpKinds.Create, Source = OpSources.Action, AfterHash = action.ContentSha256, Status = OpStatus.Applied });
                journal.ChangesApplied = true;
            }
            saved();
        }
    }

    public static void SkipPending(RunJournal journal)
    {
        foreach (var action in journal.Actions.Where(a => a.Status is JournalAction.Approved or JournalAction.Proposed))
            action.Status = JournalAction.Skipped;
    }

    public static JsonArray ForEvent(IEnumerable<JournalAction> actions) =>
        new(actions.Select(a => (JsonNode)new JsonObject { ["id"] = a.Id, ["kind"] = a.Kind, ["summary"] = a.Summary, ["target"] = a.Target }).ToArray());

    public static JsonArray ForResult(IEnumerable<JournalAction> actions) =>
        new(actions.Select(a => (JsonNode)new JsonObject { ["id"] = a.Id, ["kind"] = a.Kind, ["summary"] = a.Summary, ["target"] = a.Target, ["status"] = a.Status }).ToArray());

    private static ActionOutcome Run(IActionExecutor? executor, string projectId, JournalAction action)
    {
        if (executor is null) return new ActionOutcome(JournalAction.Failed);
        try
        {
            return executor.Execute(projectId, action);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or EngineException or ArgumentException)
        {
            return new ActionOutcome(JournalAction.Failed);
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
