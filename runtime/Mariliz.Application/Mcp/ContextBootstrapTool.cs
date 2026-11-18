using System.Text.Json.Nodes;
using Mariliz.Application.Context;
using Mariliz.Application.Workspace;

namespace Mariliz.Application.Mcp;

// F3: o pacote de abertura de sessão do Context Engine (F2) para a IDE, em texto e em JSON. O JSON segue
// contracts/schemas/mcp/spock_context_bootstrap.output.schema.json; um teste confere que o schema daqui é o mesmo.
public static class ContextBootstrapTool
{
    public const string Name = "spock_context_bootstrap";

    private static JsonObject Text(string description) => new() { ["type"] = "string", ["description"] = description };

    private static JsonObject Enum(params string[] values) => new() { ["type"] = "string", ["enum"] = new JsonArray(values.Select(v => (JsonNode)v).ToArray()) };

    public static JsonObject OutputSchema() => new()
    {
        ["type"] = "object",
        ["required"] = new JsonArray("projectId", "intent", "contextVersion", "budgetTokens", "usedTokens", "composedAt", "items", "omitted", "limitations"),
        ["properties"] = new JsonObject
        {
            ["projectId"] = Text("Id da cópia de trabalho (p-)."),
            ["stableId"] = new JsonObject { ["type"] = new JsonArray("string", "null"), ["description"] = "Id estável do projeto (spk-), do spock.yaml (ou do mariliz.yaml de um projeto anterior à troca de nome)." },
            ["intent"] = Enum(ContextIntents.Bootstrap),
            ["contextVersion"] = Text("Muda quando algo declarado ou aprovado muda; não a cada sincronização."),
            ["budgetTokens"] = new JsonObject { ["type"] = "integer" },
            ["usedTokens"] = new JsonObject { ["type"] = "integer" },
            ["composedAt"] = new JsonObject { ["type"] = "string", ["format"] = "date-time" },
            ["items"] = new JsonObject
            {
                ["type"] = "array",
                ["items"] = new JsonObject
                {
                    ["type"] = "object",
                    ["required"] = new JsonArray("section", "text", "source", "ref", "classification", "freshness"),
                    ["properties"] = new JsonObject
                    {
                        ["section"] = Enum("identity", "policies", "connections", "harness", "warnings"),
                        ["text"] = Text("O item como a IDE o lê."),
                        ["source"] = Text("De onde veio: spock, a declaração (spock.yaml ou mariliz.yaml), o lock (spock.lock ou mariliz.lock), o provedor da integração ou cloud:<provedor>."),
                        ["ref"] = Text("O que identifica o item na origem."),
                        ["classification"] = Enum(ContextClassifications.Discovered, ContextClassifications.Declared, ContextClassifications.Approved,
                            ContextClassifications.Integration, ContextClassifications.Calculated, ContextClassifications.Inferred),
                        ["observedAt"] = new JsonObject { ["type"] = new JsonArray("string", "null"), ["format"] = "date-time" },
                        ["freshness"] = Enum(ContextFreshness.Fresh, ContextFreshness.Stale, ContextFreshness.Unknown)
                    },
                    ["additionalProperties"] = false
                }
            },
            ["omitted"] = new JsonObject
            {
                ["type"] = "array",
                ["items"] = new JsonObject
                {
                    ["type"] = "object",
                    ["required"] = new JsonArray("section", "ref", "reason"),
                    ["properties"] = new JsonObject { ["section"] = Text("Seção do item."), ["ref"] = Text("Item que ficou de fora."), ["reason"] = Text("Por quê.") },
                    ["additionalProperties"] = false
                }
            },
            ["limitations"] = new JsonObject { ["type"] = "array", ["items"] = new JsonObject { ["type"] = "string" } }
        },
        ["additionalProperties"] = false
    };

    public static readonly McpToolInfo Info = new(
        Name, "Contexto de abertura do projeto",
        "Comece por aqui numa sessão nova: identidade do projeto (id estável e cópia), o que cada integração e conexão cloud libera, " +
        "há quanto tempo cada uma foi lida, divergências do Harness e avisos. Cada item diz de onde veio, como foi obtido e se está atual. " +
        "Compacto (até 1.500 tokens por padrão) e sem conteúdo das fontes externas.",
        new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject
            {
                ["project"] = new JsonObject
                {
                    ["type"] = "string",
                    ["description"] = "Nome ou id do projeto, como em list_projects. Sem ele, vale o projeto da pasta atual."
                },
                ["budget_tokens"] = new JsonObject
                {
                    ["type"] = "integer", ["minimum"] = ContextEngine.MinBudget, ["maximum"] = ContextEngine.MaxBudget,
                    ["description"] = $"Orçamento do pacote em tokens (padrão {ContextEngine.DefaultBootstrapBudget})."
                }
            },
            ["additionalProperties"] = false
        },
        OutputSchema: OutputSchema());

    public static McpReply Reply(ContextPackage package) => new(package.Text, Structured(package));

    public static JsonObject Structured(ContextPackage package) => new()
    {
        ["projectId"] = package.ProjectId,
        ["stableId"] = package.StableId,
        ["intent"] = package.Intent,
        ["contextVersion"] = package.ContextVersion,
        ["budgetTokens"] = package.BudgetTokens,
        ["usedTokens"] = package.UsedTokens,
        ["composedAt"] = package.ComposedAt.ToString("O"),
        ["items"] = new JsonArray(package.Items.Select(item => (JsonNode)new JsonObject
        {
            ["section"] = item.Section,
            ["text"] = item.Text,
            ["source"] = item.Source,
            ["ref"] = item.Ref,
            ["classification"] = item.Classification,
            ["observedAt"] = item.ObservedAt?.ToString("O"),
            ["freshness"] = item.Freshness
        }).ToArray()),
        ["omitted"] = new JsonArray(package.Omitted.Select(omission => (JsonNode)new JsonObject
        {
            ["section"] = omission.Section, ["ref"] = omission.Ref, ["reason"] = omission.Reason
        }).ToArray()),
        ["limitations"] = new JsonArray(package.Limitations.Select(limitation => (JsonNode)limitation).ToArray())
    };
}
