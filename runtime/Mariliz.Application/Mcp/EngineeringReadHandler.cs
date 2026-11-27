using System.Text.Json.Nodes;
using Mariliz.Application.Analysis;
using Mariliz.Application.Compatibility;
using Mariliz.Application.Engineering;
using Mariliz.Application.Execution;
using Mariliz.Application.Integrations;
using Mariliz.Application.Ports;
using Mariliz.Application.Protocol;
using Mariliz.Application.Workspace;

namespace Mariliz.Application.Mcp;

// As leituras de engenharia que o servidor MCP monta para o Boot. Responde com o mesmo JSON dos tratadores do protocolo, sem trazer
// o serviço do perfil nem o de configuração (que analisam e aplicam): aqui só se lê o que já está gravado ou o que os arquivos dizem.
internal sealed class EngineeringReadHandler(
    WorkspaceAuthorizer workspace,
    IProjectFilesFactory files,
    IClock clock,
    ITechnologyProfileStore profiles,
    HarnessStatusService harness,
    HarnessRevisionService revisions,
    McpInventoryService inventory) : IRequestHandler
{
    public IReadOnlyList<string> Methods { get; } =
    [
        ProtocolMethods.EngineeringProfile, ProtocolMethods.EngineeringHarness, ProtocolMethods.EngineeringCatalog, ProtocolMethods.EngineeringPolicy,
        ProtocolMethods.EngineeringMcp, ProtocolMethods.EngineeringRevisions, ProtocolMethods.ProjectArchitecture
    ];

    public Task<JsonObject> HandleAsync(string method, RequestParams p, CancellationToken ct) => Task.FromResult(method switch
    {
        ProtocolMethods.EngineeringProfile => Profile(p.Str("projectId")),
        ProtocolMethods.EngineeringHarness => IntegrationJson.Node(harness.Inspect(p.Str("projectId")))!.AsObject(),
        ProtocolMethods.EngineeringCatalog => IntegrationJson.Node(new HarnessCatalogService(harness).Read(p.OptionalStr("projectId")))!.AsObject(),
        ProtocolMethods.EngineeringPolicy => IntegrationJson.Node(AgentPolicyService.Read())!.AsObject(),
        ProtocolMethods.EngineeringMcp => IntegrationJson.Node(inventory.Read(p.Str("projectId")))!.AsObject(),
        ProtocolMethods.EngineeringRevisions => IntegrationJson.Node(revisions.Read(p.Str("projectId")))!.AsObject(),
        ProtocolMethods.ProjectArchitecture => new JsonObject { ["architecture"] = JournalJson.Node(Architecture(p.Str("projectId"))) },
        _ => throw new InvalidOperationException($"Método sem tratador: {method}.")
    });

    private JsonObject Profile(string projectId)
    {
        if (!workspace.IsAuthorized(projectId)) throw EngineErrors.ProjectNotAuthorized();
        var profile = profiles.Read(projectId);
        return new JsonObject
        {
            ["profile"] = IntegrationJson.Node(profile),
            ["compatibility"] = IntegrationJson.Node(profile is null ? null : CompatibilityAssessor.Assess(profile.Modules, ReleaseCatalog.Embedded, clock.UtcNow))
        };
    }

    // A avaliação gravada no projeto é conferida contra a mesma análise, como na tela.
    private ProjectArchitecture Architecture(string projectId)
    {
        var projectFiles = files.Open(workspace.RequireRoot(projectId));
        var report = ArchitectureAnalyzer.Analyze(projectId, projectFiles, clock.UtcNow);
        return new ProjectArchitecture(report, ArchitectureAssessments.Read(projectFiles, report), ArchitectureAssessments.PathIn(projectFiles));
    }
}
