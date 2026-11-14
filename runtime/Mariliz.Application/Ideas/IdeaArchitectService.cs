using System.Text.Json;
using System.Text.Json.Nodes;
using Mariliz.Application.Execution;
using Mariliz.Application.Models;
using Mariliz.Application.Ports;
using Mariliz.Application.Protocol;
using Mariliz.Application.Workspace;

namespace Mariliz.Application.Ideas;

// Da ideia ao projeto em etapas: o modelo escreve, o motor confere e a pessoa aprova cada etapa antes da seguinte. Gravar os
// documentos segue o caminho do Harness: diferença, aprovação pelo id do plano, cópia, diário e reversão.
public sealed class IdeaArchitectService(
    WorkspaceAuthorizer workspace,
    ModelGateway models,
    IProjectFilesFactory filesFactory,
    IRunStore store,
    IClock clock,
    RunTracker tracker)
{
    public const int MinIdea = 20;
    public const int MaxIdea = 4000;
    public const int MaxAttempts = 2;

    public async Task<IdeaDraft> DraftAsync(string projectId, string stage, IdeaArtifacts approved, CancellationToken ct)
    {
        Open(projectId);
        if (!IdeaStages.All.Contains(stage)) throw new ParamException($"Etapa desconhecida: {stage}.");
        var artifacts = Clean(approved);
        RequireApproved(stage, artifacts);

        var startedAt = clock.UtcNow;
        IdeaDraft? draft = null;
        var usage = new JournalModelUsage();
        try
        {
            for (var attempt = 1; attempt <= MaxAttempts && draft is not { Valid: true }; attempt++)
            {
                var completion = await models.ChatAsync(IdeaPrompts.Request(stage, artifacts, draft?.Checks.Where(check => !check.Passed).ToList()), ct);
                usage.Calls++;
                usage.InputTokens += completion.InputTokens;
                usage.OutputTokens += completion.OutputTokens;
                var artifact = Parse(completion.Text);
                var checks = artifact is null ? [new IdeaCheck("json", "A resposta é um objeto JSON", false, null)] : Check(stage, artifact, artifacts);
                draft = new IdeaDraft(stage, artifact, checks, checks.All(check => check.Passed), attempt, usage.InputTokens, usage.OutputTokens);
            }
        }
        catch (EngineException ex)
        {
            RecordDraft(projectId, stage, startedAt, usage, null, ex.Code);
            throw;
        }

        RecordDraft(projectId, stage, startedAt, usage, draft, null);
        return draft!;
    }

    public IdeaPlan Plan(string projectId, IdeaArtifacts approved) => Prepare(projectId, Open(projectId), approved).Plan;

    public RunHandle StartSave(string projectId, IdeaArtifacts approved, string planId)
    {
        var files = Open(projectId);
        var (plan, writes) = Prepare(projectId, files, approved);
        if (plan.PlanId != planId)
            throw new EngineException("STALE_PLAN", "Os documentos ou o projeto mudaram desde a revisão. Revise de novo antes de aprovar.");
        if (!tracker.TryStart(projectId, RunKinds.Idea, out var run) || run is null) throw EngineErrors.RunInProgress();

        run.Start(() =>
        {
            var journal = new RunJournal { RunId = run.RunId, ProjectId = projectId, Kind = RunKinds.Idea, PlanId = plan.PlanId, StartedAt = clock.UtcNow };
            var recorder = new RunJournalRecorder(run, journal, store);
            run.SetState(RunStates.Running);
            var error = new ApprovedWriter(store, files, WriteScope.Managed, OpSources.Write).Apply(run, journal, recorder, writes);

            journal.State = error is null ? RunStates.Completed : RunStates.Failed;
            journal.FinishedAt = clock.UtcNow;
            recorder.Save();
            run.Complete(journal.State, new JsonObject
            {
                ["runId"] = run.RunId,
                ["kind"] = RunKinds.Idea,
                ["outcome"] = journal.State,
                ["files"] = new JsonArray(journal.Ops.Select(op => (JsonNode)new JsonObject { ["path"] = op.Path, ["op"] = op.Op, ["status"] = op.Status }).ToArray()),
                ["error"] = error
            });
        });
        return run;
    }

    private static (IdeaPlan Plan, IReadOnlyList<ApprovedWrite> Writes) Prepare(string projectId, IProjectFiles files, IdeaArtifacts approved)
    {
        var artifacts = Clean(approved);
        if (artifacts.Backlog is null || !Check(IdeaStages.Backlog, artifacts.Backlog, artifacts).All(check => check.Passed))
            throw new ParamException("O backlog precisa estar aprovado e passar nas verificações antes de gravar.");
        RequireApproved(IdeaStages.Backlog, artifacts);

        var prepared = IdeaDocuments.Render(artifacts).Select(document => ReviewedChanges.Prepare(files, document.Path, document.Content)).ToList();
        var changes = prepared.Select(item => item.Change).ToList();
        return (new IdeaPlan(ReviewedChanges.PlanId("idea-", changes), projectId, changes), prepared.Select(item => item.Write).ToList());
    }

    // O que vem da interface é revalidado: uma etapa só serve de base para a seguinte se ainda passa nas verificações dela.
    private static void RequireApproved(string stage, IdeaArtifacts artifacts)
    {
        if (stage == IdeaStages.Specification) return;
        if (artifacts.Specification is null || !IdeaChecks.Specification(artifacts.Specification).All(check => check.Passed))
            throw new ParamException("A especificação precisa estar aprovada e passar nas verificações antes desta etapa.");
        if (stage == IdeaStages.Architecture) return;
        if (artifacts.Architecture is null || !IdeaChecks.Architecture(artifacts.Architecture, artifacts.Specification).All(check => check.Passed))
            throw new ParamException("A arquitetura precisa estar aprovada e passar nas verificações antes desta etapa.");
    }

    private static IReadOnlyList<IdeaCheck> Check(string stage, JsonObject artifact, IdeaArtifacts approved) => stage switch
    {
        IdeaStages.Specification => IdeaChecks.Specification(artifact),
        IdeaStages.Architecture => IdeaChecks.Architecture(artifact, approved.Specification!),
        _ => IdeaChecks.Backlog(artifact, approved.Specification!)
    };

    private static IdeaArtifacts Clean(IdeaArtifacts approved)
    {
        var idea = approved.Idea.Trim();
        if (idea.Length is < MinIdea or > MaxIdea)
            throw new ParamException($"Descreva a ideia com {MinIdea} a {MaxIdea} caracteres.");
        return approved with { Idea = idea };
    }

    private static JsonObject? Parse(string text)
    {
        try
        {
            return JsonNode.Parse(text) as JsonObject;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    // Como no assistente: o diário guarda etapa, tentativas e tokens, nunca o texto da ideia nem o que o modelo escreveu.
    private void RecordDraft(string projectId, string stage, DateTimeOffset startedAt, JournalModelUsage usage, IdeaDraft? draft, string? error)
    {
        var journal = new RunJournal
        {
            RunId = "idea-" + Guid.NewGuid().ToString("N")[..12],
            ProjectId = projectId,
            Kind = RunKinds.Idea,
            StartedAt = startedAt,
            FinishedAt = clock.UtcNow,
            State = draft is null ? RunStates.Failed : draft.Valid ? RunStates.Completed : RunStates.CompletedWithLimitations,
            Error = error,
            ModelUsage = usage.Calls > 0 ? usage : null
        };
        journal.Steps.Add(new JournalStep
        {
            Id = stage,
            Title = StageTitle(stage),
            Status = draft is { Valid: true } ? StepStatus.Completed : StepStatus.Failed,
            Detail = draft is null ? null : $"{draft.Checks.Count(check => check.Passed)} de {draft.Checks.Count} verificações em {draft.Attempts} tentativa(s)"
        });
        try
        {
            store.SaveJournal(journal);
        }
        catch (IOException)
        {
            // O registro é para consulta: falhar ao gravá-lo não tira da pessoa a etapa que o modelo já escreveu.
        }
    }

    private static string StageTitle(string stage) => stage switch
    {
        IdeaStages.Specification => "Especificação",
        IdeaStages.Architecture => "Arquitetura",
        _ => "Backlog"
    };

    private IProjectFiles Open(string projectId) =>
        workspace.TryGetRoot(projectId, out var root) ? filesFactory.Open(root) : throw EngineErrors.ProjectNotAuthorized();
}
