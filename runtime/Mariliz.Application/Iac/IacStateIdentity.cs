namespace Mariliz.Application.Iac;

// Uma chave por state remoto: dois projetos com a mesma identidade estariam no mesmo state. State local dentro do projeto
// é do projeto e não entra no registro.
public static class IacStateIdentity
{
    public static string? Of(IacBackend backend)
    {
        string Value(string name, string fallback = "") => backend.Settings.TryGetValue(name, out var value) ? value : fallback;
        return backend.Type switch
        {
            "s3" => $"s3://{Value("bucket")}/{Value("workspace_key_prefix", "env:")}/{Value("key")}",
            "azurerm" => $"azurerm://{Value("storage_account_name")}/{Value("container_name")}/{Value("key")}",
            "gcs" => $"gcs://{Value("bucket")}/{Value("prefix")}",
            "local" when Value("path") is { Length: > 0 } path && System.IO.Path.IsPathRooted(path) =>
                "local://" + System.IO.Path.GetFullPath(path).Replace('\\', '/').ToLowerInvariant(),
            "local" => null,
            _ => $"{backend.Type}://" + string.Join("/", backend.Settings.OrderBy(s => s.Key, StringComparer.Ordinal).Select(s => $"{s.Key}={s.Value}"))
        };
    }
}
