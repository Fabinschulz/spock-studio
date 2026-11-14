using System.Net.Http.Headers;
using System.Text.Json.Nodes;
using Mariliz.Application.Models;

namespace Mariliz.Infrastructure.ModelFormats;

internal sealed class OpenAiFormat : IModelFormat
{
    public Uri Url(ModelProvider provider, string model) => provider.Endpoint;

    public void Authenticate(HttpRequestMessage request, string key) =>
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);

    public JsonObject Body(string model, IReadOnlyList<ChatMessage> messages, int maxTokens, bool jsonObject)
    {
        var body = new JsonObject
        {
            ["model"] = model,
            ["max_tokens"] = maxTokens,
            ["messages"] = new JsonArray(messages.Select(m => (JsonNode)new JsonObject { ["role"] = m.Role, ["content"] = m.Content }).ToArray())
        };
        if (jsonObject) body["response_format"] = new JsonObject { ["type"] = "json_object" };
        return body;
    }

    public ModelReply Read(JsonObject document)
    {
        var content = document["choices"]?[0]?["message"]?["content"]?.GetValue<string>() ?? "";
        var version = document["model"]?.GetValue<string>() ?? "";
        var input = document["usage"]?["prompt_tokens"]?.GetValue<int>() ?? 0;
        var output = document["usage"]?["completion_tokens"]?.GetValue<int>() ?? 0;
        var total = document["usage"]?["total_tokens"]?.GetValue<int>() ?? input + output;
        return new ModelReply(content, version, input, output, total);
    }
}
