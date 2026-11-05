using Mariliz.Application.Execution;
using System.Text.Json.Nodes;

namespace Mariliz.Application.Protocol;

internal static class ProtocolMessages
{
    public const int Version = 1;
    public const int MaxMessageBytes = 4 * 1024 * 1024;

    public static JsonObject Ok(string id, JsonObject result) => new()
    {
        ["v"] = Version, ["kind"] = "response", ["id"] = id, ["result"] = result
    };

    public static JsonObject Error(string? id, string code, string message) => new()
    {
        ["v"] = Version,
        ["kind"] = "response",
        ["id"] = id,
        ["error"] = new JsonObject { ["code"] = code, ["message"] = message }
    };

    public static JsonObject Event(string runId, int seq, string type, JsonObject data, DateTimeOffset at) => new()
    {
        ["v"] = Version, ["kind"] = "event", ["runId"] = runId, ["type"] = type, ["seq"] = seq,
        ["at"] = EventStamp.Format(at), ["data"] = data.DeepClone()
    };
}
