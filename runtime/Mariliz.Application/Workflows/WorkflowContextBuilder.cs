using System.Text.Json.Nodes;
using Mariliz.Application.Discovery;
using Mariliz.Application.Execution;
using Mariliz.Application.Ports;
using Mariliz.Application.Workspace;

namespace Mariliz.Application.Workflows;

/// <summary>
/// O contexto que o motor entrega ao runtime: stacks do projeto (descoberta) e fontes permitidas para o índice, mais a
/// pasta de dados onde a inteligência guarda índice e checkpoints. Sem projeto (<see cref="WithoutProject"/>), o
/// runtime recebe só "contexto indisponível" e não consegue pausar para aprovação.
/// </summary>
public sealed class WorkflowContextBuilder
{
    private readonly WorkspaceAuthorizer? _workspace;
    private readonly IAiRuntime? _runtime;
    private readonly IProjectFilesFactory? _files;
    private readonly ProjectDiscoverer? _discoverer;

    public WorkflowContextBuilder(
        WorkspaceAuthorizer workspace, IAiRuntime runtime, IProjectFilesFactory files, IProcessRunner runner, string dataRoot)
    {
        _workspace = workspace;
        _runtime = runtime;
        _files = files;
        _discoverer = new ProjectDiscoverer(runner);
        DataRoot = dataRoot;
    }

    private WorkflowContextBuilder()
    {
    }

    /// <summary>Sem descoberta, sem índice e sem memória de execução.</summary>
    public static WorkflowContextBuilder WithoutProject { get; } = new();

    /// <summary>Pasta de dados da inteligência (índice e checkpoints); vazia sem projeto.</summary>
    public string DataRoot { get; } = "";

    public async Task<JsonObject> BuildAsync(string projectId, CancellationToken ct)
    {
        var context = await StacksAsync(projectId, ct);
        await AttachKnowledgeAsync(projectId, context, ct);
        if (DataRoot.Length > 0) context["executionRoot"] = DataRoot;
        return context;
    }

    private async Task<JsonObject> StacksAsync(string projectId, CancellationToken ct)
    {
        if (_discoverer is null || _files is null || _workspace is null || !_workspace.TryGetRoot(projectId, out var root))
            return Unavailable();

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
            return Unavailable();
        }
    }

    private async Task AttachKnowledgeAsync(string projectId, JsonObject context, CancellationToken ct)
    {
        if (_files is null || _runtime is null || _workspace is null || DataRoot.Length == 0) return;
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
        foreach (var source in KnowledgeSources.Select(_files.Open(root), indexed))
        {
            var item = new JsonObject { ["path"] = source.Path, ["hash"] = source.Hash };
            if (source.Text is not null) item["text"] = source.Text;
            sources.Add(item);
        }
        context["knowledgeRoot"] = DataRoot;
        context["sources"] = sources;
    }

    private static JsonObject Unavailable() => new() { ["available"] = false, ["stacks"] = new JsonArray() };
}
