using System.Text.Json.Nodes;
using Mariliz.Application.Boot;

namespace Mariliz.Application.Mcp;

// O schema de saída de spock_boot: o mesmo registro que boot.start devolve ao protocolo, dentro de "boot". Segue
// contracts/schemas/mcp/spock_boot.output.schema.json; um teste confere que este é o do contrato e que as fixtures do Boot o respeitam.
public static class BootOutputSchema
{
    private static JsonObject Text(string? description = null) => With(new JsonObject { ["type"] = "string" }, description);

    private static JsonObject NullableText(string? description = null) => With(new JsonObject { ["type"] = new JsonArray("string", "null") }, description);

    private static JsonObject Time() => new() { ["type"] = "string", ["format"] = "date-time" };

    private static JsonObject NullableTime() => new() { ["type"] = new JsonArray("string", "null"), ["format"] = "date-time" };

    private static JsonObject Whole(string? description = null) => With(new JsonObject { ["type"] = "integer" }, description);

    private static JsonObject Strings() => new() { ["type"] = "array", ["items"] = new JsonObject { ["type"] = "string" } };

    private static JsonObject Enum(params string[] values) => new() { ["type"] = "string", ["enum"] = new JsonArray(values.Select(value => (JsonNode)value).ToArray()) };

    private static JsonObject With(JsonObject schema, string? description)
    {
        if (description is not null) schema["description"] = description;
        return schema;
    }

    private static JsonObject Object(string[] required, JsonObject properties, string? description = null) => With(new JsonObject
    {
        ["type"] = "object", ["required"] = new JsonArray(required.Select(name => (JsonNode)name).ToArray()), ["properties"] = properties, ["additionalProperties"] = false
    }, description);

    private static JsonObject Nullable(JsonObject schema)
    {
        schema["type"] = new JsonArray("object", "null");
        return schema;
    }

    private static JsonObject List(JsonObject item) => new() { ["type"] = "array", ["items"] = item };

    private static readonly string[] Operations =
        ["read", "query", "analysis", "preparation", "configuration", "internalChange", "externalChange", "sensitive", "destructive"];

    private static readonly string[] Levels = ["observation", "preparation", "assisted", "governed", "extended"];

    public static JsonObject Build()
    {
        var intent = Nullable(Object(["resolution", "capabilityId", "source", "domain", "candidates", "question", "objective"], new JsonObject
        {
            ["resolution"] = Enum("resolved", "needsClarification", "noMatch"),
            ["capabilityId"] = NullableText("A capacidade do catálogo que o pedido escolheu."),
            ["source"] = Text("Como foi escolhida: pelo texto ou direto pelo capabilityId."),
            ["domain"] = NullableText(),
            ["candidates"] = List(Object(["capabilityId", "score", "confidence", "matched"], new JsonObject
            {
                ["capabilityId"] = Text(),
                ["score"] = Whole(),
                ["confidence"] = new JsonObject { ["type"] = "number" },
                ["matched"] = Strings()
            })),
            ["question"] = NullableText("A pergunta de esclarecimento, quando o pedido cabe em mais de uma capacidade."),
            ["objective"] = Text()
        }));

        var plan = Nullable(Object(
            ["objective", "domain", "capabilities", "operation", "requiredLevel", "approvals", "risks", "successCriteria", "expectedResult"], new JsonObject
            {
                ["objective"] = Text(),
                ["domain"] = NullableText(),
                ["capabilities"] = Strings(),
                ["operation"] = Enum(Operations),
                ["requiredLevel"] = Enum(Levels),
                ["approvals"] = Strings(),
                ["risks"] = Strings(),
                ["successCriteria"] = Strings(),
                ["expectedResult"] = Text()
            }));

        var step = Object(
            ["id", "kind", "title", "capabilityId", "method", "dependsOn", "operation", "status", "startedAt", "finishedAt", "summary", "arguments", "argumentNotes", "evidence", "errorCode"],
            new JsonObject
            {
                ["id"] = Text(),
                ["kind"] = Enum(StepKinds.Check, StepKinds.Context, StepKinds.Capability, StepKinds.Handoff),
                ["title"] = Text(),
                ["capabilityId"] = NullableText(),
                ["method"] = NullableText(),
                ["dependsOn"] = Strings(),
                ["operation"] = Enum(Operations),
                ["status"] = Enum(StepStatuses.Pending, StepStatuses.Running, StepStatuses.Completed, StepStatuses.Skipped, StepStatuses.Failed, StepStatuses.Blocked),
                ["startedAt"] = NullableTime(),
                ["finishedAt"] = NullableTime(),
                ["summary"] = Text("O que o passo leu do motor; nunca texto escrito por modelo."),
                ["arguments"] = new JsonObject { ["type"] = "object", ["additionalProperties"] = new JsonObject { ["type"] = "integer" } },
                ["argumentNotes"] = Strings(),
                ["evidence"] = Strings(),
                ["errorCode"] = NullableText()
            });

        var checkpoint = Object(["at", "state", "stepId", "note"], new JsonObject
        {
            ["at"] = Time(), ["state"] = Text(), ["stepId"] = NullableText(), ["note"] = Text()
        });

        var result = Nullable(Object(
            ["outcome", "headline", "done", "consulted", "changes", "validations", "problems", "pending", "nextStep", "route", "routeLabel", "alternatives"],
            new JsonObject
            {
                ["outcome"] = Enum(BootOutcomes.Answered, BootOutcomes.Prepared, BootOutcomes.Handoff, BootOutcomes.NeedsClarification, BootOutcomes.NeedsInput,
                    BootOutcomes.Unsupported, BootOutcomes.Failed),
                ["headline"] = Text(),
                ["done"] = Strings(),
                ["consulted"] = Strings(),
                ["changes"] = Strings(),
                ["validations"] = Strings(),
                ["problems"] = Strings(),
                ["pending"] = Strings(),
                ["nextStep"] = NullableText(),
                ["route"] = NullableText("A tela do Spock Studio onde a pessoa continua e aprova; o Boot não a abre."),
                ["routeLabel"] = NullableText(),
                ["alternatives"] = List(Object(["capabilityId", "name", "reason"], new JsonObject
                {
                    ["capabilityId"] = Text("Passe em capabilityId para escolher esta."), ["name"] = Text(), ["reason"] = Text()
                }))
            }));

        var boot = Object(
            ["id", "version", "mode", "origin", "source", "projectId", "stableProjectId", "state", "autonomy", "request", "hint", "intent", "plan", "steps",
                "checkpoints", "result", "contextVersion", "recoversBootId", "recoveredByBootId", "createdAt", "updatedAt", "finishedAt", "errorCode"],
            new JsonObject
            {
                ["id"] = new JsonObject { ["type"] = "string", ["pattern"] = "^boot-[0-9a-f]{12}$" },
                ["version"] = Whole("Sobe a cada gravação do registro."),
                ["mode"] = Enum("request", "project", "work", "recovery", "autonomous"),
                ["origin"] = Text("Quem iniciou; pelo MCP é sempre mcp."),
                ["source"] = NullableText("O nome que a IDE deu de si, sem conferência."),
                ["projectId"] = NullableText("Id da cópia de trabalho (p-)."),
                ["stableProjectId"] = NullableText(),
                ["state"] = Enum(BootStates.Created, BootStates.Interpreting, BootStates.AwaitingClarification, BootStates.Preparing, BootStates.Planned, BootStates.Executing,
                    BootStates.Completed, BootStates.Failed, BootStates.Cancelled, BootStates.Interrupted),
                ["autonomy"] = Enum(Levels),
                ["request"] = NullableText("O texto do pedido, devolvido como foi enviado; só serve para escolher uma capacidade."),
                ["hint"] = NullableText(),
                ["intent"] = intent,
                ["plan"] = plan,
                ["steps"] = List(step),
                ["checkpoints"] = List(checkpoint),
                ["result"] = result,
                ["contextVersion"] = NullableText(),
                ["recoversBootId"] = NullableText(),
                ["recoveredByBootId"] = NullableText(),
                ["createdAt"] = Time(),
                ["updatedAt"] = Time(),
                ["finishedAt"] = NullableTime(),
                ["errorCode"] = NullableText()
            });

        return Object(["boot"], new JsonObject { ["boot"] = boot });
    }
}
