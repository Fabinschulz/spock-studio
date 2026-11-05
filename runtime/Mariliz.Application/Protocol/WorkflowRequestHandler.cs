using System.Text.Json.Nodes;
using Mariliz.Application.Execution;
using Mariliz.Application.Workflows;
using Mariliz.Core.Text;

namespace Mariliz.Application.Protocol;

/// <summary>Traduz <c>workflow.start</c> e <c>workflow.resume</c> para o <see cref="WorkflowService"/>.</summary>
internal sealed class WorkflowRequestHandler(WorkflowService service)
{
    public async Task<JsonObject> HandleAsync(string method, RequestParams p, CancellationToken ct) => method switch
    {
        ProtocolMethods.WorkflowStart => RunStarted(await service.StartAsync(p.Str("projectId"), p.ObjectOrEmpty("input"), ct)),
        ProtocolMethods.WorkflowResume => RunStarted(await service.ResumeAsync(p.Str("projectId"), p.Str("runId"), Decision(p))),
        _ => throw new InvalidOperationException($"Método sem tratador: {method}.")
    };

    /// <summary>
    /// A decisão da pessoa: o <c>approvalId</c> que ela viu e as ações que aprova. Sem <c>actionIds</c>, nenhuma ação é aprovada
    /// (aprovar é sempre explícito).
    /// </summary>
    private static ResumeDecision Decision(RequestParams p)
    {
        var approvalId = p.OptionalStr("approvalId");
        if (approvalId is not null && !Ids.IsApprovalId(approvalId)) throw new ParamException("'approvalId' não tem o formato esperado.");
        var actionIds = p.StringList("actionIds");
        if (actionIds.Count > 32 || actionIds.Any(id => !ActionGovernance.IsActionId(id))) throw new ParamException("'actionIds' tem um identificador fora do formato.");
        return new ResumeDecision(approvalId, actionIds);
    }

    public Task ShutdownAsync() => service.ShutdownAsync();

    public string AiRuntimeStatus() => service.AiRuntimeStatus();

    private static JsonObject RunStarted(RunHandle run) => new() { ["runId"] = run.RunId };
}
