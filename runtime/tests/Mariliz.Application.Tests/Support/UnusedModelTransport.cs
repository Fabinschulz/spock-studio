using Mariliz.Application.Models;

namespace Mariliz.Application.Tests.Support;

// O transporte do teste de conexão, que o reconhecimento de intenção nunca usa.
internal sealed class UnusedModelTransport : IModelTransport
{
    public Task<ModelTransportResult> SendAsync(Uri endpoint, string key, string prompt, int maxTokens, CancellationToken ct) =>
        throw new InvalidOperationException("O reconhecimento de intenção só conversa pelo chat.");
}
