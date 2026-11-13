using System.Text.Json.Nodes;
using Mariliz.Application.Execution;
using Mariliz.Application.Integrations;
using Mariliz.Application.Management.Assistant;
using Mariliz.Application.Ports;
using Mariliz.Application.Protocol;
using Mariliz.Application.Workspace;
using Mariliz.Core.Policy;

namespace Mariliz.Application.Management;

/// <summary>Uma execução ligada a uma decisão, como era quando a decisão foi registrada. Só identificador, tipo, estado e início.</summary>
public sealed record DecisionRun(string RunId, string Kind, string State, DateTimeOffset StartedAt);

/// <summary>Um fato do pacote de evidências no instante da decisão, com o texto exatamente como o motor o escreveu.</summary>
public sealed record DecisionFact(string Id, string Group, string Text);

/// <summary>
/// Uma decisão gerencial registrada pela pessoa: o que foi decidido e por quê, com os fatos que o motor tinha naquele instante
/// e as execuções relacionadas. Não se edita nem se apaga: é a trilha de por que se decidiu o que se decidiu.
/// </summary>
/// <param name="Facts">Os fatos do pacote de evidências no instante do registro (o que valia nesse dia, não o que vale hoje).</param>
/// <param name="Withheld">O que ficou de fora do pacote de propósito, como no pacote que vai ao assistente.</param>
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

/// <summary>Onde as decisões de um projeto ficam. Um arquivo por decisão, na pasta do projeto: decisões de projetos diferentes não se misturam.</summary>
public interface IDecisionStore
{
    IReadOnlyList<ManagementDecision> List(string projectId);
    ManagementDecision? Get(string projectId, string decisionId);
    void Save(ManagementDecision decision);
}

/// <summary>
/// Registra e lê decisões gerenciais. O texto é da pessoa e passa pela mesma redação de segredos de tudo o que o motor guarda:
/// uma chave colada por engano não fica gravada, e a resposta diz que houve redação. Os fatos vêm do motor, nunca do pedido.
/// </summary>
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

    /// <summary>
    /// Registra a decisão com os fatos de agora e as execuções informadas. Devolve o registro completo e se algum trecho foi
    /// redigido por parecer um segredo.
    /// </summary>
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
        audit.Record("decision.recorded", new JsonObject
        {
            ["projectId"] = projectId,
            ["decisionId"] = record.Id,
            ["runs"] = related.Count,
            ["facts"] = record.Facts.Count,
            ["redacted"] = redacted
        });
        return (record, redacted);
    }

    /// <summary>As decisões do projeto, da mais recente para a mais antiga, sem os fatos (que vão em <see cref="Get"/>).</summary>
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
