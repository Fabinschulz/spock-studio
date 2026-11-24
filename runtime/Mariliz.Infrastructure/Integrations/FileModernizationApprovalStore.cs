using System.Text.Json;
using Mariliz.Application.Analysis;
using Mariliz.Application.Execution;
using Mariliz.Application.Integrations;
using Mariliz.Core.Text;

namespace Mariliz.Infrastructure.Integrations;

public sealed class FileModernizationApprovalStore(string dataRoot) : IModernizationApprovalStore
{
    private readonly object _gate = new();

    public IReadOnlyList<ModernizationApproval> List(string projectId)
    {
        var path = PathOf(projectId);
        lock (_gate) return AtomicFile.ReadJson<List<ModernizationApproval>>(path, IntegrationJson.Options) ?? [];
    }

    public void Save(string projectId, ModernizationApproval approval)
    {
        var path = PathOf(projectId);
        lock (_gate)
        {
            var current = AtomicFile.ReadJson<List<ModernizationApproval>>(path, IntegrationJson.Options) ?? [];
            List<ModernizationApproval> next = [.. current.Where(item => item.StepId != approval.StepId), approval];
            AtomicFile.Write(path, JsonSerializer.Serialize(next, IntegrationJson.Options));
        }
    }

    private string PathOf(string projectId) =>
        Ids.Is(projectId, "p-")
            ? Path.Combine(dataRoot, "projects", projectId, "modernization-approvals.json")
            : throw new EngineException("INVALID_PARAMS", "Identificador fora do formato esperado.");
}
