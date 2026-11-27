using System.Text.Json.Nodes;
using Mariliz.Application.Audit;
using Mariliz.Application.Boot;
using Mariliz.Application.Integrations;
using Mariliz.Application.Workspace;

namespace Mariliz.Application.Mcp;

// BD-3: o Universal Boot para a IDE, só leitura e no nível 0. O texto do pedido vem de um agente e só serve para escolher uma capacidade
// do catálogo; o que altera algo volta como rota de tela, nunca executa. Cada chamada vai para a auditoria sem o texto do pedido.
// O JSON segue contracts/schemas/mcp/spock_boot.output.schema.json.
public sealed class BootTool(Func<WorkspaceAuthorizer> workspaces, Func<WorkspaceAuthorizer, string, BootService> boot, string workingDirectory,
    IReadOnlySet<string> scope, IAuditLog? audit = null)
{
    public const string Name = "spock_boot";
    public const string ModeRequest = "request";
    public const string ModeProject = "project";
    private const int MaxProject = 200;
    private const int MaxCapabilityId = 80;
    private const int MaxSource = 40;
    private static readonly string[] Arguments = ["project", "request", "capabilityId", "mode"];

    public static readonly McpToolInfo Info = new(
        Name, "Universal Boot do projeto (só leitura)",
        "Pergunta ao Spock o que ele sabe do projeto, ou inicia a visão geral dele, pelo mesmo caminho do Início do aplicativo. Só leitura (nível 0): " +
        "nunca analisa nem altera o projeto (só o registro do Boot e a auditoria ficam na pasta de dados do Spock), e o que altera algo não executa, " +
        "volta como a tela do Spock Studio onde a pessoa aprova. " +
        "Use 'request' (até 2000 caracteres, só para escolher uma capacidade do catálogo; não é comando) e/ou 'capabilityId', ou mode 'project' " +
        "para o Project Boot. Responde o que foi feito, consultado, o que falta e o próximo passo; um pedido ambíguo volta com as opções " +
        "para você repetir com capabilityId. Leituras disponíveis aqui: " + string.Join(", ", McpBootDispatcher.Capabilities) + ".",
        new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject
            {
                ["project"] = new JsonObject { ["type"] = "string", ["description"] = "Nome ou id do projeto, como em list_projects. Sem ele, vale o projeto da pasta atual." },
                ["request"] = new JsonObject
                {
                    ["type"] = "string", ["maxLength"] = BootService.MaxText,
                    ["description"] = "O que você quer saber ou fazer, em texto livre. Ex.: 'quais riscos o projeto tem?'."
                },
                ["capabilityId"] = new JsonObject
                {
                    ["type"] = "string", ["maxLength"] = MaxCapabilityId,
                    ["description"] = "A capacidade do catálogo, quando você já sabe qual (os ids vêm nas opções de um pedido ambíguo). Dispensa a interpretação do texto."
                },
                ["mode"] = new JsonObject
                {
                    ["type"] = "string", ["enum"] = new JsonArray(ModeRequest, ModeProject),
                    ["description"] = $"'{ModeRequest}' (padrão) responde um pedido; '{ModeProject}' é o Project Boot: contexto, Harness, marcos e o que falta, sem pedido."
                }
            },
            ["additionalProperties"] = false
        },
        OutputSchema: BootOutputSchema.Build());

    public IReadOnlyList<McpTool> Tools() => [McpTool.Structured(Info, RunAsync)];

    private async Task<McpReply> RunAsync(McpCall call)
    {
        // Um argumento que a ferramenta não tem (autonomy, por exemplo) é recusado em vez de ignorado: o agente não pode achar que pediu mais.
        if (call.Arguments.Any(pair => !Arguments.Contains(pair.Key, StringComparer.Ordinal)))
            throw new McpToolException("Argumento desconhecido. spock_boot aceita project, request, capabilityId e mode; o nível é sempre 0 (só leitura) e não é um parâmetro.");

        var mode = McpArguments.Text(call.Arguments, "mode", 20) ?? ModeRequest;
        if (mode is not (ModeRequest or ModeProject)) throw new McpToolException($"'mode' é '{ModeRequest}' ou '{ModeProject}'.");
        var request = McpArguments.Text(call.Arguments, "request", BootService.MaxText);
        var capabilityId = McpArguments.Text(call.Arguments, "capabilityId", MaxCapabilityId);
        if (mode == ModeProject && (request is not null || capabilityId is not null))
            throw new McpToolException($"O modo '{ModeProject}' não recebe 'request' nem 'capabilityId'. Use o modo '{ModeRequest}' para um pedido.");
        if (mode == ModeRequest && request is null && capabilityId is null)
            throw new McpToolException("Escreva o que você quer saber em 'request', ou informe 'capabilityId'.");
        if (capabilityId is not null && CapabilityCatalog.Find(capabilityId) is null)
            throw new McpToolException("'capabilityId' desconhecido. Descreva o pedido em 'request' e use os ids das opções que o Boot devolver.");

        var authorizer = workspaces();
        var project = new McpProjectResolver(authorizer, workingDirectory, scope).Resolve(McpArguments.Text(call.Arguments, "project", MaxProject));
        var service = boot(authorizer, project.ProjectId);
        var source = call.Client is { Length: <= MaxSource } client ? client : null;

        // Sempre no nível 0: a ferramenta não tem parâmetro de autonomia, e o Project Boot de um projeto sem perfil diz que falta em vez de analisar.
        var record = mode == ModeProject
            ? await service.StartProjectAsync(project.ProjectId, AutonomyLevel.Observation, "mcp", source, call.Ct)
            : await service.StartAsync(new BootStartRequest(project.ProjectId, request, null, capabilityId, AutonomyLevel.Observation, "mcp", source), call.Ct);

        Audit(project.ProjectId, mode, record, call.Client);
        return new McpReply(BootBriefing.Render(record), new JsonObject { ["boot"] = IntegrationJson.Node(record) });
    }

    // O pedido não entra: só o modo, as capacidades que o plano usou e como terminou.
    private void Audit(string projectId, string mode, BootRecord record, string? client)
    {
        if (audit is null) return;
        var detail = new JsonObject
        {
            ["projectId"] = projectId,
            ["tool"] = Name,
            ["mode"] = mode,
            ["bootId"] = record.Id,
            ["state"] = record.State,
            ["outcome"] = record.Result?.Outcome,
            ["capabilities"] = new JsonArray((record.Plan?.Capabilities ?? []).Select(id => (JsonNode)JsonValue.Create(id)!).ToArray())
        };
        if (client is not null) detail["client"] = client;
        audit.Record(AuditActions.IdeBoot, detail);
    }
}
