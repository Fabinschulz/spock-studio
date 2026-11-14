using System.Net;
using System.Text.Json.Nodes;
using Mariliz.Application.Execution;
using Mariliz.Application.Models;

namespace Mariliz.Infrastructure.ModelFormats;

// O modelo vai no endereço, então só entra nele o que está na lista do provedor.
internal sealed class GoogleFormat : IModelFormat
{
    public Uri Url(ModelProvider provider, string model)
    {
        if (provider.Models.All(option => option.Id != model))
            throw new EngineException("MODEL_CHOICE_DENIED", "Esse modelo não está disponível neste provedor.");
        return new Uri($"{provider.Endpoint.AbsoluteUri.TrimEnd('/')}/{model}:generateContent");
    }

    public void Authenticate(HttpRequestMessage request, string key) => request.Headers.Add("x-goog-api-key", key);

    public JsonObject Body(string model, IReadOnlyList<ChatMessage> messages, int maxTokens, bool jsonObject)
    {
        var layout = ChatLayout.Split(messages);
        var generation = new JsonObject { ["maxOutputTokens"] = maxTokens };
        if (jsonObject) generation["responseMimeType"] = "application/json";

        var body = new JsonObject
        {
            ["contents"] = new JsonArray(layout.Conversation.Select(m => (JsonNode)new JsonObject
            {
                ["role"] = m.Role == "assistant" ? "model" : "user",
                ["parts"] = new JsonArray(new JsonObject { ["text"] = m.Content })
            }).ToArray()),
            ["generationConfig"] = generation
        };
        if (layout.System is not null)
            body["systemInstruction"] = new JsonObject { ["parts"] = new JsonArray(new JsonObject { ["text"] = layout.System }) };
        return body;
    }

    // Sem candidato, o pedido foi barrado antes de gerar texto: não há resposta a devolver.
    public ModelReply Read(JsonObject document)
    {
        if (document["candidates"] is not JsonArray { Count: > 0 } candidates)
            throw new EngineException("MODEL_INVALID_RESPONSE", "O modelo respondeu fora do contrato.");

        var text = candidates[0]?["content"]?["parts"] is JsonArray parts
            ? string.Concat(parts.OfType<JsonObject>().Select(part => part["text"]?.GetValue<string>() ?? ""))
            : "";
        var usage = document["usageMetadata"];
        var input = usage?["promptTokenCount"]?.GetValue<int>() ?? 0;
        // O raciocínio do modelo é saída cobrada, mas vem em campo à parte.
        var output = (usage?["candidatesTokenCount"]?.GetValue<int>() ?? 0) + (usage?["thoughtsTokenCount"]?.GetValue<int>() ?? 0);
        return new ModelReply(text, document["modelVersion"]?.GetValue<string>() ?? "", input, output, usage?["totalTokenCount"]?.GetValue<int>() ?? input + output);
    }

    // O Google responde 400, e não 401, a uma chave inválida.
    public bool RefusesKey(HttpStatusCode status, string body) =>
        status is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden
        || (status == HttpStatusCode.BadRequest && body.Contains("API_KEY_INVALID", StringComparison.Ordinal));
}
