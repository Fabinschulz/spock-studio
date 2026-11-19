using System.Text;
using System.Text.Json.Nodes;
using Mariliz.Application.Compatibility;

namespace Mariliz.Application.Mcp;

// MV-3: o agente chama antes de concluir a tarefa. Compara as versões que os arquivos declaram agora com o perfil que o Spock
// analisou (a restrição que o agente recebeu) e aponta combinação incompatível. O JSON segue
// contracts/schemas/mcp/spock_version_check.output.schema.json; um teste confere.
public static class VersionCheckTool
{
    public const string Name = "spock_version_check";

    private static JsonObject Text(string description) => new() { ["type"] = "string", ["description"] = description };

    private static JsonObject NullableText(string description) => new() { ["type"] = new JsonArray("string", "null"), ["description"] = description };

    private static JsonObject Strings(string description) => new()
    {
        ["type"] = "array", ["items"] = new JsonObject { ["type"] = "string" }, ["description"] = description
    };

    public static JsonObject OutputSchema() => new()
    {
        ["type"] = "object",
        ["required"] = new JsonArray("projectId", "baselineAt", "clean", "changes", "incompatible", "guidance"),
        ["properties"] = new JsonObject
        {
            ["projectId"] = Text("Id da cópia de trabalho (p-)."),
            ["baselineAt"] = new JsonObject { ["type"] = new JsonArray("string", "null"), ["format"] = "date-time", ["description"] = "Quando o perfil comparado foi analisado; nulo sem perfil." },
            ["clean"] = new JsonObject { ["type"] = "boolean", ["description"] = "Nenhuma versão mudou e nenhuma combinação está incompatível." },
            ["changes"] = new JsonObject
            {
                ["type"] = "array",
                ["items"] = new JsonObject
                {
                    ["type"] = "object",
                    ["required"] = new JsonArray("root", "technologyId", "name", "origin", "kind", "from", "to", "evidence"),
                    ["properties"] = new JsonObject
                    {
                        ["root"] = Text("Pasta do projeto."),
                        ["technologyId"] = Text("Tecnologia."),
                        ["name"] = Text("Nome legível."),
                        ["origin"] = new JsonObject { ["type"] = "string", ["enum"] = new JsonArray(ProjectModules.Stack, ProjectModules.Runtime, ProjectModules.Framework) },
                        ["kind"] = new JsonObject { ["type"] = "string", ["enum"] = new JsonArray(VersionCheck.Changed, VersionCheck.Added, VersionCheck.Removed) },
                        ["from"] = NullableText("Versão no perfil."),
                        ["to"] = NullableText("Versão nos arquivos agora."),
                        ["evidence"] = Strings("Arquivo e valor lido.")
                    },
                    ["additionalProperties"] = false
                }
            },
            ["incompatible"] = new JsonObject
            {
                ["type"] = "array",
                ["items"] = new JsonObject
                {
                    ["type"] = "object",
                    ["required"] = new JsonArray("root", "technologyId", "name", "version", "reason"),
                    ["properties"] = new JsonObject
                    {
                        ["root"] = Text("Pasta do projeto."),
                        ["technologyId"] = Text("Tecnologia."),
                        ["name"] = Text("Nome legível."),
                        ["version"] = NullableText("Versão declarada."),
                        ["reason"] = Text("Por que a combinação não é suportada.")
                    },
                    ["additionalProperties"] = false
                }
            },
            ["guidance"] = Text("O que fazer com o resultado.")
        },
        ["additionalProperties"] = false
    };

    public static readonly McpToolInfo Info = new(
        Name, "Conferência de versões depois da tarefa",
        "Chame antes de concluir uma tarefa que mexeu em código ou configuração. Compara as versões que os arquivos do projeto declaram " +
        "agora (TargetFramework, global.json, .nvmrc, engines, requires-python, dependências) com as do perfil que o Spock analisou e " +
        "aponta combinação incompatível (por exemplo, Vite que exige Node mais novo que o do projeto). Só lê: não muda o projeto nem o perfil.",
        new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject
            {
                ["project"] = new JsonObject { ["type"] = "string", ["description"] = "Nome ou id do projeto, como em list_projects. Sem ele, vale o projeto da pasta atual." }
            },
            ["additionalProperties"] = false
        },
        OutputSchema: OutputSchema());

    public static McpReply Reply(VersionCheckReport report) => new(Render(report), Structured(report));

    public static JsonObject Structured(VersionCheckReport report) => new()
    {
        ["projectId"] = report.ProjectId,
        ["baselineAt"] = report.BaselineAt?.ToString("O"),
        ["clean"] = report.Clean,
        ["changes"] = new JsonArray(report.Changes.Select(change => (JsonNode)new JsonObject
        {
            ["root"] = change.Root,
            ["technologyId"] = change.TechnologyId,
            ["name"] = change.Name,
            ["origin"] = change.Origin,
            ["kind"] = change.Kind,
            ["from"] = change.From,
            ["to"] = change.To,
            ["evidence"] = new JsonArray(change.Evidence.Select(item => (JsonNode)item).ToArray())
        }).ToArray()),
        ["incompatible"] = new JsonArray(report.Incompatible.Select(module => (JsonNode)new JsonObject
        {
            ["root"] = module.Root,
            ["technologyId"] = module.TechnologyId,
            ["name"] = module.Name,
            ["version"] = module.Version,
            ["reason"] = module.Reason
        }).ToArray()),
        ["guidance"] = report.Guidance
    };

    private static string Render(VersionCheckReport report)
    {
        var text = new StringBuilder();
        text.AppendLine("# Conferência de versões");
        text.AppendLine();
        text.AppendLine(report.Guidance);
        if (report.Changes.Count > 0)
        {
            text.AppendLine();
            text.AppendLine("## Versões que mudaram");
            foreach (var change in report.Changes)
                text.AppendLine(change.Kind switch
                {
                    VersionCheck.Added => $"- {change.Root}: {change.Name} {change.To} entrou.",
                    VersionCheck.Removed => $"- {change.Root}: {change.Name} {change.From} saiu.",
                    _ => $"- {change.Root}: {change.Name} {change.From} -> {change.To}."
                });
        }
        if (report.Incompatible.Count > 0)
        {
            text.AppendLine();
            text.AppendLine("## Combinações incompatíveis");
            foreach (var module in report.Incompatible) text.AppendLine($"- {module.Root}: {module.Reason}");
        }
        return text.ToString().TrimEnd();
    }
}
