using System.Text.Json;
using System.Text.RegularExpressions;
using Mariliz.Application.Execution;
using Mariliz.Application.Ports;
using Mariliz.Application.Workspace;
using Mariliz.Core.Policy;

namespace Mariliz.Application.Engineering;

// O valor nunca sai do motor.
public sealed record McpEnvVar(string Name, bool Referenced);

// O valor encontrado nunca é devolvido.
public sealed record McpLiteralSecret(string Where, string Why);

/// <param name="Runs">Para <c>http</c> e <c>sse</c>, só o servidor (sem caminho, sem consulta, sem usuário e senha).</param>
public sealed record McpServerEntry(
    string Name,
    string Transport,
    string Runs,
    IReadOnlyList<McpEnvVar> Env,
    IReadOnlyList<McpLiteralSecret> LiteralSecrets);

public sealed record McpInventory(string ProjectId, bool Present, bool Readable, IReadOnlyList<McpServerEntry> Servers);

// Só leitura: a chave spock do .mcp.json é do Harness, por plano aprovado; este inventário não grava, e nenhum valor de variável, argumento ou cabeçalho sai daqui.
public sealed partial class McpInventoryService(WorkspaceAuthorizer workspace, IProjectFilesFactory files)
{
    public const string FileName = ".mcp.json";

    public McpInventory Read(string projectId)
    {
        if (!workspace.TryGetRoot(projectId, out var root)) throw EngineErrors.ProjectNotAuthorized();
        var text = files.Open(root).ReadText(FileName);
        if (text is null) return new McpInventory(projectId, false, true, []);

        try
        {
            using var document = JsonDocument.Parse(text);
            if (document.RootElement.ValueKind != JsonValueKind.Object) return new McpInventory(projectId, true, false, []);
            if (!document.RootElement.TryGetProperty("mcpServers", out var servers) || servers.ValueKind != JsonValueKind.Object)
                return new McpInventory(projectId, true, true, []);

            return new McpInventory(projectId, true, true,
                servers.EnumerateObject().Select(server => Describe(server.Name, server.Value)).ToList());
        }
        catch (JsonException)
        {
            return new McpInventory(projectId, true, false, []);
        }
    }

    private static McpServerEntry Describe(string name, JsonElement server)
    {
        if (server.ValueKind != JsonValueKind.Object) return new McpServerEntry(name, "unknown", "configuração fora do formato esperado", [], []);

        var secrets = new List<McpLiteralSecret>();
        var command = Str(server, "command");
        var url = Str(server, "url");
        var declared = Str(server, "type");
        var transport = declared is "stdio" or "http" or "sse" ? declared : command is not null ? "stdio" : url is not null ? "http" : "unknown";

        if (command is not null) Check("command", command, secrets);
        if (url is not null)
        {
            Check("url", url, secrets);
            // Credencial na consulta do endereço (?api_key=...): o nome do parâmetro diz o que é, mesmo num formato desconhecido.
            if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
                foreach (var pair in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
                {
                    var parts = pair.Split('=', 2);
                    if (parts.Length == 2 && parts[1].Length > 0 && !IsReference(Uri.UnescapeDataString(parts[1])) && SecretName().IsMatch(parts[0]))
                        Add(secrets, new McpLiteralSecret("url", "O endereço traz uma credencial na consulta."));
                }
        }

        if (server.TryGetProperty("args", out var args) && args.ValueKind == JsonValueKind.Array)
        {
            var index = 0;
            foreach (var arg in args.EnumerateArray())
            {
                if (arg.ValueKind == JsonValueKind.String) Check($"args[{index}]", arg.GetString()!, secrets);
                index++;
            }
        }

        var env = new List<McpEnvVar>();
        foreach (var (key, value) in Pairs(server, "env"))
        {
            env.Add(new McpEnvVar(key, IsReference(value)));
            CheckNamed($"env.{key}", key, value, secrets);
        }
        foreach (var (key, value) in Pairs(server, "headers")) CheckNamed($"headers.{key}", key, value, secrets);

        var runs = transport == "stdio" ? ProgramName(command, secrets) : Host(url);
        return new McpServerEntry(name, transport, runs, env, secrets);
    }

    private static void Check(string where, string value, List<McpLiteralSecret> secrets)
    {
        var finding = SecretScanner.Scan(value).FirstOrDefault();
        if (finding is not null) Add(secrets, new McpLiteralSecret(where, $"Formato de credencial conhecido ({finding.Rule})."));
    }

    // O nome também conta: GITHUB_TOKEN com um valor escrito é uma credencial, qualquer que seja o formato.
    private static void CheckNamed(string where, string key, string value, List<McpLiteralSecret> secrets)
    {
        Check(where, value, secrets);
        if (value.Length > 0 && !IsReference(value) && SecretName().IsMatch(key))
            Add(secrets, new McpLiteralSecret(where, "O nome indica uma credencial e o valor está escrito no arquivo."));
    }

    private static void Add(List<McpLiteralSecret> secrets, McpLiteralSecret secret)
    {
        if (secrets.All(existing => existing.Where != secret.Where)) secrets.Add(secret);
    }

    private static bool IsReference(string value)
    {
        if (!Reference().IsMatch(value)) return false;
        var rest = Reference().Replace(value, "").Trim();
        return rest.Length == 0 || rest.Equals("Bearer", StringComparison.OrdinalIgnoreCase) || rest.Equals("Basic", StringComparison.OrdinalIgnoreCase);
    }

    private static string ProgramName(string? command, List<McpLiteralSecret> secrets)
    {
        if (command is null) return "sem programa declarado";
        if (secrets.Any(secret => secret.Where == "command")) return "omitido: parece uma credencial";
        var name = command.Replace('\\', '/').Split('/').Last();
        return name.Length == 0 ? "sem programa declarado" : name;
    }

    // Caminho, consulta e usuário com senha podem carregar credencial e ficam de fora.
    private static string Host(string? url)
    {
        if (url is null) return "sem endereço declarado";
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Host.Length == 0) return "endereço fora do formato esperado";
        return uri.Host;
    }

    private static string? Str(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static IEnumerable<(string Key, string Value)> Pairs(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var map) || map.ValueKind != JsonValueKind.Object) yield break;
        foreach (var property in map.EnumerateObject())
            yield return (property.Name, property.Value.ValueKind == JsonValueKind.String ? property.Value.GetString()! : property.Value.GetRawText());
    }

    [GeneratedRegex(@"\$\{[A-Za-z_][A-Za-z0-9_]*(?::-[^}]*)?\}|\$[A-Za-z_][A-Za-z0-9_]*")]
    private static partial Regex Reference();

    [GeneratedRegex(@"token|secret|password|passwd|pwd|api[_-]?key|apikey|authorization|credential|bearer", RegexOptions.IgnoreCase)]
    private static partial Regex SecretName();
}
