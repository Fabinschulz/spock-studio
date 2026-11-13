using Mariliz.Application.Execution;
using Mariliz.Application.Integrations;
using Mariliz.Application.Management;
using Mariliz.Application.Ports;
using Mariliz.Application.Workflows;
using Mariliz.Application.Workspace;

namespace Mariliz.Application.Engineering;

// O índice só é consultado se o runtime de IA já estiver ligado: abrir a tela não sobe o processo Python.
public sealed class ProjectKnowledgeService(
    WorkspaceAuthorizer workspace,
    IProjectFilesFactory files,
    IAiRuntime runtime,
    string indexRoot,
    IClock clock,
    ExternalKnowledge? external = null,
    DecisionMemory? memory = null)
{
    // Os totais contam todos.
    public const int MaxListed = 300;
    public const int MaxExamples = 8;

    private static readonly IReadOnlyList<(KnowledgeSources.Exclusion Reason, string Id, string Label)> Reasons =
    [
        (KnowledgeSources.Exclusion.Secret, "secret", "Possível segredo no conteúdo"),
        (KnowledgeSources.Exclusion.Excluded, "excluded", "Excluído em mariliz.yaml (knowledge.exclude)"),
        (KnowledgeSources.Exclusion.NotIncluded, "notIncluded", "Fora do knowledge.include de mariliz.yaml"),
        (KnowledgeSources.Exclusion.Gitignored, "gitignored", "Ignorado pelo .gitignore"),
        (KnowledgeSources.Exclusion.Blocked, "blocked", "Binário, dependência, build ou arquivo sensível pelo nome"),
        (KnowledgeSources.Exclusion.Empty, "empty", "Vazio ou binário"),
        (KnowledgeSources.Exclusion.OverLimit, "overLimit", $"Além do limite de {KnowledgeSources.MaxFiles} arquivos")
    ];

    public async Task<ProjectKnowledge> InspectAsync(string projectId, CancellationToken ct)
    {
        if (!workspace.TryGetRoot(projectId, out var root)) throw EngineErrors.ProjectNotAuthorized();
        var project = files.Open(root);
        var (include, exclude) = KnowledgeSources.DeclaredRules(project);
        // Descarta o texto de cada arquivo logo depois de medir.
        var evaluated = KnowledgeSources.Evaluate(project)
            .Select(file => (file.Path, file.Reason, file.Hash, Chars: file.Text?.Length ?? 0))
            .ToList();
        var allowed = evaluated.Where(file => file.Reason == KnowledgeSources.Exclusion.None).ToList();

        var (indexState, indexed) = await ReadIndexAsync(projectId, ct);
        string State(string path, string? hash) =>
            indexed is null ? "unknown"
            : !indexed.TryGetValue(path, out var stored) ? "pending"
            : stored == hash ? "indexed" : "outdated";

        var listed = allowed.Take(MaxListed).Select(file => new KnowledgeFile(file.Path, file.Chars, State(file.Path, file.Hash))).ToList();

        var states = allowed.Select(file => State(file.Path, file.Hash)).ToList();
        // Os textos de fontes externas autorizadas também são fonte do índice: não contam como órfãos.
        var externalDocuments = external?.Documents(projectId) ?? [];
        // As decisões registradas também são fonte do índice: contam como memória, não como órfãs.
        var memoryDocuments = memory?.Documents(projectId) ?? [];
        var sourcePaths = allowed.Select(file => file.Path).Concat(externalDocuments.Select(document => document.Path)).Concat(memoryDocuments.Select(document => document.Path)).ToHashSet(StringComparer.Ordinal);
        var memoryStates = memoryDocuments.Select(document => State(document.Path, document.Hash)).ToList();
        var excluded = Reasons
            .Select(reason => (reason, matches: evaluated.Where(file => file.Reason == reason.Reason).Select(file => file.Path).ToList()))
            .Where(group => group.matches.Count > 0)
            .Select(group => new KnowledgeExclusion(group.reason.Id, group.reason.Label, group.matches.Count, group.matches.Take(MaxExamples).ToList()))
            .ToList();

        return new ProjectKnowledge(
            projectId,
            clock.UtcNow,
            include,
            exclude,
            allowed.Count,
            listed,
            excluded,
            indexState,
            states.Count(state => state == "indexed"),
            states.Count(state => state == "outdated"),
            states.Count(state => state == "pending"),
            indexed is null ? 0 : indexed.Keys.Count(path => !sourcePaths.Contains(path)),
            ExternalSources(projectId, externalDocuments, State),
            new KnowledgeMemory(memoryDocuments.Count, memoryStates.Count(item => item == "indexed"), memoryStates.Count(item => item == "pending"), memoryStates.Count(item => item == "outdated")));
    }

    private IReadOnlyList<KnowledgeExternalSource> ExternalSources(
        string projectId, IReadOnlyList<ExternalDocument> documents, Func<string, string?, string> state) =>
        (external?.Sources(projectId) ?? []).Select(source =>
        {
            var states = documents.Where(document => document.IntegrationId == source.IntegrationId).Select(document => state(document.Path, document.Hash)).ToList();
            return new KnowledgeExternalSource(
                source.IntegrationId, source.ProviderName, source.ResourceName, source.Authorized, source.DataKinds,
                source.Items, source.Documents, source.Withheld,
                states.Count(item => item == "indexed"), states.Count(item => item == "pending"), states.Count(item => item == "outdated"));
        }).ToList();

    private async Task<(string State, IReadOnlyDictionary<string, string>? Indexed)> ReadIndexAsync(string projectId, CancellationToken ct)
    {
        switch (runtime.Probe())
        {
            case "ready":
                try
                {
                    return ("read", await runtime.IndexedHashesAsync(projectId, indexRoot, ct));
                }
                catch (Exception ex) when (ex is EngineException or HttpRequestException or IOException)
                {
                    return ("failed", null);
                }
            case "unavailable":
                return ("runtimeUnavailable", null);
            default:
                return ("runtimeStopped", null);
        }
    }
}
