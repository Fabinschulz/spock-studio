using System.Text.Json.Nodes;

namespace Mariliz.Application.Ports;

// Porta à parte de IAiRuntime porque não tem pausa, aprovação nem checkpoint.
public interface IInvestigationRuntime
{
    // O ack final é devolvido como veio: quem chama confere antes de confiar.
    Task<JsonObject> InvestigateAsync(JsonObject request, Action<JsonObject> onEvent, CancellationToken ct);
}
