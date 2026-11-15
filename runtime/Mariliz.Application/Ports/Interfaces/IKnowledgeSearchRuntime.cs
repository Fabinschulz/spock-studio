using System.Text.Json.Nodes;

namespace Mariliz.Application.Ports;

// Só consulta o índice que o aplicativo montou: não sincroniza, não executa e não chama modelo.
public interface IKnowledgeSearchRuntime
{
    // A resposta volta como veio: quem chama confere antes de confiar.
    Task<JsonObject> SearchKnowledgeAsync(JsonObject request, CancellationToken ct);
}
