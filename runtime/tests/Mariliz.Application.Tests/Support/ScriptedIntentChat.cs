using Mariliz.Application.Models;

namespace Mariliz.Application.Tests.Support;

// O provedor de modelo do reconhecimento de intenção, escrito no teste: devolve o texto que o teste manda, demora ou falha como ele pedir.
internal sealed class ScriptedIntentChat : IModelChatTransport
{
    public const string NoCapability = """{"capabilityId": null, "alternatives": [], "reason": "nenhuma serve"}""";

    public Func<ChatRequest, string> Reply { get; set; } = _ => NoCapability;
    public Exception? Throw { get; set; }
    public bool Hang { get; set; }
    public int InputTokens { get; set; } = 1200;
    public int OutputTokens { get; set; } = 30;
    public List<ChatRequest> Seen { get; } = [];
    public List<string> Models { get; } = [];

    public void Say(string capabilityId, params string[] alternatives) =>
        Reply = _ => $$"""{"capabilityId": "{{capabilityId}}", "alternatives": [{{string.Join(", ", alternatives.Select(id => $"\"{id}\""))}}], "reason": "r"}""";

    public async Task<ChatTransportResult> SendChatAsync(Uri endpoint, string key, string model, ChatRequest request, CancellationToken ct)
    {
        Seen.Add(request);
        Models.Add(model);
        if (Hang) await Task.Delay(Timeout.Infinite, ct);
        if (Throw is not null) throw Throw;
        return new ChatTransportResult(Reply(request), "modelo-roteirizado", InputTokens, OutputTokens);
    }
}
