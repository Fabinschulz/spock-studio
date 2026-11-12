using System.Text.Json.Nodes;
using Mariliz.Application.Execution;

namespace Mariliz.Application.Protocol;

/// <summary>Traduz <c>run.*</c>: retrato, cancelamento, listagem e detalhe, válidos para execuções de qualquer jornada.</summary>
internal sealed class RunRequestHandler(RunQueries runs)
{
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
        _ => throw new InvalidOperationException($"Método sem tratador: {method}.")
    };

    private static IReadOnlyList<string> Known(IReadOnlyList<string> values, IReadOnlyList<string> allowed, string name)
    {
        var unknown = values.FirstOrDefault(value => !allowed.Contains(value));
        return unknown is null ? values : throw new ParamException($"'{name}' tem um valor desconhecido.");
    }
}
