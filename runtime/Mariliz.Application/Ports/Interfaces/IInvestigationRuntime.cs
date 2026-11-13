using System.Text.Json.Nodes;

namespace Mariliz.Application.Ports;

/// <summary>
/// O workflow de investigação de gestão no runtime de IA. É uma porta à parte de <see cref="IAiRuntime"/> porque não tem
/// pausa, aprovação nem checkpoint: o motor manda a pergunta e os fatos, e a última linha da resposta é o resultado.
/// </summary>
public interface IInvestigationRuntime
{
    /// <summary>
    /// Envia um <c>ai.investigation.start.v1</c>. Cada <c>ai.event.v1</c> chega em <paramref name="onEvent"/> enquanto o grafo anda
    /// e o <c>ai.investigation.ack.v1</c> final é devolvido como veio: quem chama confere antes de confiar.
    /// </summary>
    Task<JsonObject> InvestigateAsync(JsonObject request, Action<JsonObject> onEvent, CancellationToken ct);
}
