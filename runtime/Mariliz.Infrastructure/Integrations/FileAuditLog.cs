using System.Text;
using System.Text.Json.Nodes;
using Mariliz.Application.Execution;
using Mariliz.Application.Integrations;
using Mariliz.Application.Ports;

namespace Mariliz.Infrastructure.Integrations;

/// <summary>Uma linha JSON por operação em <c>{dataRoot}/audit.ndjson</c>. O arquivo só cresce; nada nele é reescrito.</summary>
public sealed class FileAuditLog(string dataRoot, IClock clock) : IAuditLog
{
    private readonly object _gate = new();

    public void Record(string action, JsonObject detail)
    {
        var line = new JsonObject { ["at"] = EventStamp.Format(clock.UtcNow), ["action"] = action, ["detail"] = detail.DeepClone() };
        lock (_gate)
        {
            try
            {
                Directory.CreateDirectory(dataRoot);
                File.AppendAllText(Path.Combine(dataRoot, "audit.ndjson"), line.ToJsonString() + "\n", new UTF8Encoding(false));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // A trilha não pode derrubar a operação que ela registra; quem hospeda o motor fica sabendo pelo stderr.
                Console.Error.WriteLine($"Não foi possível gravar a auditoria ({ex.GetType().Name}).");
            }
        }
    }
}
