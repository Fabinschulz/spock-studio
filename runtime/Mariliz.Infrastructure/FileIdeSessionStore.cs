using System.Text.Json;
using Mariliz.Application.Context;
using Mariliz.Infrastructure.Integrations;

namespace Mariliz.Infrastructure;

// Um arquivo por cópia do projeto em <dados>/ide-sessions. Sessão da IDE é curta: ficam as 50 mais recentes dos últimos 7 dias.
public sealed class FileIdeSessionStore(string dataRoot) : IIdeSessionStore
{
    public const int MaxSessions = 50;
    public static readonly TimeSpan KeepFor = TimeSpan.FromDays(7);

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private sealed record Entry(string ContextVersion, List<string> Noted, DateTimeOffset At);

    public IdeSessionState? Read(string projectId, string sessionId) =>
        Load(projectId).TryGetValue(sessionId, out var entry) ? new IdeSessionState(entry.ContextVersion, entry.Noted) : null;

    public void Write(string projectId, string sessionId, IdeSessionState state)
    {
        var now = DateTimeOffset.UtcNow;
        var sessions = Load(projectId);
        sessions[sessionId] = new Entry(state.ContextVersion, [.. state.Noted], now);
        var kept = sessions
            .Where(pair => now - pair.Value.At <= KeepFor)
            .OrderByDescending(pair => pair.Value.At)
            .Take(MaxSessions)
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        AtomicFile.Write(PathOf(projectId), JsonSerializer.Serialize(kept, Json));
    }

    private Dictionary<string, Entry> Load(string projectId) =>
        AtomicFile.ReadJson<Dictionary<string, Entry>>(PathOf(projectId), Json) is { } sessions
            ? new Dictionary<string, Entry>(sessions, StringComparer.Ordinal)
            : new Dictionary<string, Entry>(StringComparer.Ordinal);

    // O id da cópia (p-<hash>) já é seguro como nome de arquivo; o da sessão só vira chave do JSON.
    private string PathOf(string projectId) => Path.Combine(dataRoot, "ide-sessions", projectId + ".json");
}
