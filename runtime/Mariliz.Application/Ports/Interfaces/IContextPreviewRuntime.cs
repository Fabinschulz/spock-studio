using System.Text.Json.Nodes;

namespace Mariliz.Application.Ports;

// Porta à parte de IAiRuntime: só lê o índice e monta as mensagens, sem execução, modelo nem evento.
public interface IContextPreviewRuntime
{
    // A resposta volta como veio: quem chama confere antes de confiar.
    Task<JsonObject> PreviewContextAsync(JsonObject request, CancellationToken ct);
}
