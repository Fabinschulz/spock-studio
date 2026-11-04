using System.Text.Json.Nodes;
using Mariliz.Application.Execution;
using Mariliz.Application.Protocol;

namespace Mariliz.Application.Workflows;

/// <summary>O pedido entra só com os campos do contrato; o objetivo é obrigatório e limitado.</summary>
internal static class WorkflowInput
{
    public const int MaxGoal = 4000;
    private static readonly HashSet<string> Fields = new(StringComparer.Ordinal) { "goal", "impact", "requiresApproval" };
    private static readonly string[] Flags = ["impact", "requiresApproval"];

    public static JsonObject Parse(JsonObject input)
    {
        if (input.Any(pair => !Fields.Contains(pair.Key)))
            throw new EngineException(ProtocolErrorCodes.InvalidParams, "'input' só aceita goal, impact e requiresApproval.");
        var goal = input.Text("goal").Trim();
        if (goal.Length is 0 or > MaxGoal)
            throw new EngineException(ProtocolErrorCodes.InvalidParams, $"Descreva o pedido em até {MaxGoal} caracteres.");

        var request = new JsonObject { ["goal"] = goal };
        foreach (var flag in Flags)
        {
            if (input[flag] is null) continue;
            if (input[flag] is not JsonValue item || !item.TryGetValue<bool>(out var on))
                throw new EngineException(ProtocolErrorCodes.InvalidParams, $"'{flag}' precisa ser verdadeiro ou falso.");
            request[flag] = on;
        }
        return request;
    }
}
