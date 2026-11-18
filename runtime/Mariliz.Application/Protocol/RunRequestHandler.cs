using System.Text.Json.Nodes;
using Mariliz.Application.Execution;

namespace Mariliz.Application.Protocol;

internal sealed class RunRequestHandler(RunQueries runs) : IRequestHandler
{
    public IReadOnlyList<string> Methods => ProtocolMethods.RunControl;

    Task<JsonObject> IRequestHandler.HandleAsync(string method, RequestParams p, CancellationToken ct) => Task.FromResult(Handle(method, p));

    public JsonObject Handle(string method, RequestParams p) => method switch
    {
        ProtocolMethods.Snapshot => runs.Snapshot(p.Str("runId"), p.OptionalInt("sinceSeq"), p.OptionalStr("projectId")),
        ProtocolMethods.Cancel => new JsonObject { ["cancelled"] = runs.Cancel(p.Str("runId"), p.OptionalStr("projectId")) },
        ProtocolMethods.RunList => runs.Catalog.List(
            p.OptionalStr("projectId"),
            Known(p.StringList("kinds"), RunCatalog.Kinds, "kinds"),
            Known(p.StringList("states"), RunCatalog.States, "states"),
            p.OptionalInt("limit"),
            p.OptionalStr("cursor")),
        ProtocolMethods.RunDetail => runs.Catalog.Detail(p.Str("projectId"), p.Str("runId")),
        ProtocolMethods.RunLinked => runs.Catalog.Linked(p.Str("projectId")),
        ProtocolMethods.RunLink => runs.Catalog.Link(p.Str("projectId"), p.Str("runId"), p.Str("externalId"), p.OptionalStr("integrationId")),
        ProtocolMethods.RunUnlink => runs.Catalog.Unlink(
            p.Str("projectId"), p.Str("runId"), p.Str("integrationId"), p.Str("kind"), p.Str("externalId")),
        _ => throw new InvalidOperationException($"Método sem tratador: {method}.")
    };

    private static IReadOnlyList<string> Known(IReadOnlyList<string> values, IReadOnlyList<string> allowed, string name)
    {
        var unknown = values.FirstOrDefault(value => !allowed.Contains(value));
        return unknown is null ? values : throw new ParamException($"'{name}' tem um valor desconhecido.");
    }
}
