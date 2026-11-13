using System.Text.Json.Nodes;

namespace Mariliz.Application.Ports;

public interface IEvalRecords
{
    void Save(string projectId, string executionId, JsonObject record);
}
