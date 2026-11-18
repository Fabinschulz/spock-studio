using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Mariliz.Application.Ports;
using Mariliz.Core.Plan;
using Mariliz.Core.Text;

namespace Mariliz.Application.Harness;

// C1 (CONTEXT_PERSISTENCE_AND_ADAPTERS.md §5.4): o Spock gerencia só um trecho de um arquivo que é da pessoa (CLAUDE.md,
// AGENTS.md, .mcp.json, .codex/config.toml). O plano compara o trecho, não o arquivo; o resto nunca é lido para decidir nem
// reescrito. Formato que não dá para editar com segurança vira FormatException, e o plano mostra conflito em vez de tentar.
public static partial class ManagedRegion
{
    // Markdown e texto: entre <!-- spock:begin <id> --> e <!-- spock:end <id> -->.
    public const string Block = "block";
    // JSON: uma chave própria num caminho de objetos ("mcpServers.spock"). O arquivo é reformatado; o sentido do resto, não.
    public const string JsonKey = "json-key";
    // TOML: uma tabela própria e as subtabelas dela ("mcp_servers.spock").
    public const string TomlTable = "toml-table";

    public static bool IsRegionMode(string mode) => mode is Block or JsonKey or TomlTable;

    public static string Hash(string content) => Hashing.Sha256(Normalize(content));

    /// <returns>O trecho, normalizado; nulo quando o arquivo não o tem.</returns>
    public static string? Extract(string mode, string region, string text) => mode switch
    {
        Block => ExtractBlock(region, text),
        JsonKey => ExtractJson(region, text),
        TomlTable => ExtractToml(region, text),
        _ => throw new ArgumentException($"Modo sem região: {mode}.", nameof(mode))
    };

    /// <returns>O arquivo inteiro com o trecho no lugar (ou acrescentado ao fim), e tudo o mais como estava.</returns>
    public static string Upsert(string mode, string region, string text, string content) => mode switch
    {
        Block => UpsertBlock(region, text, content),
        JsonKey => UpsertJson(region, text, content),
        TomlTable => UpsertToml(region, text, content),
        _ => throw new ArgumentException($"Modo sem região: {mode}.", nameof(mode))
    };

    /// <returns>O arquivo sem o trecho (e sem a linha em branco que o separava); o resto fica como estava.</returns>
    public static string Remove(string mode, string region, string text)
    {
        switch (mode)
        {
            case Block when FindBlock(region, Lines(text)) is { } block:
                return Without(text, block.Begin, block.End);
            case TomlTable when FindTable(region, Lines(text)) is { } table:
                return Without(text, table.Begin, table.End);
            case JsonKey:
                var root = ParseObject(text);
                JsonNode? node = root;
                var parts = JsonPath(region);
                foreach (var part in parts[..^1]) node = (node as JsonObject)?[part];
                if (node is not JsonObject parent || !parent.Remove(parts[^1])) return text;
                return root.ToJsonString(Indented).Replace("\r\n", "\n") + "\n";
            case Block or TomlTable:
                return text;
            default:
                throw new ArgumentException($"Modo sem região: {mode}.", nameof(mode));
        }
    }

    private static string Without(string text, int start, int end)
    {
        var raw = text.Split('\n');
        // A linha em branco que Append pôs antes do trecho sai junto.
        if (start > 0 && raw[start - 1].TrimEnd('\r').Length == 0) start--;
        return string.Join('\n', raw.Take(start).Concat(raw.Skip(end + 1)));
    }

    // Para quem compõe o plano: o arquivo desejado é o que está no disco com o trecho do Spock no lugar.
    public static (DesiredFile? File, string? Problem) Desired(IProjectFiles files, string path, string mode, string region, string content,
        ComponentKind kind, string componentId, string origin, string reason)
    {
        try
        {
            var merged = Upsert(mode, region, files.ReadText(path) ?? "", content);
            return (new DesiredFile(path, kind, componentId, origin, reason, merged, mode, region), null);
        }
        catch (FormatException ex)
        {
            return (null, $"{path}: {ex.Message}");
        }
    }

    private static string Normalize(string text) => text.Replace("\r\n", "\n");

    private static string NewLine(string text) => text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";

    // Acrescenta ao fim, separado por uma linha em branco, sem tocar no que já estava.
    private static string Append(string text, IEnumerable<string> lines)
    {
        var newline = NewLine(text);
        var block = string.Join(newline, lines) + newline;
        if (text.Length == 0) return block;
        var withEnd = text.EndsWith('\n') ? text : text + newline;
        var separated = withEnd.EndsWith(newline + newline, StringComparison.Ordinal) || withEnd == newline ? withEnd : withEnd + newline;
        return separated + block;
    }

    // Troca as linhas [start, end] (índices nas linhas cruas) mantendo o fim de linha do arquivo.
    private static string Replace(string text, int start, int end, IReadOnlyList<string> lines)
    {
        var raw = text.Split('\n');
        var cr = NewLine(text) == "\r\n" ? "\r" : "";
        var replaced = raw.Take(start).Concat(lines.Select(line => line + cr)).Concat(raw.Skip(end + 1));
        return string.Join('\n', replaced);
    }

    private static string[] Lines(string text) => Normalize(text).Split('\n');

    // --- Markdown ---------------------------------------------------------------------------------------------------

    [GeneratedRegex(@"^[a-z0-9][a-z0-9.\-]{0,63}$")]
    private static partial Regex RegionId();

    private static string Begin(string id, string content) => $"<!-- spock:begin {id} sha256={Hash(content)[..16]} -->";

    private static string End(string id) => $"<!-- spock:end {id} -->";

    private static (int Begin, int End)? FindBlock(string id, string[] lines)
    {
        if (!RegionId().IsMatch(id)) throw new ArgumentException($"Id de bloco inválido: {id}.", nameof(id));
        var begins = lines.Select((line, index) => (line.TrimEnd(), index))
            .Where(item => item.Item1.StartsWith($"<!-- spock:begin {id} ", StringComparison.Ordinal) || item.Item1 == $"<!-- spock:begin {id} -->")
            .Select(item => item.index).ToList();
        var ends = lines.Select((line, index) => (line.TrimEnd(), index)).Where(item => item.Item1 == End(id)).Select(item => item.index).ToList();
        if (begins.Count == 0 && ends.Count == 0) return null;
        if (begins.Count != 1 || ends.Count != 1 || ends[0] < begins[0])
            throw new FormatException($"os marcadores do trecho '{id}' do Spock estão incompletos, repetidos ou fora de ordem.");
        return (begins[0], ends[0]);
    }

    private static string? ExtractBlock(string id, string text)
    {
        var lines = Lines(text);
        return FindBlock(id, lines) is { } found ? string.Join('\n', lines[(found.Begin + 1)..found.End]) : null;
    }

    private static string UpsertBlock(string id, string text, string content)
    {
        var body = Normalize(content).TrimEnd('\n');
        string[] rendered = [Begin(id, body), .. body.Split('\n'), End(id)];
        return FindBlock(id, Lines(text)) is { } found ? Replace(text, found.Begin, found.End, rendered) : Append(text, rendered);
    }

    // --- JSON -------------------------------------------------------------------------------------------------------

    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    private static JsonObject ParseObject(string text)
    {
        if (text.Trim().Length == 0) return [];
        try
        {
            // Sem comentários: ao regravar, eles sumiriam. Melhor recusar do que apagar o que a pessoa escreveu.
            return JsonNode.Parse(text, documentOptions: new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Disallow }) as JsonObject
                   ?? throw new FormatException("o arquivo não é um objeto JSON.");
        }
        catch (JsonException)
        {
            throw new FormatException("o JSON não lê (ou tem comentários, que a regravação apagaria).");
        }
    }

    private static string[] JsonPath(string region) =>
        region.Split('.') is { Length: > 0 } parts && parts.All(part => part.Length > 0)
            ? parts
            : throw new ArgumentException($"Caminho JSON inválido: {region}.", nameof(region));

    private static string? ExtractJson(string region, string text)
    {
        JsonNode? node = ParseObject(text);
        foreach (var part in JsonPath(region))
        {
            if (node is not JsonObject obj || !obj.TryGetPropertyValue(part, out node)) return null;
        }
        return node?.ToJsonString();
    }

    private static string UpsertJson(string region, string text, string content)
    {
        var root = ParseObject(text);
        var parts = JsonPath(region);
        var parent = root;
        foreach (var part in parts[..^1])
        {
            if (!parent.TryGetPropertyValue(part, out var next) || next is null) parent[part] = next = new JsonObject();
            parent = next as JsonObject ?? throw new FormatException($"'{part}' existe e não é um objeto.");
        }
        parent[parts[^1]] = JsonNode.Parse(content);
        return root.ToJsonString(Indented).Replace("\r\n", "\n") + "\n";
    }

    // --- TOML -------------------------------------------------------------------------------------------------------

    [GeneratedRegex(@"^\s*\[\[?\s*([^\]]+?)\s*\]\]?\s*(#.*)?$")]
    private static partial Regex TomlHeader();

    private static string? HeaderName(string line) => TomlHeader().Match(line) is { Success: true } match ? match.Groups[1].Value : null;

    private static bool InRegion(string? header, string region) =>
        header is not null && (header == region || header.StartsWith(region + ".", StringComparison.Ordinal));

    private static (int Begin, int End)? FindTable(string region, string[] lines)
    {
        var last = region[(region.LastIndexOf('.') + 1)..];
        var parent = region.Contains('.') ? region[..region.LastIndexOf('.')] : "";
        (int Begin, int End)? found = null;
        string? current = "";
        for (var index = 0; index < lines.Length; index++)
        {
            var header = HeaderName(lines[index]);
            if (header is not null)
            {
                current = header;
                if (header == region)
                {
                    if (found is not null) throw new FormatException($"a tabela [{region}] aparece mais de uma vez.");
                    var end = index;
                    for (var next = index + 1; next < lines.Length; next++)
                    {
                        var nextHeader = HeaderName(lines[next]);
                        if (nextHeader == region) throw new FormatException($"a tabela [{region}] aparece mais de uma vez.");
                        if (nextHeader is not null && !InRegion(nextHeader, region)) break;
                        if (lines[next].Trim().Length > 0) end = next;
                    }
                    found = (index, end);
                    index = end;
                }
                else if (InRegion(header, region))
                {
                    throw new FormatException($"[{header}] fica longe de [{region}]; junte as duas ou edite à mão.");
                }
                continue;
            }
            // A mesma tabela escrita de outro jeito (chave pontuada ou tabela inline) seria uma segunda definição.
            var key = lines[index].TrimStart();
            if ((current == parent && Regex.IsMatch(key, $@"^""?{Regex.Escape(last)}""?\s*[.=]"))
                || (current == "" && key.StartsWith(region + ".", StringComparison.Ordinal)))
                throw new FormatException($"'{region}' já está definida fora de uma tabela [{region}]; edite à mão.");
        }
        return found;
    }

    private static string? ExtractToml(string region, string text)
    {
        var lines = Lines(text);
        return FindTable(region, lines) is { } found ? string.Join('\n', lines[found.Begin..(found.End + 1)]) : null;
    }

    private static string UpsertToml(string region, string text, string content)
    {
        var body = Normalize(content).TrimEnd('\n').Split('\n');
        if (HeaderName(body[0]) != region) throw new ArgumentException($"O conteúdo precisa começar por [{region}].", nameof(content));
        return FindTable(region, Lines(text)) is { } found ? Replace(text, found.Begin, found.End, body) : Append(text, body);
    }
}
