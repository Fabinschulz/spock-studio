using System.Collections.Concurrent;
using System.Text.Json;
using Mariliz.Application.Execution;
using Mariliz.Application.Integrations;
using Mariliz.Application.Management;
using Mariliz.Core.Text;

namespace Mariliz.Infrastructure.Integrations;

/// <summary>
/// Os registros de risco de um projeto, num arquivo dentro da pasta de dados dele. O identificador do projeto vira nome de
/// pasta, então só passa o que tem a forma gerada pelo motor. Um arquivo corrompido vai para `.corrupt-*` e o histórico recomeça; um travado falha em vez de virar "sem histórico".
/// </summary>
public sealed class FileRiskHistoryStore(string dataRoot) : IRiskHistoryStore
{
    private readonly ConcurrentDictionary<string, object> _gates = new(StringComparer.Ordinal);

    public IReadOnlyList<RiskSnapshot> Load(string projectId)
    {
        var path = PathOf(projectId);
        lock (Gate(path))
        {
            return AtomicFile.ReadJson<List<RiskSnapshot>>(path, IntegrationJson.Options) ?? [];
        }
    }

    public void Save(string projectId, IReadOnlyList<RiskSnapshot> snapshots)
    {
        var path = PathOf(projectId);
        lock (Gate(path)) AtomicFile.Write(path, JsonSerializer.Serialize(snapshots, IntegrationJson.Options));
    }

    private string PathOf(string projectId) =>
        Ids.Is(projectId, "p-")
            ? Path.Combine(dataRoot, "projects", projectId, "risk-history.json")
            : throw new EngineException("INVALID_PARAMS", "Identificador fora do formato esperado.");

    private object Gate(string path) => _gates.GetOrAdd(path, _ => new object());
}
