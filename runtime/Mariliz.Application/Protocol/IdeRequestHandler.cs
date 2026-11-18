using System.Text.Json.Nodes;
using Mariliz.Application.Mcp;

namespace Mariliz.Application.Protocol;

internal sealed class IdeRequestHandler(IdeConnection ide) : IRequestHandler
{
    public IReadOnlyList<string> Methods => ProtocolMethods.Ide;

    public Task<JsonObject> HandleAsync(string method, RequestParams p, CancellationToken ct) => Task.FromResult(ide.Describe());
}
