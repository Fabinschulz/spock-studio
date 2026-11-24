using System.Text.Json.Nodes;
using Mariliz.Application.Execution;
using Mariliz.Application.Integrations;
using Mariliz.Application.Management.Assistant;
using Mariliz.Application.Models;
using Mariliz.Application.Ports;
using Mariliz.Application.Protocol;
using Mariliz.Application.Timeline;
using Mariliz.Application.Workspace;

namespace Mariliz.Application.Management.Investigation;

// Só nomes de grupo e ids de fatos: nunca texto de fato, de hipótese nem a pergunta.
public sealed record InvestigationStep(string Id, string Title, string Detail, int Round, IReadOnlyList<string> Groups, IReadOnlyList<string> Facts);

public sealed record InvestigationLimitation(string Code, string Message);

public sealed record ProjectInvestigation(
    string InvestigationId,
    string RunId,
    DateTimeOffset AnsweredAt,
    int Rounds,
    bool UsedModel,
    AssistantAnswer Answer,
    IReadOnlyList<InvestigationStep> Path,
    IReadOnlyList<InvestigationLimitation> Limitations);

// Sem modelo configurado, a investigação ainda roda e lista os sinais, dizendo que o modelo não escreveu a análise.
public sealed class InvestigationService
{
    public const int WindowDays = InvestigationFacts.WindowDays;
    private const int MaxFacts = 300;

    /// <summary>
    /// O tempo que o runtime de IA recebe. Termina antes do <see cref="Deadline"/> de propósito: o runtime conclui com a limitação de
    /// tempo (e a resposta que já tem), em vez de o motor cortar a conversa. O host espera ainda mais (engine/timeouts.rs).
    /// </summary>
    public const int RuntimeBudgetMilliseconds = 120_000;

    private readonly ManagementAssistant _assistant;
    private readonly IntegrationHub _hub;
    private readonly WorkspaceAuthorizer _workspace;
    private readonly IInvestigationRuntime _runtime;
    private readonly IClock _clock;
    private readonly IModelAccess? _models;
    private readonly InvestigationFacts _facts;
    private readonly InvestigationMemory _memory;
    private readonly InvestigationJournal _journal;

    public InvestigationService(
        ManagementAssistant assistant,
        IntegrationHub hub,
        WorkspaceAuthorizer workspace,
        IInvestigationRuntime runtime,
        IClock clock,
        IRunStore? runs = null,
        IModelAccess? models = null,
        ProjectTimelineService? timeline = null,
        DecisionMemory? memory = null,
        string? indexRoot = null)
    {
        _assistant = assistant;
        _hub = hub;
        _workspace = workspace;
        _runtime = runtime;
        _clock = clock;
        _models = models;
        _facts = new InvestigationFacts(runs, timeline, clock);
        _memory = new InvestigationMemory(memory, hub, indexRoot);
        _journal = new InvestigationJournal(runs, workspace, clock);
    }

    /// <summary>Prazo da conversa com o runtime: até quatro chamadas de modelo, cada uma com o seu prazo.</summary>
    internal TimeSpan Deadline { get; init; } = TimeSpan.FromSeconds(150);

    public async Task<ProjectInvestigation> InvestigateAsync(string projectId, string question, CancellationToken ct)
    {
        var cleanQuestion = ManagementAssistant.CleanQuestion(question);
        if (!_workspace.IsAuthorized(projectId)) throw EngineErrors.ProjectNotAuthorized();

        var startedAt = _clock.UtcNow;
        var investigationId = "inv-" + Guid.NewGuid().ToString("N")[..12];
        var executionId = "ex-" + Guid.NewGuid().ToString("N")[..12];
        ModelUsage? usage = null;
        try
        {
            var pack = Pack(projectId);
            var request = InvestigationWire.Request(investigationId, executionId, projectId, cleanQuestion, pack);
            var documents = _memory.Documents(projectId);
            if (_memory.Request(documents) is { } memoryRequest) request["memory"] = memoryRequest;
            var grant = _models is { Configured: true } ? _models.Open(executionId, ModelPurposes.Assistant) : null;
            request["policy"]!["modelAllowed"] = grant is not null;
            request["limits"]!["maxTokens"] = grant?.TokenBudget ?? 0;
            if (grant is not null) request["broker"] = new JsonObject { ["url"] = grant.Url, ["token"] = grant.Token };

            JsonObject ack;
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
            deadline.CancelAfter(Deadline);
            try
            {
                ack = await _runtime.InvestigateAsync(request, _ => { }, deadline.Token);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                throw new EngineException("INVESTIGATION_TIMEOUT", "A investigação passou do prazo.");
            }
            finally
            {
                if (grant is not null) usage = _models!.Close(executionId);
            }

            var result = Verify(ack, pack, investigationId, grant is not null, usage, _models?.Calls(executionId), documents);
            _hub.Audit.Record("management.investigation_answered", new JsonObject
            {
                ["projectId"] = projectId,
                ["rounds"] = result.Rounds,
                ["consulted"] = result.Answer.Evidence.Count,
                ["rejected"] = result.Answer.Rejected.Count,
                ["modelCalls"] = usage?.Calls ?? 0,
                ["inputTokens"] = usage?.InputTokens ?? 0,
                ["outputTokens"] = usage?.OutputTokens ?? 0
            });
            _journal.Save(projectId, investigationId, executionId, startedAt, result, null, usage);
            return result;
        }
        catch (EngineException ex)
        {
            _journal.Save(projectId, investigationId, executionId, startedAt, null, ex.Code, usage);
            throw;
        }
    }

    public EvidencePack Pack(string projectId)
    {
        var basePack = _assistant.Evidence(projectId);
        var facts = new List<EvidenceFact>(basePack.Facts);
        facts.AddRange(_facts.For(projectId));

        var sendable = facts.Where(InvestigationWire.Sendable).Take(MaxFacts)
            .Select(fact => fact with { Group = InvestigationWire.Clip(fact.Group, 40), Text = InvestigationWire.Clip(fact.Text, 1200) }).ToList();
        var withheld = basePack.Withheld.ToList();
        if (sendable.Count < facts.Count) withheld.Add($"{facts.Count - sendable.Count} fatos ficaram de fora por não caberem no contrato da investigação.");
        return basePack with { Facts = sendable, Withheld = withheld };
    }

    // A resposta é conferida contra os fatos consultados, não contra o pacote inteiro: citar um fato que a investigação nunca olhou não passa.
    private ProjectInvestigation Verify(JsonObject ack, EvidencePack pack, string investigationId, bool usedModel, ModelUsage? usage, IReadOnlyList<ModelCallRecord>? calls,
        IReadOnlyList<MemoryDocument> documents)
    {
        var answer = InvestigationWire.ReadAnswer(ack, investigationId);

        pack = pack with { Facts = [.. pack.Facts, .. InvestigationWire.Retrieved(ack["retrieved"], documents)] };
        var known = pack.Facts.Select(fact => fact.Id).ToHashSet(StringComparer.Ordinal);
        var consulted = InvestigationWire.Strings(ack["consulted"]).Where(known.Contains).Distinct(StringComparer.Ordinal).ToHashSet(StringComparer.Ordinal);
        var path = InvestigationWire.Path(ack["path"], known);
        var consultedPack = pack with { Facts = pack.Facts.Where(fact => consulted.Contains(fact.Id)).ToList() };

        var verified = AnswerVerifier.Verify(answer.ToJsonString(), consultedPack);
        var rejected = verified.Rejected.Concat(InvestigationWire.Rejected(ack["rejected"])).ToList();
        var limitations = InvestigationWire.Limitations(ack["limitations"]);
        var rounds = InvestigationWire.Rounds(ack, path);

        var answerOut = verified.ToAnswer(
            pack.ProjectId, _clock.UtcNow, consultedPack.Facts, calls?.LastOrDefault()?.ModelVersion ?? "",
            usage?.InputTokens ?? 0, usage?.OutputTokens ?? 0, rejected);
        return new ProjectInvestigation(investigationId, investigationId, _clock.UtcNow, rounds, usedModel, answerOut, path, limitations);
    }
}
