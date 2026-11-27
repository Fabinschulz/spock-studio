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
        // O que entrou no cache vem em campos próprios e não está em input_tokens: sem somar, o teto de tokens e o uso mostrado
        // ficam abaixo do que foi gasto (o gateway chegou a devolver 5 de entrada com 42 gravados no cache).
        var usage = document["usage"];
        var input = Count(usage?["input_tokens"]) + Count(usage?["cache_creation_input_tokens"]) + Count(usage?["cache_read_input_tokens"]);
        var output = Count(usage?["output_tokens"]);
        return new ModelReply(content, version, input, output, input + output);
    }

    private static int Count(JsonNode? node) => node is JsonValue value && value.TryGetValue<int>(out var number) && number > 0 ? number : 0;
}
