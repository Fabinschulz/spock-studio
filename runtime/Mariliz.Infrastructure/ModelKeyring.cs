using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Mariliz.Application.Execution;
using Mariliz.Application.Integrations;
using Mariliz.Application.Models;

namespace Mariliz.Infrastructure;

// Uma chave por provedor, cada uma no seu lugar do cofre: guardar uma nunca mexe nas dos outros. O provedor padrão é só um ponteiro, sem segredo.
// A chave de antes de haver uma por provedor migra para o lugar do provedor dela, e só some do lugar antigo depois de o novo devolver a mesma chave.
public sealed class ModelKeyring : IModelSettings
{
    private readonly object _gate = new();
    private readonly string _dataRoot;
    private readonly Dictionary<string, LocalModelSettings> _slots;
    private readonly LocalModelSettings _legacy;

    public ModelKeyring(string dataRoot, ISecretVault vault)
    {
        _dataRoot = dataRoot;
        _slots = ModelProviders.All.ToDictionary(
            provider => provider.Id,
            provider => new LocalModelSettings(Path.Combine(dataRoot, "models", provider.Id), vault, ReservedSecretIds.ModelKeyOf(provider.Id)),
            StringComparer.Ordinal);
        _legacy = new LocalModelSettings(dataRoot, vault);
    }

    private string DefaultPath => Path.Combine(_dataRoot, "model-default.json");

    public ModelStatus Status()
    {
        lock (_gate) return DefaultProvider(ConfiguredProviders()) is { } provider ? new ModelStatus(true, provider) : new ModelStatus(false, "");
    }

    public IReadOnlyList<string> Providers()
    {
        lock (_gate) return ConfiguredProviders();
    }

    public void Validate(string provider, string key) => ModelKeyRules.Validate(provider, key);

    public string? Protection()
    {
        lock (_gate) return DefaultProvider(ConfiguredProviders()) is { } provider ? Holder(provider).Protection() : null;
    }

    public string? Protection(string provider)
    {
        lock (_gate) return Holder(provider).Protection();
    }

    public void Save(string provider, string key)
    {
        lock (_gate)
        {
            var current = DefaultProvider(ConfiguredProviders());
            Slot(provider).Save(provider, key);
            // A chave nova vale; a do lugar antigo, se era deste provedor, ficaria para trás.
            if (_legacy.Status() is { Configured: true } old && old.Provider == provider) _legacy.Clear();
            // Guardar uma chave não tira o padrão de quem já era: o ponteiro só passa a existir fixando o que já valia.
            if (ReadDefault() is null) WriteDefault(current ?? provider);
        }
    }

    public void Remove(string provider)
    {
        lock (_gate)
        {
            Slot(provider).Clear();
            if (_legacy.Status() is { Configured: true } old && old.Provider == provider) _legacy.Clear();
            if (ReadDefault() == provider) DeleteDefault();
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            foreach (var slot in _slots.Values) slot.Clear();
            _legacy.Clear();
            DeleteDefault();
        }
    }

    public bool TryGetKey(out string key)
    {
        lock (_gate)
        {
            if (DefaultProvider(ConfiguredProviders()) is { } provider) return Holder(provider).TryGetKey(out key);
            key = "";
            return false;
        }
    }

    public bool TryGetKey(string provider, out string key)
    {
        lock (_gate) return Holder(provider).TryGetKey(out key);
    }

    public void SetDefault(string provider)
    {
        lock (_gate)
        {
            Slot(provider);
            WriteDefault(provider);
        }
    }

    private LocalModelSettings Slot(string provider) =>
        _slots.GetValueOrDefault(provider) ?? throw new EngineException("MODEL_PROVIDER_DENIED", "O provedor não está disponível.");

    private List<string> ConfiguredProviders()
    {
        MigrateLegacy();
        return ModelProviders.All.Where(provider => Holder(provider.Id).Status().Configured).Select(provider => provider.Id).ToList();
    }

    // O lugar do provedor; enquanto a migração da chave antiga não terminou, ela continua valendo no lugar antigo.
    private IModelSettings Holder(string provider)
    {
        var slot = Slot(provider);
        if (slot.Status().Configured) return slot;
        return _legacy.Status() is { Configured: true } old && old.Provider == provider ? _legacy : slot;
    }

    private void MigrateLegacy()
    {
        var old = _legacy.Status();
        if (!old.Configured || !_slots.TryGetValue(old.Provider, out var slot) || !_legacy.TryGetKey(out var key)) return;

        try
        {
            if (!slot.Status().Configured) slot.Save(old.Provider, key);
            // Com outra chave já no lugar novo, a antiga fica: não cabe a este código decidir qual das duas a pessoa queria.
            if (slot.TryGetKey(out var stored) && stored == key) _legacy.Clear();
        }
        catch (Exception ex) when (ex is EngineException or IOException or CryptographicException or UnauthorizedAccessException)
        {
            // A chave continua no lugar antigo e continua valendo; a migração é tentada de novo na próxima leitura.
        }
    }

    // O ponteiro vale se o provedor dele ainda tem chave; senão, o primeiro que tiver.
    private string? DefaultProvider(List<string> configured)
    {
        if (configured.Count == 0) return null;
        return ReadDefault() is { } stored && configured.Contains(stored) ? stored : configured[0];
    }

    private string? ReadDefault()
    {
        try
        {
            if (!File.Exists(DefaultPath)) return null;
            var provider = (JsonNode.Parse(File.ReadAllText(DefaultPath)) as JsonObject)?["provider"]?.GetValue<string>();
            return ModelProviders.Find(provider) is null ? null : provider;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or InvalidOperationException or FormatException)
        {
            return null;
        }
    }

    private void WriteDefault(string provider)
    {
        Directory.CreateDirectory(_dataRoot);
        var temp = DefaultPath + ".tmp";
        File.WriteAllText(temp, new JsonObject { ["provider"] = provider }.ToJsonString());
        File.Move(temp, DefaultPath, overwrite: true);
    }

    private void DeleteDefault()
    {
        if (File.Exists(DefaultPath)) File.Delete(DefaultPath);
    }
}
