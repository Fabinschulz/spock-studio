using System.Text.Json.Nodes;
using Mariliz.Application.Audit;
using Mariliz.Application.Boot;
using Mariliz.Application.Engineering;
using Mariliz.Application.Execution;
using Mariliz.Application.Integrations;
using Mariliz.Application.Management;
using Mariliz.Application.Ports;
using Mariliz.Application.Protocol;
using Mariliz.Application.Workspace;

namespace Mariliz.Application.Mcp;

// O despacho do Boot dentro do servidor MCP. Não há sessão de protocolo aqui: um roteador pequeno, só com os tratadores de leitura,
// preso a um projeto e a uma lista fechada de métodos. O Boot já só executa o que a ficha direta diz e no nível 0; esta lista é a
// segunda trava: um método que não está nela falha mesmo que um tratador o conheça.
public sealed class McpBootDispatcher
{
    // Dado do projeto autorizado, só leitura. Ficam de fora o que é da máquina inteira (portfólio, conexões, modelos, contas de
    // nuvem), o que sobe processo ou runtime (agentes, conhecimento, infraestrutura) e o que depende de serviços que gravam
    // (decisões, relatório, linha do tempo, execuções): aparecem como indisponíveis, nunca como prontos.
    private static readonly string[] Mounted =
    [
        "engineering.project_overview", "engineering.harness_status", "engineering.harness_revisions", "engineering.catalog", "engineering.mcp_inventory",
        "standards.architecture", "standards.agent_policy",
        "management.tracking", "management.risks", "management.risk_history", "management.forecast", "management.milestones",
        "platform.audit"
    ];

    private readonly Dictionary<string, IRequestHandler> _routes = new(StringComparer.Ordinal);
    private readonly string _projectId;

    public McpBootDispatcher(string projectId, WorkspaceAuthorizer workspace, IProjectFilesFactory files, IClock clock, ITechnologyProfileStore profiles,
        IRunStore runs, IntegrationHub integrations, ManagementService management)
    {
        _projectId = projectId;
        IRequestHandler[] handlers =
        [
            new EngineeringReadHandler(workspace, files, clock, profiles, new HarnessStatusService(workspace, files, clock),
                new HarnessRevisionService(workspace, files, runs), new McpInventoryService(workspace, files)),
            new ManagementRequestHandler(management, assistant: null),
            new AuditRequestHandler(new ProjectAuditService(workspace, clock, runs, integrations.AuditReader, integrations))
        ];
        foreach (var handler in handlers)
            foreach (var method in handler.Methods.Where(ReadMethods.Contains)) _routes.Add(method, handler);
        RoutedMethods = new HashSet<string>(_routes.Keys.Concat(ScreenMethods()), StringComparer.Ordinal);
    }

    /// <summary>Os ids das capacidades que o Boot executa por MCP.</summary>
    public static IReadOnlyList<string> Capabilities => Mounted;

    /// <summary>Os métodos que o Boot pode executar por MCP: o de cada capacidade acima, sempre leitura direta.</summary>
    public static IReadOnlySet<string> ReadMethods { get; } = Methods();

    /// <summary>
    /// O que o Boot enxerga como "roteado". Além das leituras montadas, entram os métodos das capacidades que só abrem a tela (a tela é do
    /// aplicativo, não deste processo); nenhum deles executa aqui. Uma leitura direta que o MCP não monta continua indisponível.
    /// </summary>
    public IReadOnlySet<string> RoutedMethods { get; }

    public Task<JsonObject> DispatchAsync(string method, JsonObject parameters, CancellationToken ct)
    {
        if (!_routes.TryGetValue(method, out var handler)) throw new EngineException(ProtocolErrorCodes.MethodNotFound, "Método desconhecido.");
        // O servidor MCP é por projeto: nada lê de outro, nem por um registro adulterado.
        if (JsonRead.Text(parameters, "projectId") != _projectId) throw EngineErrors.ProjectNotAuthorized();
        return handler.HandleAsync(method, new RequestParams(parameters), ct);
    }

    private static HashSet<string> Methods()
    {
        var methods = new HashSet<string>(StringComparer.Ordinal);
        foreach (var id in Mounted)
        {
            var capability = CapabilityCatalog.Find(id) ?? throw new InvalidOperationException($"A capacidade {id} não está no catálogo.");
            if (capability.Mode != ExecutionMode.Direct || capability.Operation > OperationClass.Analysis)
                throw new InvalidOperationException($"{id} não é uma leitura direta: o Boot por MCP não a executa.");
            methods.Add(capability.Method!);
        }
        return methods;
    }

    private static HashSet<string> ScreenMethods()
    {
        var unmounted = CapabilityCatalog.All
            .Where(capability => capability.Mode == ExecutionMode.Direct && capability.Operation <= OperationClass.Analysis && !ReadMethods.Contains(capability.Method!))
            .Select(capability => capability.Method!)
            .ToHashSet(StringComparer.Ordinal);
        return CapabilityCatalog.All
            .Where(capability => capability.Mode != ExecutionMode.Direct || capability.Operation > OperationClass.Analysis)
            .SelectMany(capability => capability.Methods)
            .Where(method => !unmounted.Contains(method))
            .ToHashSet(StringComparer.Ordinal);
    }
}
