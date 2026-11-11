using System.Collections.Concurrent;

namespace Mariliz.Application.Integrations;

/// <summary>Quais integrações estão sincronizando agora. Garante uma sincronização por integração e segura mudanças de recorte no meio de uma.</summary>
public sealed class SyncActivity
{
    private readonly ConcurrentDictionary<string, string> _running = new(StringComparer.Ordinal);

    public bool TryBegin(string integrationId, string syncId) => _running.TryAdd(integrationId, syncId);

    public void End(string integrationId) => _running.TryRemove(integrationId, out _);

    public bool IsRunning(string integrationId) => _running.ContainsKey(integrationId);

    public string? RunningSync(string integrationId) => _running.GetValueOrDefault(integrationId);
}
