using System.Text.Json;
using System.Text.RegularExpressions;
using Mariliz.Application.Iac;
using Mariliz.Application.Integrations;
using Mariliz.Infrastructure.Integrations;

namespace Mariliz.Infrastructure.Iac;

// Lê o bloco backend como texto, sem rodar a ferramenta. Só as configurações da lista entram no resultado: chave de
// acesso, token, SAS e afins podem estar no bloco e nunca são lidos.
public sealed partial class IacStateReader : IIacStateReader
{
    private static readonly HashSet<string> SafeSettings = new(StringComparer.Ordinal)
    {
        "bucket", "key", "workspace_key_prefix", "region", "encrypt", "use_lockfile", "dynamodb_table", "profile",
        "resource_group_name", "storage_account_name", "container_name", "use_azuread_auth", "prefix", "path", "workspace_dir",
        "hostname", "organization"
    };

    public IacBackend? Backend(string projectRoot, string folder)
    {
        foreach (var text in Texts(projectRoot, folder))
        {
            var match = BackendStart().Match(text);
            if (!match.Success) continue;
            var body = Block(text, match.Index + match.Length - 1);
            var settings = new SortedDictionary<string, string>(StringComparer.Ordinal);
            foreach (Match attribute in Attribute().Matches(body))
            {
                var name = attribute.Groups[1].Value;
                if (SafeSettings.Contains(name)) settings[name] = attribute.Groups[2].Value.Trim('"');
            }
            return new IacBackend(folder, match.Groups[1].Value, settings);
        }
        return null;
    }

    public bool IsRoot(string projectRoot, string folder) => Texts(projectRoot, folder).Any(text => RootMarker().IsMatch(text));

    public IReadOnlyList<string> Providers(string projectRoot, string folder) =>
        Texts(projectRoot, folder).SelectMany(text => ProviderBlock().Matches(text).Select(m => m.Groups[1].Value)).Distinct(StringComparer.Ordinal).ToList();

    public IReadOnlyList<string> LocalStateFiles(string projectRoot, string folder)
    {
        var directory = Directory(projectRoot, folder);
        var found = new List<string>();
        if (File.Exists(Path.Combine(directory, "terraform.tfstate"))) found.Add("terraform.tfstate");
        var workspaces = Path.Combine(directory, "terraform.tfstate.d");
        if (System.IO.Directory.Exists(workspaces))
            foreach (var workspace in System.IO.Directory.EnumerateDirectories(workspaces).Order(StringComparer.Ordinal))
                if (File.Exists(Path.Combine(workspace, "terraform.tfstate")))
                    found.Add($"terraform.tfstate.d/{Path.GetFileName(workspace)}/terraform.tfstate");
        return found;
    }

    private static IEnumerable<string> Texts(string projectRoot, string folder)
    {
        var directory = Directory(projectRoot, folder);
        if (!System.IO.Directory.Exists(directory)) yield break;
        foreach (var file in System.IO.Directory.EnumerateFiles(directory, "*.tf").Concat(System.IO.Directory.EnumerateFiles(directory, "*.tofu"))
                     .Order(StringComparer.Ordinal))
        {
            string text;
            try
            {
                text = File.ReadAllText(file);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                continue;
            }
            // Arquivo salvo no Windows vem com \r\n; o "$" das expressões só fecha a linha antes do \n.
            yield return Comments().Replace(text.Replace("\r\n", "\n"), "");
        }
    }

    private static string Directory(string projectRoot, string folder) =>
        folder.Length == 0 ? projectRoot : Path.Combine(projectRoot, folder.Replace('/', Path.DirectorySeparatorChar));

    // Do "{" do bloco até a chave que o fecha.
    private static string Block(string text, int open)
    {
        var depth = 0;
        for (var i = open; i < text.Length; i++)
        {
            if (text[i] == '{') depth++;
            else if (text[i] == '}' && --depth == 0) return text[(open + 1)..i];
        }
        return text[(open + 1)..];
    }

    [GeneratedRegex("""backend\s+"([A-Za-z0-9_-]+)"\s*\{""")]
    private static partial Regex BackendStart();

    [GeneratedRegex("""^[ \t]*([a-z_]+)[ \t]*=[ \t]*("[^"\r\n]*"|true|false|[0-9]+)[ \t]*$""", RegexOptions.Multiline)]
    private static partial Regex Attribute();

    [GeneratedRegex("""^[ \t]*(provider|backend)[ \t]+"[A-Za-z0-9_-]+"[ \t]*\{""", RegexOptions.Multiline)]
    private static partial Regex RootMarker();

    [GeneratedRegex("""^[ \t]*provider[ \t]+"([A-Za-z0-9_-]+)""", RegexOptions.Multiline)]
    private static partial Regex ProviderBlock();

    [GeneratedRegex(@"(?m)^\s*(#|//).*$")]
    private static partial Regex Comments();
}

// <dados>/iac/state-registry.json: identidade do state → projetos do Spock que o usam.
public sealed class FileIacStateRegistry(string dataRoot) : IIacStateRegistry
{
    private readonly object _gate = new();
    private string FilePath => Path.Combine(dataRoot, "iac", "state-registry.json");

    public IReadOnlyList<string> Owners(string identity)
    {
        lock (_gate) return Read().TryGetValue(identity, out var owners) ? owners : [];
    }

    public void Claim(string identity, string projectId)
    {
        lock (_gate)
        {
            var registry = Read();
            var owners = registry.TryGetValue(identity, out var list) ? list : [];
            if (owners.Contains(projectId)) return;
            registry[identity] = [.. owners, projectId];
            AtomicFile.Write(FilePath, JsonSerializer.Serialize(registry, IntegrationJson.Options));
        }
    }

    private Dictionary<string, List<string>> Read() =>
        AtomicFile.ReadJson<Dictionary<string, List<string>>>(FilePath, IntegrationJson.Options) ?? new Dictionary<string, List<string>>(StringComparer.Ordinal);
}
