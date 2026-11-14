using System.Text.Json.Nodes;
using Mariliz.Application.Models;

namespace Mariliz.Infrastructure.ModelFormats;

// A Anthropic não tem modo de resposta em JSON: o pedido vai na instrução, e a cerca de código em volta da resposta é tirada pelo transporte.
internal sealed class AnthropicFormat : IModelFormat
{
    public const string Version = "2023-06-01";

    public Uri Url(ModelProvider provider, string model) => provider.Endpoint;

    public void Authenticate(HttpRequestMessage request, string key)
    {
        request.Headers.Add("x-api-key", key);
        request.Headers.Add("anthropic-version", Version);
    }

    public JsonObject Body(string model, IReadOnlyList<ChatMessage> messages, int maxTokens, bool jsonObject)
    {
        var layout = jsonObject ? ChatLayout.Split(messages, JsonInstruction.Text) : ChatLayout.Split(messages);
        var body = new JsonObject
        {
            ["model"] = model,
            ["max_tokens"] = maxTokens,
            ["messages"] = new JsonArray(layout.Conversation.Select(m => (JsonNode)new JsonObject
            {
                ["role"] = m.Role == "assistant" ? "assistant" : "user",
                ["content"] = m.Content
            }).ToArray())
        };
        if (layout.System is not null) body["system"] = layout.System;
        return body;
    }

    public ModelReply Read(JsonObject document)
    {
        var content = document["content"] is JsonArray blocks
            ? string.Concat(blocks.OfType<JsonObject>().Where(b => b["type"]?.GetValue<string>() == "text").Select(b => b["text"]?.GetValue<string>() ?? ""))
            : "";
        var version = document["model"]?.GetValue<string>() ?? "";
        var input = document["usage"]?["input_tokens"]?.GetValue<int>() ?? 0;
        var output = document["usage"]?["output_tokens"]?.GetValue<int>() ?? 0;
        return new ModelReply(content, version, input, output, input + output);
    }
}
