using Mariliz.Application.Validation;
using System.Text.Json.Nodes;

namespace Mariliz.Application.Execution;

/// <summary>Publica <c>eval.started</c> e <c>eval.completed</c> na execução, com os mesmos valores de <c>EvalResult</c>.</summary>
internal sealed class RunEvalProgress(RunHandle run) : IEvalProgress
{
    public void Started(string id, string name, string kind) =>
        run.Publish("eval.started", new JsonObject { ["id"] = id, ["name"] = name, ["kind"] = kind });

    public void Completed(string id, string status) =>
        run.Publish("eval.completed", new JsonObject { ["id"] = id, ["status"] = status });
}
