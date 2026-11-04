using System.Text.Json.Nodes;
using Mariliz.Application.Execution;

namespace Mariliz.Application.Protocol;

/// <summary>Traduz <c>run.snapshot</c> e <c>run.cancel</c>, válidos para execuções de qualquer jornada.</summary>
internal sealed class RunRequestHandler(RunQueries runs)
{
    public JsonObject Handle(string method, RequestParams p) => method switch
    {
        ProtocolMethods.Snapshot => runs.Snapshot(p.Str("runId"), p.OptionalInt("sinceSeq")),
        ProtocolMethods.Cancel => new JsonObject { ["cancelled"] = runs.Cancel(p.Str("runId")) },
        _ => throw new InvalidOperationException($"Método sem tratador: {method}.")
    };
}
