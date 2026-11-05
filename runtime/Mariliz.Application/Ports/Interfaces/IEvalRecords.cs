using System.Text.Json.Nodes;

namespace Mariliz.Application.Ports;

/// <summary>Onde o motor guarda o registro das avaliações por modelo de cada execução (<c>engineering.eval.record.v1</c>).</summary>
public interface IEvalRecords
{
    void Save(string projectId, string executionId, JsonObject record);
}
