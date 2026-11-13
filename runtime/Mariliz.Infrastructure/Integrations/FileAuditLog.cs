using System.Text;
using System.Text.Json.Nodes;
using Mariliz.Application.Execution;
using Mariliz.Application.Integrations;
using Mariliz.Application.Ports;

namespace Mariliz.Infrastructure.Integrations;

// O arquivo só cresce; nada nele é reescrito.
public sealed class FileAuditLog(string dataRoot, IClock clock) : IAuditLog, IAuditReader
{
    // A numeração é a do arquivo inteiro.
    public const int MaxLines = 100_000;

    private readonly object _gate = new();
    private string FilePath => Path.Combine(dataRoot, "audit.ndjson");

    public IReadOnlyList<AuditRecord> ReadAll()
    {
        lock (_gate)
        {
            if (!File.Exists(FilePath)) return [];
            var lines = File.ReadAllLines(FilePath, Encoding.UTF8);
            var first = Math.Max(0, lines.Length - MaxLines);
            var records = new List<AuditRecord>();
            for (var index = first; index < lines.Length; index++)
            {
                if (string.IsNullOrWhiteSpace(lines[index])) continue;
                try
                {
                    if (JsonNode.Parse(lines[index]) is not JsonObject line
                        || line["action"]?.GetValue<string>() is not { } action
                        || !DateTimeOffset.TryParse(line["at"]?.GetValue<string>(), System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind, out var at))
                        continue;
                    records.Add(new AuditRecord(index + 1, at, action, line["detail"] as JsonObject ?? new JsonObject()));
                }
                catch (Exception ex) when (ex is System.Text.Json.JsonException or InvalidOperationException or FormatException)
                {
                    // Uma linha ilegível (corte no meio de uma escrita, por exemplo) não impede de ler as outras.
                }
            }
            return records;
        }
    }

    public void Record(string action, JsonObject detail)
    {
        var line = new JsonObject { ["at"] = EventStamp.Format(clock.UtcNow), ["action"] = action, ["detail"] = detail.DeepClone() };
        lock (_gate)
        {
            try
            {
                Directory.CreateDirectory(dataRoot);
                File.AppendAllText(FilePath, line.ToJsonString() + "\n", new UTF8Encoding(false));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // A trilha não pode derrubar a operação que ela registra; quem hospeda o motor fica sabendo pelo stderr.
                Console.Error.WriteLine($"Não foi possível gravar a auditoria ({ex.GetType().Name}).");
            }
        }
    }
}
