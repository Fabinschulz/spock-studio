using System.Text.Json;
using Mariliz.Application.Execution;
using Mariliz.Application.Integrations;
using Mariliz.Application.Management;
using Mariliz.Core.Text;

namespace Mariliz.Infrastructure.Integrations;

/// <summary>
/// As decisões gerenciais de um projeto, um arquivo por decisão em <c>projects/{projeto}/decisions</c>. Os identificadores viram
/// nome de pasta e de arquivo, então só passam os que têm a forma gerada pelo motor. Um arquivo ilegível fica de fora da lista.
/// </summary>
public sealed class FileDecisionStore(string dataRoot) : IDecisionStore
{
    private readonly object _gate = new();

    public IReadOnlyList<ManagementDecision> List(string projectId)
    {
        var dir = DirectoryOf(projectId);
        lock (_gate)
        {
            if (!Directory.Exists(dir)) return [];
            return Directory.EnumerateFiles(dir, "dec-*.json").Select(Read).OfType<ManagementDecision>().ToList();
        }
    }

    public ManagementDecision? Get(string projectId, string decisionId)
    {
        var path = PathOf(projectId, decisionId);
        lock (_gate) return File.Exists(path) ? Read(path) : null;
    }

    public void Save(ManagementDecision decision)
    {
        var path = PathOf(decision.ProjectId, decision.Id);
        lock (_gate) AtomicFile.Write(path, JsonSerializer.Serialize(decision, IntegrationJson.Options));
    }

    private static ManagementDecision? Read(string path)
    {
        try
        {
            return JsonSerializer.Deserialize<ManagementDecision>(File.ReadAllText(path), IntegrationJson.Options);
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            return null;
        }
    }

    private string DirectoryOf(string projectId) =>
        Ids.Is(projectId, "p-")
            ? Path.Combine(dataRoot, "projects", projectId, "decisions")
            : throw new EngineException("INVALID_PARAMS", "Identificador fora do formato esperado.");

    private string PathOf(string projectId, string decisionId) =>
        Ids.Is(decisionId, "dec-")
            ? Path.Combine(DirectoryOf(projectId), decisionId + ".json")
            : throw new EngineException("INVALID_PARAMS", "Identificador fora do formato esperado.");
}
