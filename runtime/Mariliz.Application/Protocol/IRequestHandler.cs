using System.Text.Json.Nodes;

namespace Mariliz.Application.Protocol;

// Cada área do protocolo atende um grupo fechado de métodos; a sessão só roteia pelo nome e anuncia os grupos presentes.
internal interface IRequestHandler
{
    IReadOnlyList<string> Methods { get; }

    Task<JsonObject> HandleAsync(string method, RequestParams p, CancellationToken ct);
}
