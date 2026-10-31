using System.Text.Json.Nodes;
using Mariliz.Application.Execution;
using Mariliz.Application.Workflows;

namespace Mariliz.Application.Protocol;

/// <summary>Traduz <c>workflow.start</c> e <c>workflow.resume</c> para o <see cref="WorkflowService"/>.</summary>
internal sealed class WorkflowRequestHandler(WorkflowService service)
{
    public async Task<JsonObject> HandleAsync(string method, RequestParams p, CancellationToken ct) => method switch
    {
        ProtocolMethods.WorkflowStart => RunStarted(await service.StartAsync(p.Str("projectId"), p.ObjectOrEmpty("input"), ct)),
        ProtocolMethods.WorkflowResume => RunStarted(await service.ResumeAsync(p.Str("projectId"), p.Str("runId"))),
        _ => throw new InvalidOperationException($"Método sem tratador: {method}.")
    };

    public Task ShutdownAsync() => service.ShutdownAsync();

    public string AiRuntimeStatus() => service.AiRuntimeStatus();

    private static JsonObject RunStarted(RunHandle run) => new() { ["runId"] = run.RunId };
}
