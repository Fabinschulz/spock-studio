using System.Globalization;
using System.Text.Json.Nodes;
using Mariliz.Application.Execution;
using Mariliz.Application.Ports;
using Mariliz.Application.Protocol;
using Mariliz.Core.Policy;

namespace Mariliz.Application.Workflows;

/// <summary>
/// O que do runtime de IA chega à interface e ao diário. Nada passa por cópia direta: cada campo é conferido contra o
/// contrato e reconstruído, então texto livre, caminho absoluto ou segredo no trecho recuperado ficam de fora.
/// </summary>
internal static class WorkflowResult
{
    public static JsonObject From(WorkflowAck ack, JournalModelUsage? usage = null, JournalJudgment? judgment = null,
        IReadOnlyList<JournalAction>? actions = null)
    {
        var result = new JsonObject
        {
            ["kind"] = WorkflowService.Kind,
            ["workflowId"] = ack.WorkflowId,
            ["executionId"] = ack.ExecutionId,
            ["summary"] = ack.Output.Text("summary"),
            ["stacks"] = ack.Output["stacks"]?.DeepClone() ?? new JsonArray(),
            ["evidence"] = CopyEvidence(ack.Output),
            ["limitations"] = new JsonArray(ack.Limitations.Select(item => (JsonNode)new JsonObject
            {
                ["code"] = item.Code,
                ["message"] = item.Message
            }).ToArray())
        };
        if (TryImpact(ack.Output["impact"], out var impact)) result["impact"] = impact;
        // O runtime só diz que houve avaliação; a opinião do juiz sai do diário, já conferida contra o proxy.
        if (ack.Output["evaluation"] is JsonObject declared && declared.Text("verdict") == "deterministic")
            result["evaluation"] = JudgmentRecord.Evaluation(judgment);
        // As ações são as do diário (propostas, aprovadas e executadas pelo motor), nunca as que o runtime diz ter feito.
        if (actions is { Count: > 0 }) result["actions"] = ActionGovernance.ForResult(actions);
        // O uso vem do proxy do motor, não do que o runtime disse ter gasto.
        if (usage is not null)
            result["usage"] = new JsonObject { ["modelCalls"] = usage.Calls, ["inputTokens"] = usage.InputTokens, ["outputTokens"] = usage.OutputTokens };
        return result;
    }

    public static JsonObject Failure(string code, string message) => new()
    {
        ["kind"] = WorkflowService.Kind,
        ["error"] = new JsonObject { ["code"] = code, ["message"] = message }
    };

    /// <summary>Ids das etapas do plano, para a interface mostrar onde o grafo parou.</summary>
    public static JsonArray StepIds(JsonObject output)
    {
        var ids = new JsonArray();
        if (output["plan"]?["steps"] is not JsonArray steps) return ids;
        foreach (var step in steps)
        {
            if (step is JsonObject item && item.Text("id") is var id && IsStepId(id))
                ids.Add(id);
        }
        return ids;
    }

    /// <summary>Proveniência de um trecho recuperado: caminho relativo seguro, chunk, hash e pontuação.</summary>
    public static bool TryEvidence(JsonObject item, out JsonObject data)
    {
        data = new JsonObject();
        var path = item.Text("filePath");
        var chunkId = item.Text("chunkId");
        var hash = item.Text("hash");
        if (!SafeRelative(path) || chunkId.Length == 0 || hash.Length != 64) return false;
        if (item["score"] is not JsonValue score || !double.TryParse(score.ToJsonString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            return false;
        var documentId = item.Text("documentId");
        data["documentId"] = documentId.Length == 0 ? path : documentId;
        data["chunkId"] = chunkId;
        data["filePath"] = path;
        data["hash"] = hash;
        data["score"] = value;
        return SafeRelative(data["documentId"]!.GetValue<string>());
    }

    private static bool TryImpact(JsonNode? node, out JsonObject impact)
    {
        impact = new JsonObject();
        if (node is not JsonObject item || item.Text("schema") != "engineering.impact.v1") return false;
        if (item["areas"] is not JsonArray areas || item["risks"] is not JsonArray risks || item["evidence"] is not JsonArray refs) return false;
        if (!TryCopy(areas, text => text is "servico" or "interface" or "projeto", out var copyAreas) || copyAreas.Count == 0) return false;
        if (!TryCopy(risks, text => text is "sem-evidencia" or "sem-stack", out var copyRisks)) return false;
        if (!TryCopy(refs, SafeChunk, out var copyRefs)) return false;
        impact["schema"] = "engineering.impact.v1";
        impact["areas"] = copyAreas;
        impact["risks"] = copyRisks;
        impact["evidence"] = copyRefs;
        return true;
    }

    /// <summary>Copia uma lista de textos só se todos forem aceitos; um item fora do contrato invalida a lista.</summary>
    private static bool TryCopy(JsonArray source, Func<string, bool> accepted, out JsonArray copy)
    {
        copy = new JsonArray();
        foreach (var node in source)
        {
            if (node is not JsonValue value || !value.TryGetValue<string>(out var text) || !accepted(text)) return false;
            copy.Add(text);
        }
        return true;
    }

    private static JsonArray CopyEvidence(JsonObject output)
    {
        var copies = new JsonArray();
        if (output["evidence"] is not JsonArray evidence) return copies;
        foreach (var node in evidence)
        {
            if (node is not JsonObject item || !TryEvidence(item, out var data)) continue;
            var excerpt = SecretScanner.RedactLines(item.Text("excerpt"));
            data["excerpt"] = SecretScanner.ContainsSecret(excerpt) ? "" : excerpt;
            copies.Add(data);
        }
        return copies;
    }

    private static bool SafeChunk(string value) =>
        value.Length is > 0 and <= 240 && !value.Contains('\\') && !value.Contains("..");

    private static bool SafeRelative(string path) =>
        path.Length is > 0 and <= 240
        && !path.Contains('\\')
        && !path.StartsWith('/')
        && !path.Split('/').Contains("..");

    private static bool IsStepId(string id) =>
        id.Length is > 0 and <= 32 && id.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');
}
