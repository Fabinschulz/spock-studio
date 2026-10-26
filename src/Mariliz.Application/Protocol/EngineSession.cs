using System.Text.Json;
using System.Text.Json.Nodes;

namespace Mariliz.Application.Protocol;

/// <summary>
/// Laço do protocolo NDJSON v1: uma mensagem JSON por linha. Só escreve respostas e eventos
/// no <c>output</c>; diagnósticos são responsabilidade de quem hospeda a sessão (stderr).
/// </summary>
public sealed class EngineSession
{
    public const int ProtocolVersion = 1;
    public const int MaxMessageBytes = 4 * 1024 * 1024;

    private readonly string _engineVersion;
    private readonly string[] _capabilities;

    public EngineSession(string engineVersion, IEnumerable<string>? capabilities = null)
    {
        _engineVersion = engineVersion;
        _capabilities = capabilities?.ToArray() ?? [];
    }

    public async Task RunAsync(TextReader input, TextWriter output, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var line = await input.ReadLineAsync(ct);
            if (line is null) return;
            if (string.IsNullOrWhiteSpace(line)) continue;

            var (response, shutdown) = Handle(line);
            await output.WriteLineAsync(response.ToJsonString());
            await output.FlushAsync(ct);
            if (shutdown) return;
        }
    }

    internal (JsonObject Response, bool Shutdown) Handle(string line)
    {
        if (System.Text.Encoding.UTF8.GetByteCount(line) > MaxMessageBytes)
            return (Error(null, ProtocolErrorCodes.MessageTooLarge, "Mensagem acima do limite permitido."), false);

        JsonObject? message;
        try { message = JsonNode.Parse(line) as JsonObject; }
        catch (JsonException) { message = null; }

        if (message is null)
            return (Error(null, ProtocolErrorCodes.InvalidMessage, "A mensagem não é um objeto JSON válido."), false);

        var id = message["id"]?.GetValue<string>();

        if (message["v"]?.GetValue<int>() != ProtocolVersion)
            return (Error(id, ProtocolErrorCodes.UnsupportedProtocol, "Versão de protocolo não suportada."), false);

        if (message["kind"]?.GetValue<string>() != "request" || id is null || message["method"] is not JsonValue methodNode)
            return (Error(id, ProtocolErrorCodes.InvalidMessage, "Requisição malformada."), false);

        return methodNode.GetValue<string>() switch
        {
            "engine.hello" => (Hello(id, message["params"] as JsonObject), false),
            "engine.shutdown" => (Ok(id, new JsonObject()), true),
            _ => (Error(id, ProtocolErrorCodes.MethodNotFound, "Método desconhecido."), false)
        };
    }

    private JsonObject Hello(string id, JsonObject? parameters)
    {
        var offered = parameters?["protocol"] as JsonArray;
        if (offered is null || parameters?["hostVersion"] is null)
            return Error(id, ProtocolErrorCodes.InvalidParams, "Parâmetros de engine.hello inválidos.");

        if (!offered.Any(n => n is JsonValue v && v.TryGetValue<int>(out var p) && p == ProtocolVersion))
            return Error(id, ProtocolErrorCodes.UnsupportedProtocol, "Nenhuma versão de protocolo em comum.");

        return Ok(id, new JsonObject
        {
            ["protocol"] = ProtocolVersion,
            ["engineVersion"] = _engineVersion,
            ["capabilities"] = new JsonArray(_capabilities.Select(c => (JsonNode)JsonValue.Create(c)!).ToArray())
        });
    }

    private static JsonObject Ok(string id, JsonObject result) => new()
    {
        ["v"] = ProtocolVersion, ["kind"] = "response", ["id"] = id, ["result"] = result
    };

    private static JsonObject Error(string? id, string code, string message) => new()
    {
        ["v"] = ProtocolVersion, ["kind"] = "response", ["id"] = id,
        ["error"] = new JsonObject { ["code"] = code, ["message"] = message }
    };
}
