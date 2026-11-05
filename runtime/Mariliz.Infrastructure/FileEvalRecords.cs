using System.Text.Json.Nodes;
using Mariliz.Application.Ports;

namespace Mariliz.Infrastructure;

/// <summary>Um arquivo por execução em <c>&lt;dados&gt;/projects/&lt;id&gt;/evals/&lt;executionId&gt;.json</c>, fora do repositório do projeto.</summary>
public sealed class FileEvalRecords(string dataRoot) : IEvalRecords
{
    public void Save(string projectId, string executionId, JsonObject record)
    {
        var dir = Path.Combine(dataRoot, "projects", Safe(projectId), "evals");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, Safe(executionId) + ".json");
        var temp = path + ".tmp";
        File.WriteAllText(temp, record.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        File.Move(temp, path, overwrite: true);
    }

    private static string Safe(string id) =>
        id.Length > 0 && id.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_') ? id : throw new ArgumentException("Identificador inválido.");
}
