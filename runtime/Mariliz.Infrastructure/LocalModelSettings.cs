using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Mariliz.Application.Execution;
using Mariliz.Application.Integrations;
using Mariliz.Application.Models;

namespace Mariliz.Infrastructure;

/// <summary>
/// A chave do provedor de modelo, guardada no mesmo cofre das credenciais das integrações (no Windows, a DPAPI do usuário
/// atual) e não mais em texto puro.
///
/// Onde não há cofre (hoje Linux e macOS), a chave continua em <c>{dataRoot}/model.json</c>, como antes: recusar guardá-la
/// quebraria quem já usa, e a central de integrações já deixa claro que ali o cofre ainda não existe.
///
/// Quem já tinha uma chave em <c>model.json</c> é migrado sozinho, na primeira leitura. O arquivo antigo só some depois de
/// o cofre devolver exatamente a mesma chave: perder a chave não é um custo aceitável, e qualquer falha do cofre deixa o
/// arquivo onde está e a chave continua valendo.
/// </summary>
public sealed class LocalModelSettings(string dataRoot, ISecretVault vault) : IModelSettings
{
    private readonly object _gate = new();
    private readonly FileModelSettings _legacy = new(dataRoot);

    /// <summary>Qual provedor é o da chave do cofre. O cofre guarda só o segredo; o nome do provedor não é segredo e fica ao lado.</summary>
    private string ProviderPath => Path.Combine(dataRoot, "model-provider.json");

    public ModelStatus Status()
    {
        lock (_gate)
        {
            Migrate();
            return TryRead(out _) ? new ModelStatus(true, ActiveProvider()) : new ModelStatus(false, "");
        }
    }

    public void Validate(string provider, string key) => ModelKeyRules.Validate(provider, key);

    public string? Protection()
    {
        lock (_gate)
        {
            Migrate();
            if (vault.Available && vault.TryRead(ReservedSecretIds.ModelKey, out var stored) && ModelKeyRules.IsValidKey(stored)) return ModelKeyProtection.Vault;
            return _legacy.TryGetKey(out _) ? ModelKeyProtection.File : null;
        }
    }

    public void Save(string provider, string key)
    {
        ModelKeyRules.Validate(provider, key);

        lock (_gate)
        {
            if (!vault.Available)
            {
                _legacy.Save(provider, key);
                return;
            }

            // O provedor vai primeiro: se o cofre falhar depois, ele volta ao que era, para a chave que já valia não ficar com o
            // provedor de outra.
            var previousProvider = ReadProvider();
            try
            {
                WriteProvider(provider);
                vault.Store(ReservedSecretIds.ModelKey, key);
                // Só se dá a chave por guardada quando o cofre a devolve igual; senão ela ficaria só na promessa.
                if (!vault.TryRead(ReservedSecretIds.ModelKey, out var stored) || stored != key) throw StoreFailed();
            }
            catch (Exception ex) when (ex is IOException or CryptographicException or UnauthorizedAccessException or EngineException)
            {
                RestoreProvider(previousProvider);
                throw ex is EngineException ? ex : StoreFailed();
            }

            // A chave nova vale; um arquivo antigo com outra chave não pode continuar existindo em texto puro.
            _legacy.Clear();
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            vault.Remove(ReservedSecretIds.ModelKey);
            _legacy.Clear();
            RestoreProvider(null);
        }
    }

    public bool TryGetKey(out string key)
    {
        lock (_gate)
        {
            Migrate();
            return TryRead(out key);
        }
    }

    /// <summary>O provedor da chave que está valendo. Uma chave de antes da escolha de provedor é da OpenAI.</summary>
    private string ActiveProvider()
    {
        if (vault.Available && vault.TryRead(ReservedSecretIds.ModelKey, out var stored) && ModelKeyRules.IsValidKey(stored))
            return ReadProvider() ?? ModelProviders.Default.Id;
        return _legacy.StoredProvider() ?? ModelProviders.Default.Id;
    }

    private string? ReadProvider()
    {
        try
        {
            if (!File.Exists(ProviderPath)) return null;
            var provider = (JsonNode.Parse(File.ReadAllText(ProviderPath)) as JsonObject)?["provider"]?.GetValue<string>();
            return ModelProviders.Find(provider) is null ? null : provider;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or InvalidOperationException or FormatException)
        {
            return null;
        }
    }

    private void WriteProvider(string provider)
    {
        Directory.CreateDirectory(dataRoot);
        var temp = ProviderPath + ".tmp";
        File.WriteAllText(temp, new JsonObject { ["provider"] = provider }.ToJsonString());
        File.Move(temp, ProviderPath, overwrite: true);
    }

    /// <summary>Volta o arquivo do provedor ao que era; nulo apaga.</summary>
    private void RestoreProvider(string? provider)
    {
        try
        {
            if (provider is not null) WriteProvider(provider);
            else if (File.Exists(ProviderPath)) File.Delete(ProviderPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Não há mais o que fazer aqui: o erro que importa é o que levou a desfazer.
        }
    }

    private bool TryRead(out string key)
    {
        if (vault.Available && vault.TryRead(ReservedSecretIds.ModelKey, out var stored) && ModelKeyRules.IsValidKey(stored))
        {
            key = stored;
            return true;
        }

        return _legacy.TryGetKey(out key);
    }

    /// <summary>Passa a chave de <c>model.json</c> para o cofre, se houver cofre e houver arquivo.</summary>
    private void Migrate()
    {
        if (!vault.Available || !_legacy.TryGetKey(out var legacyKey)) return;

        try
        {
            if (!vault.TryRead(ReservedSecretIds.ModelKey, out var stored))
            {
                WriteProvider(_legacy.StoredProvider() ?? ModelProviders.Default.Id);
                vault.Store(ReservedSecretIds.ModelKey, legacyKey);
                vault.TryRead(ReservedSecretIds.ModelKey, out stored);
            }

            // O arquivo só some quando o cofre tem a mesma chave. Com outra chave no cofre, o arquivo fica: não cabe a este
            // código decidir qual das duas a pessoa queria.
            if (stored == legacyKey) _legacy.Clear();
        }
        catch (Exception ex) when (ex is IOException or CryptographicException or UnauthorizedAccessException)
        {
            // O arquivo continua lá e a chave continua valendo; a migração é tentada de novo na próxima leitura.
        }
    }

    private static EngineException StoreFailed() =>
        new("MODEL_KEY_STORE_FAILED", "Não foi possível guardar a chave com a proteção do sistema.");
}
