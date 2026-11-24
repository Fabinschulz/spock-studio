using System.Text.Json.Nodes;
using Mariliz.Application.Execution;
using Mariliz.Application.Integrations;
using Mariliz.Application.Management.Assistant;
using Mariliz.Application.Ports;
using Mariliz.Application.Protocol;
using Mariliz.Application.Workspace;
using Mariliz.Core.Policy;
using Mariliz.Application.Audit;

namespace Mariliz.Application.Management;

// Retrato da execução no instante do registro, não o estado atual.
public sealed record DecisionRun(string RunId, string Kind, string State, DateTimeOffset StartedAt);

// O texto exatamente como o motor o escreveu no instante da decisão.
public sealed record DecisionFact(string Id, string Group, string Text);

// Não se edita nem se apaga: é a trilha de por que se decidiu o que se decidiu.
public sealed record ManagementDecision(
    string Id,
    string ProjectId,
    DateTimeOffset RecordedAt,
    string Title,
    string Decision,
    string? Rationale,
    IReadOnlyList<DecisionRun> Runs,
    IReadOnlyList<DecisionFact> Facts,
    IReadOnlyList<string> Withheld);

public interface IDecisionStore
{
    IReadOnlyList<ManagementDecision> List(string projectId);
    ManagementDecision? Get(string projectId, string decisionId);
    void Save(ManagementDecision decision);
}

// O texto da pessoa passa pela redação de segredos; os fatos vêm do motor, nunca do pedido.
public sealed class DecisionService(
    WorkspaceAuthorizer workspace,
    IDecisionStore store,
    IRunStore runs,
    Func<string, EvidencePack> evidence,
    IAuditLog audit,
    IClock clock)
{
    public const int MaxTitle = 120;
    public const int MaxDecision = 1000;
    public const int MaxRationale = 2000;
    public const int MaxRuns = 10;
    public const int MaxPerProject = 500;

    public (ManagementDecision Decision, bool Redacted) Record(
        string projectId, string title, string decision, string? rationale, IReadOnlyList<string> runIds)
    {
        if (!workspace.IsAuthorized(projectId)) throw EngineErrors.ProjectNotAuthorized();

        var cleanTitle = Required(title, "title", MaxTitle);
        var cleanDecision = Required(decision, "decision", MaxDecision);
        var cleanRationale = string.IsNullOrWhiteSpace(rationale) ? null : Limited(rationale, "rationale", MaxRationale);
        var distinctRuns = runIds.Distinct(StringComparer.Ordinal).ToList();
        if (distinctRuns.Count > MaxRuns) throw new ParamException($"Uma decisão liga a até {MaxRuns} execuções.");
        if (store.List(projectId).Count >= MaxPerProject)
            throw new EngineException("DECISION_LIMIT", $"Um projeto guarda até {MaxPerProject} decisões.");

        var related = distinctRuns.Select(id => Run(projectId, id)).ToList();
        var redactedTitle = SecretScanner.RedactLines(cleanTitle);
        var redactedDecision = SecretScanner.RedactLines(cleanDecision);
        var redactedRationale = cleanRationale is null ? null : SecretScanner.RedactLines(cleanRationale);
        var redacted = redactedTitle != cleanTitle || redactedDecision != cleanDecision || redactedRationale != cleanRationale;

        var pack = evidence(projectId);
        var record = new ManagementDecision(
            "dec-" + Guid.NewGuid().ToString("N")[..12], projectId, clock.UtcNow, redactedTitle, redactedDecision, redactedRationale,
            related, pack.Facts.Select(fact => new DecisionFact(fact.Id, fact.Group, fact.Text)).ToList(), pack.Withheld);
        store.Save(record);

        // Na trilha vão os números e identificadores, nunca o texto da decisão.
        audit.Record(AuditActions.DecisionRecorded, new JsonObject
        {
            ["projectId"] = projectId,
            ["decisionId"] = record.Id,
            ["runs"] = related.Count,
            ["facts"] = record.Facts.Count,
            ["redacted"] = redacted
        });
        return (record, redacted);
    }

    public JsonArray List(string projectId)
    {
        if (!workspace.IsAuthorized(projectId)) throw EngineErrors.ProjectNotAuthorized();
        return new JsonArray(store.List(projectId).OrderByDescending(item => item.RecordedAt)
            .Select(item => (JsonNode)new JsonObject
            {
                ["id"] = item.Id,
                ["recordedAt"] = IntegrationJson.Node(item.RecordedAt),
                ["title"] = item.Title,
                ["decision"] = item.Decision,
                ["rationale"] = item.Rationale,
                ["runs"] = IntegrationJson.Node(item.Runs),
                ["factCount"] = item.Facts.Count
            }).ToArray());
    }

    public IReadOnlyList<ManagementDecision> Recorded(string projectId, DateTimeOffset from, DateTimeOffset to)
    {
        if (!workspace.IsAuthorized(projectId)) throw EngineErrors.ProjectNotAuthorized();
        return store.List(projectId).Where(item => item.RecordedAt >= from && item.RecordedAt <= to)
            .OrderByDescending(item => item.RecordedAt).ToList();
    }

    public ManagementDecision Get(string projectId, string decisionId)
    {
        if (!workspace.IsAuthorized(projectId)) throw EngineErrors.ProjectNotAuthorized();
        ManagementDecision? found;
        try
        {
            found = store.Get(projectId, decisionId);
        }
        catch (EngineException)
        {
            // O id vem do cliente e vira nome de arquivo: formato inválido é "não existe".
            found = null;
        }

        return found is not null && found.ProjectId == projectId
            ? found
            : throw new EngineException("DECISION_NOT_FOUND", "Decisão não encontrada.");
    }

    private DecisionRun Run(string projectId, string runId)
    {
        RunJournal? journal;
        try
        {
            journal = runs.LoadJournal(projectId, runId);
        }
        catch (ArgumentException)
        {
            journal = null;
        }

        return journal is null
            ? throw new EngineException("DECISION_RUN_NOT_FOUND", "Uma das execuções informadas não existe neste projeto.")
            : new DecisionRun(journal.RunId, journal.Kind, journal.State, journal.StartedAt);
    }

    private static string Required(string value, string name, int max)
    {
        var text = value.Trim();
        if (text.Length == 0) throw new ParamException($"'{name}' não pode ficar em branco.");
        return Limited(text, name, max);
    }

    private static string Limited(string value, string name, int max)
    {
        var text = value.Trim();
        return text.Length <= max ? text : throw new ParamException($"'{name}' passa de {max} caracteres.");
    }
}
