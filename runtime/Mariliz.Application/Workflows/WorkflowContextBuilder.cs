using System.Text.Json.Nodes;
using Mariliz.Application.Discovery;
using Mariliz.Application.Execution;
using Mariliz.Application.Integrations;
using Mariliz.Application.Management;
using Mariliz.Application.Ports;
using Mariliz.Application.Workspace;

namespace Mariliz.Application.Workflows;

public interface IWorkflowContext
{
    string DataRoot { get; }

    Task<JsonObject> BuildAsync(string projectId, CancellationToken ct);
}

// Sem projeto, o runtime recebe só "contexto indisponível" e não consegue pausar para aprovação.
internal sealed class EmptyWorkflowContext : IWorkflowContext
{
    public string DataRoot => "";

    public Task<JsonObject> BuildAsync(string projectId, CancellationToken ct) => Task.FromResult(Unavailable());

    public static JsonObject Unavailable() => new() { ["available"] = false, ["stacks"] = new JsonArray() };
}

public sealed class WorkflowContextBuilder : IWorkflowContext
{
    private readonly WorkspaceAuthorizer _workspace;
    private readonly IAiRuntime _runtime;
    private readonly IProjectFilesFactory _files;
    private readonly ProjectDiscoverer _discoverer;
    private readonly ExternalKnowledge? _external;
    private readonly DecisionMemory? _memory;

    public WorkflowContextBuilder(
        WorkspaceAuthorizer workspace, IAiRuntime runtime, IProjectFilesFactory files, IProcessRunner runner, string dataRoot,
        ExternalKnowledge? external = null, DecisionMemory? memory = null)
    {
        _external = external;
        _memory = memory;
        _workspace = workspace;
        _runtime = runtime;
        _files = files;
        _discoverer = new ProjectDiscoverer(runner);
        DataRoot = dataRoot;
    }

    public static IWorkflowContext WithoutProject { get; } = new EmptyWorkflowContext();

    public string DataRoot { get; }

    public async Task<JsonObject> BuildAsync(string projectId, CancellationToken ct)
    {
        var context = await StacksAsync(projectId, ct);
        await AttachKnowledgeAsync(projectId, context, ct);
        if (DataRoot.Length > 0) context["executionRoot"] = DataRoot;
        return context;
    }

    private async Task<JsonObject> StacksAsync(string projectId, CancellationToken ct)
    {
        if (!_workspace.TryGetRoot(projectId, out var root))
            return EmptyWorkflowContext.Unavailable();

        try
        {
            var report = await _discoverer.DiscoverAsync(projectId, root, _files.Open(root), ct);
            return new JsonObject
            {
                ["available"] = true,
                ["stacks"] = new JsonArray(report.Stacks.Select(stack => stack.Id).Distinct().Take(8).Select(id => (JsonNode)JsonValue.Create(id)!).ToArray())
            };
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception)
        {
            // Descoberta é um extra: falhar nela não impede o pedido, só tira as stacks do contexto.
            return EmptyWorkflowContext.Unavailable();
        }
    }

    private async Task AttachKnowledgeAsync(string projectId, JsonObject context, CancellationToken ct)
    {
        if (DataRoot.Length == 0) return;
        if (!_workspace.TryGetRoot(projectId, out var root)) return;

        IReadOnlyDictionary<string, string> indexed;
        try
        {
            Directory.CreateDirectory(DataRoot);
            indexed = await _runtime.IndexedHashesAsync(projectId, DataRoot, ct);
        }
        catch (Exception ex) when (ex is EngineException or HttpRequestException or IOException)
        {
            indexed = new Dictionary<string, string>();
        }

        var sources = new JsonArray();
        foreach (var source in KnowledgeSources.Select(_files.Open(root), indexed, _external?.Documents(projectId), _memory?.Documents(projectId)))
        {
            var item = new JsonObject { ["path"] = source.Path, ["hash"] = source.Hash };
            if (source.Text is not null) item["text"] = source.Text;
            sources.Add(item);
        }
        context["knowledgeRoot"] = DataRoot;
        context["sources"] = sources;
    }
}
