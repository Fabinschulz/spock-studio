using System.Text.Json.Nodes;
using Mariliz.Application.Discovery;
using Mariliz.Application.Execution;
using Mariliz.Application.Integrations;
using Mariliz.Application.Management;
using Mariliz.Application.Ports;
using Mariliz.Application.Workspace;

namespace Mariliz.Application.Workflows;

/// <summary>O contexto que o motor entrega ao runtime de IA para um projeto.</summary>
public interface IWorkflowContext
{
    /// <summary>Pasta de dados da inteligência (índice e checkpoints); vazia sem projeto.</summary>
    string DataRoot { get; }

    Task<JsonObject> BuildAsync(string projectId, CancellationToken ct);
}

/// <summary>Sem projeto: o runtime recebe só "contexto indisponível" e não consegue pausar para aprovação.</summary>
internal sealed class EmptyWorkflowContext : IWorkflowContext
{
    public string DataRoot => "";

    public Task<JsonObject> BuildAsync(string projectId, CancellationToken ct) => Task.FromResult(Unavailable());

    public static JsonObject Unavailable() => new() { ["available"] = false, ["stacks"] = new JsonArray() };
}

/// <summary>
/// O contexto que o motor entrega ao runtime: stacks do projeto (descoberta) e fontes permitidas para o índice, mais a
/// pasta de dados onde a inteligência guarda índice e checkpoints. Sem projeto, use <see cref="WithoutProject"/>.
/// </summary>
public sealed class WorkflowContextBuilder : IWorkflowContext
{
    private readonly WorkspaceAuthorizer _workspace;
    private readonly IAiRuntime _runtime;
    private readonly IProjectFilesFactory _files;
    private readonly ProjectDiscoverer _discoverer;
    private readonly ExternalKnowledge? _external;
    private readonly DecisionMemory? _memory;

    /// <param name="external">Os itens sincronizados que o projeto autorizou como contexto. Sem isto, o índice só recebe arquivos do projeto.</param>
    /// <param name="memory">As decisões gerenciais registradas no projeto, como memória do índice.</param>
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

    /// <summary>Sem descoberta, sem índice e sem memória de execução.</summary>
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
