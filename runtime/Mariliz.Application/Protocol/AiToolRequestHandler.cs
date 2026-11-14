using System.Text.Json.Nodes;
using Mariliz.Application.AiTools;
using Mariliz.Application.Integrations;

namespace Mariliz.Application.Protocol;

internal sealed class AiToolRequestHandler(AiToolService service)
{
    public async Task<JsonObject> HandleAsync(string method, CancellationToken ct) => method switch
    {
        ProtocolMethods.AiToolList => new JsonObject { ["tools"] = IntegrationJson.Node(await service.ListAsync(ct)) },
        _ => throw new InvalidOperationException($"Método sem tratador: {method}.")
    };
}
