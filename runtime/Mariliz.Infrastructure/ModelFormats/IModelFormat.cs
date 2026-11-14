using System.Net;
using System.Text.Json.Nodes;
using Mariliz.Application.Models;

namespace Mariliz.Infrastructure.ModelFormats;

internal sealed record ModelReply(string Text, string Version, int InputTokens, int OutputTokens, int TotalTokens);

/// <summary>Como um provedor recebe o pedido e devolve a resposta. O formato vem do provedor, nunca do chamador.</summary>
internal interface IModelFormat
{
    Uri Url(ModelProvider provider, string model);

    void Authenticate(HttpRequestMessage request, string key);

    JsonObject Body(string model, IReadOnlyList<ChatMessage> messages, int maxTokens, bool jsonObject);

    ModelReply Read(JsonObject document);

    /// <summary>O corpo só serve para reconhecer o motivo: nunca é repetido, porque pode citar a chave.</summary>
    bool RefusesKey(HttpStatusCode status, string body) => status is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden;
}
