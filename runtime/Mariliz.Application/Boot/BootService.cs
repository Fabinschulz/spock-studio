using System.Text.Json.Nodes;
using Mariliz.Application.Audit;
using Mariliz.Application.Context;
using Mariliz.Application.Execution;
using Mariliz.Application.Integrations;
using Mariliz.Application.Ports;
using Mariliz.Application.Workspace;
using Mariliz.Core.Policy;

namespace Mariliz.Application.Boot;

/// <param name="Hint">O tipo de atividade que a tela já sabe (request, agent, assistant, idea).</param>
/// <param name="CapabilityId">Quando a pessoa abriu uma funcionalidade direto: não há o que interpretar.</param>
public sealed record BootStartRequest(string? ProjectId, string? Text, string? Hint, string? CapabilityId, AutonomyLevel Autonomy, string Origin, string? Source);

/// <summary>
/// Orquestra um Boot do começo ao fim: interpreta, confere fatos e permissão, monta o plano e executa os passos que são leitura
/// ou preparação, chamando os serviços que já existem pelo mesmo caminho que a interface usa. O que altera o projeto ou a nuvem
/// vira um passo de "abrir a tela". O estado é gravado a cada passo, então uma queda deixa um registro que o Boot de recuperação
/// retoma sem repetir o que já foi feito.
/// </summary>
public sealed class BootService(
    WorkspaceAuthorizer workspace,
    CapabilityRegistry registry,
    IntentRecognizer recognizer,
    BootFactsProbe probe,
    BootPlanner planner,
    IBootStore store,
    IClock clock,
    IAuditLog audit,
    ContextEngine? context = null,
    ModelIntentStage? modelStage = null)
{
    public const int MaxText = 2000;
    private static readonly string[] Hints = ["request", "agent", "assistant", "idea"];

    /// <summary>Executa um método do motor como a interface faria. Só o que o catálogo marca como execução direta passa por aqui.</summary>
    public delegate Task<JsonObject> Dispatch(string method, JsonObject parameters, CancellationToken ct);

    private IReadOnlySet<string> _routed = new HashSet<string>();
    private Dispatch? _dispatch;

    // A sessão do protocolo conhece as rotas, e o Boot não pode conhecer a sessão: ela entrega o que ele precisa depois de montada.
    public void BindEngine(IReadOnlySet<string> routedMethods, Dispatch dispatch)
    {
        if (_dispatch is not null) throw new InvalidOperationException("O Boot já está ligado ao motor.");
        _routed = routedMethods;
        _dispatch = dispatch;
    }

    public CapabilityRegistry Registry => registry;

    public async Task<BootRecord> StartAsync(BootStartRequest request, CancellationToken ct)
    {
        Validate(request);
        // Uma pessoa pode colar uma chave no pedido: a linha com cara de segredo sai antes de qualquer outro uso do texto
        // (reconhecer a intenção, gravar o registro, o plano), com o mesmo detector que o motor usa nas outras leituras.
        var text = request.Text is null ? null : SecretScanner.RedactLines(request.Text);
        request = request with { Text = text };
        var record = New(BootMode.Request, request.ProjectId, request.Autonomy, request.Origin, request.Source);
        record.RequestRedacted = text is not null && text.Contains(SecretScanner.RedactedLine, StringComparison.Ordinal);
        record.Request = request.Text?.Trim();
        record.Hint = request.Hint;
        Begin(record, "Pedido recebido.");

        var facts = Facts(record);
        var lexicon = recognizer.Recognize(new IntentRequest(request.Text, request.Hint, request.CapabilityId, facts.ProjectAuthorized));
        try
        {
            record.Intent = await RefineWithModelAsync(record, lexicon, facts, ct);
        }
        catch (OperationCanceledException)
        {
            record.Intent = lexicon;
            return Interrupt(record);
        }
        // O estado do estágio do modelo só entra na nota quando houve chamada; o que não chamou continua com a nota de sempre.
        var staged = record.Intent.ModelStage is ModelStages.Used or ModelStages.Failed ? $" (modelo: {record.Intent.ModelStage})" : "";
        Checkpoint(record, BootStates.Interpreting, null, $"Intenção: {record.Intent.Resolution}{staged}.");

        if (record.Intent.Resolution != IntentResolution.Resolved)
            return Finish(record, BootResults.Unresolved(record.Intent, registry, facts, registry.Resolve(facts)), record.Intent.Resolution == IntentResolution.NeedsClarification ? BootStates.AwaitingClarification : BootStates.Completed);

        var capability = registry.Find(record.Intent.CapabilityId!)!;
        var status = registry.Resolve(capability, facts);
        var decision = PermissionEvaluator.Evaluate(capability, status, request.Autonomy);
        var planned = planner.ForCapability(record.Intent, capability, status, decision, facts);
        record.Plan = planned.Plan;
        record.Steps = planned.Steps;
        Checkpoint(record, BootStates.Planned, null, $"Plano com {planned.Steps.Count} passos.");

        if (decision.Verdict == PermissionVerdict.Denied)
        {
            var blocked = BootResults.Blocked(capability, status, record.Intent, registry, facts);
            return Finish(record, blocked, BootStates.Completed);
        }
        return await ExecuteAsync(record, facts, capability, decision, ct);
    }

    public async Task<BootRecord> StartProjectAsync(string projectId, AutonomyLevel autonomy, string origin, string? source, CancellationToken ct)
    {
        if (!workspace.IsAuthorized(projectId)) throw EngineErrors.ProjectNotAuthorized();
        RequireAutonomy(autonomy);
        var record = New(BootMode.Project, projectId, autonomy, origin, source);
        Begin(record, "Project Boot iniciado.");

        var facts = Facts(record);
        var planned = planner.ForProject(facts, autonomy);
        record.Plan = planned.Plan;
        record.Steps = planned.Steps;
        Checkpoint(record, BootStates.Planned, null, $"Plano com {planned.Steps.Count} passos.");
        return await ExecuteAsync(record, facts, null, null, ct);
    }

    /// <summary>Boot de recuperação: refaz só o que o Boot interrompido não terminou e diz o que mudou no projeto desde então.</summary>
    public async Task<BootRecord> ResumeAsync(string? projectId, string bootId, CancellationToken ct)
    {
        if (projectId is not null && !workspace.IsAuthorized(projectId)) throw EngineErrors.ProjectNotAuthorized();
        var original = Find(projectId, bootId);
        if (original.State != BootStates.Interrupted || original.RecoveredByBootId is not null)
            throw new EngineException("BOOT_NOT_RESUMABLE", "Este pedido não foi interrompido, ou já foi retomado.");
        // Sem plano não há o que retomar, e o formato do resultado (pedido ou Project Boot) sairia errado: a pessoa pede de novo.
        if (original.Plan is null)
            throw new EngineException("BOOT_NOT_RESUMABLE", "Este pedido foi interrompido antes de ter um plano. Faça o pedido de novo.");

        var record = New(BootMode.Recovery, original.ProjectId, original.Autonomy, original.Origin, original.Source);
        record.Request = original.Request;
        record.RequestRedacted = original.RequestRedacted;
        record.Hint = original.Hint;
        record.Intent = original.Intent;
        record.Plan = original.Plan;
        record.RecoversBootId = original.Id;
        record.Steps = [.. original.Steps.Select(Clone)];
        foreach (var step in record.Steps.Where(step => step.Status != StepStatuses.Completed || step.Id == BootPlanner.ContextStepId))
        {
            // O contexto sempre é lido de novo: é com ele que se vê o que mudou. O que falhou ou não rodou também volta à fila.
            if (step.Status is StepStatuses.Blocked or StepStatuses.Skipped) continue;
            step.Status = StepStatuses.Pending;
            step.Summary = "";
            step.Evidence = [];
            step.ErrorCode = null;
        }
        Begin(record, $"Recuperando o Boot {original.Id}.");
        original.RecoveredByBootId = record.Id;
        store.Save(original);

        var facts = Facts(record);
        var capability = original.Intent?.CapabilityId is { } id ? registry.Find(id) : null;
        var decision = capability is null ? null : PermissionEvaluator.Evaluate(capability, registry.Resolve(capability, facts), record.Autonomy);
        return await ExecuteAsync(record, facts, capability, decision, ct, original.ContextVersion);
    }

    public BootRecord Detail(string? projectId, string bootId)
    {
        RequireAuthorized(projectId);
        return Find(projectId, bootId);
    }

    // Só o interrompido que tem plano e ainda não foi recuperado: sem plano não há o que retomar, e o motor recusa recuperar duas vezes.
    public static bool CanResume(BootRecord record) => record is { State: BootStates.Interrupted, RecoveredByBootId: null, Plan: not null };

    // Os mais novos até o limite, e mais todo Boot que dá para retomar, por antigo que seja: é o que a pessoa precisa achar, e com
    // muitos pedidos mais novos ele sumiria da lista.
    public IReadOnlyList<BootRecord> List(string? projectId, int limit = 20)
    {
        RequireAuthorized(projectId);
        var all = store.List(projectId);
        var take = Math.Clamp(limit, 1, 100);
        return [.. all.Take(take), .. all.Skip(take).Where(CanResume)];
    }

    public IReadOnlyList<(CapabilityStatus Status, PermissionDecision Decision)> Capabilities(string? projectId, AutonomyLevel autonomy = PermissionEvaluator.Ceiling)
    {
        var facts = probe.Read(projectId, _routed);
        return [.. registry.Resolve(facts).Select(status => (status, PermissionEvaluator.Evaluate(status.Capability, status, autonomy)))];
    }

    // Um Boot que a queda deixou em andamento não vai terminar: fica "interrompido", com os passos até onde foram, para ser retomado.
    public static int MarkInterrupted(IBootStore store, IClock clock)
    {
        var count = 0;
        foreach (var record in store.ListAll().Where(record => BootStates.IsLive(record.State)))
        {
            foreach (var step in record.Steps.Where(step => step.Status == StepStatuses.Running)) step.Status = StepStatuses.Pending;
            record.State = BootStates.Interrupted;
            record.UpdatedAt = clock.UtcNow;
            record.FinishedAt = clock.UtcNow;
            record.Checkpoints.Add(new BootCheckpoint { At = clock.UtcNow, State = BootStates.Interrupted, Note = "O motor foi encerrado antes de o Boot terminar." });
            record.Version++;
            store.Save(record);
            count++;
        }
        return count;
    }

    private async Task<BootRecord> ExecuteAsync(BootRecord record, BootFacts facts, Capability? capability, PermissionDecision? decision, CancellationToken ct,
        string? previousContextVersion = null)
    {
        Checkpoint(record, BootStates.Executing, null, "Executando os passos.");
        try
        {
            foreach (var step in record.Steps)
            {
                ct.ThrowIfCancellationRequested();
                if (step.Status != StepStatuses.Pending || !DependenciesMet(record, step)) continue;
                await RunStepAsync(record, step, facts, ct);
                Checkpoint(record, BootStates.Executing, step.Id, $"{step.Title}: {step.Status}.");
            }
        }
        catch (OperationCanceledException)
        {
            return Interrupt(record);
        }

        var result = BootResults.Build(record, facts, registry, capability, decision, previousContextVersion);
        var capabilitySteps = record.Steps.Where(step => step.Kind == StepKinds.Capability).ToList();
        // Só a capacidade decide: uma falha no passo de contexto fica nos problemas do resultado, não vira falha de um pedido respondido.
        var failed = capabilitySteps.Any(step => step.Status == StepStatuses.Failed);
        var everyoneFailed = capabilitySteps.Count > 0 && capabilitySteps.All(step => step.Status == StepStatuses.Failed);
        var state = record.Mode == BootMode.Project ? (everyoneFailed ? BootStates.Failed : BootStates.Completed) : failed ? BootStates.Failed : BootStates.Completed;
        return Finish(record, result, state);
    }

    private BootRecord Interrupt(BootRecord record)
    {
        foreach (var step in record.Steps.Where(step => step.Status == StepStatuses.Running)) step.Status = StepStatuses.Pending;
        record.State = BootStates.Interrupted;
        record.FinishedAt = clock.UtcNow;
        Checkpoint(record, BootStates.Interrupted, null, "Interrompido antes de terminar.");
        Audit(AuditActions.BootFinished, record);
        return record;
    }

    // O modelo só entra onde o léxico não resolveu e a pessoa escreveu um texto: escolha direta e léxico resolvido nunca o chamam. Sem
    // provedor, ou com qualquer falha do modelo, o resultado é o do léxico; o estado do estágio é o que fica dito no registro.
    private async Task<IntentResult> RefineWithModelAsync(BootRecord record, IntentResult lexicon, BootFacts facts, CancellationToken ct)
    {
        if (lexicon.Resolution == IntentResolution.Resolved || lexicon.Source == "explicit" || string.IsNullOrWhiteSpace(record.Request))
            return lexicon;
        if (modelStage is null || !facts.ModelConfigured) return lexicon with { ModelStage = ModelStages.Unavailable };

        var proposal = await modelStage.ProposeAsync(record.Request, ct);
        record.ModelUsage = proposal.Usage;
        if (proposal.Usage.Calls > 0) AuditModel(record, proposal);
        return recognizer.WithModel(lexicon, proposal);
    }

    private async Task RunStepAsync(BootRecord record, BootStep step, BootFacts facts, CancellationToken ct)
    {
        step.Status = StepStatuses.Running;
        step.StartedAt = clock.UtcNow;
        try
        {
            switch (step.Kind)
            {
                case StepKinds.Context:
                    RunContext(record, step);
                    break;
                case StepKinds.Capability:
                    await RunCapabilityAsync(record, step, facts, ct);
                    break;
                default:
                    // Abrir a tela é o que o Boot entrega; quem executa é ela.
                    step.Status = StepStatuses.Completed;
                    step.Summary = "Pronto para abrir.";
                    break;
            }
        }
        catch (EngineException ex)
        {
            step.Status = StepStatuses.Failed;
            step.ErrorCode = ex.Code;
            step.Summary = ex.Message;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // O texto de uma exceção pode ter caminho ou dado do projeto: só o tipo fica no registro.
            step.Status = StepStatuses.Failed;
            step.ErrorCode = "INTERNAL_ERROR";
            step.Summary = $"Falha interna ({ex.GetType().Name}).";
        }
        step.FinishedAt = clock.UtcNow;
    }

    private void RunContext(BootRecord record, BootStep step)
    {
        if (context is null || record.ProjectId is null)
        {
            step.Status = StepStatuses.Skipped;
            step.Summary = "O contexto do projeto não está disponível nesta sessão do motor.";
            return;
        }
        var package = context.Compose(record.ProjectId, ContextIntents.Bootstrap);
        // Na recuperação este é o contexto de agora; o do Boot interrompido continua no registro dele.
        record.ContextVersion = package.ContextVersion;
        step.Status = StepStatuses.Completed;
        step.Summary = $"Contexto do projeto reunido: {package.Items.Count} itens, {package.UsedTokens} tokens.";
        step.Evidence = [.. package.Limitations, .. package.Omitted.Select(omission => $"Ficou de fora ({omission.Section}): {omission.Ref}.")];
    }

    private async Task RunCapabilityAsync(BootRecord record, BootStep step, BootFacts facts, CancellationToken ct)
    {
        var capability = registry.Find(step.CapabilityId ?? "") ?? throw new EngineException("BOOT_CAPABILITY_UNKNOWN", "O passo aponta para uma capacidade que não existe.");
        var status = registry.Resolve(capability, facts);
        var decision = PermissionEvaluator.Evaluate(capability, status, record.Autonomy);
        if (decision.Verdict != PermissionVerdict.Allowed)
        {
            // No Project Boot cada leitura confere de novo: o que não vale (sem integração, sem projeto) vira limitação, não erro.
            step.Status = StepStatuses.Skipped;
            step.Summary = decision.Reason;
            return;
        }
        if (_dispatch is null) throw new EngineException("BOOT_NOT_BOUND", "O Boot não está ligado ao motor.");
        // A prova de que o passo é o que o catálogo autoriza: o método vem da ficha, nunca do registro gravado nem do pedido.
        var parameters = new JsonObject();
        if (record.ProjectId is not null) parameters["projectId"] = record.ProjectId;
        // Só o que a ficha declara, e sempre dentro da faixa do motor: o registro gravado não é fonte de argumento livre.
        foreach (var (name, value) in step.Arguments)
            if (CapabilityParameters.Bound(capability.Id, name, value) is { } bounded) parameters[name] = bounded;
        var result = await _dispatch(capability.Method!, parameters, ct);
        var summary = CapabilitySummaries.Describe(capability.Id, result);
        step.Status = StepStatuses.Completed;
        step.Summary = summary.Headline;
        step.Evidence = [.. step.ArgumentNotes, .. summary.Evidence];
    }

    private static bool DependenciesMet(BootRecord record, BootStep step) =>
        step.DependsOn.All(id => record.Steps.FirstOrDefault(other => other.Id == id) is { Status: StepStatuses.Completed or StepStatuses.Skipped });

    private BootRecord Finish(BootRecord record, BootResult result, string state)
    {
        if (record.RequestRedacted) result.Problems.Add("Uma linha do pedido parecia um segredo e foi omitida: não foi usada nem guardada.");
        record.Result = result;
        record.State = state;
        record.FinishedAt = clock.UtcNow;
        Checkpoint(record, state, null, result.Headline);
        Audit(AuditActions.BootFinished, record);
        BootRetention.PruneIfNeeded(store, record.ProjectId);
        return record;
    }

    private BootFacts Facts(BootRecord record) => probe.Read(record.ProjectId, _routed);

    private BootRecord New(BootMode mode, string? projectId, AutonomyLevel autonomy, string origin, string? source)
    {
        RequireAutonomy(autonomy);
        var now = clock.UtcNow;
        return new BootRecord
        {
            Id = BootIds.New(),
            Mode = mode,
            Origin = origin,
            Source = source,
            ProjectId = projectId,
            StableProjectId = projectId is null ? null : workspace.StableIdOf(projectId),
            Autonomy = autonomy,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    private void Begin(BootRecord record, string note)
    {
        Checkpoint(record, BootStates.Created, null, note);
        Audit(AuditActions.BootStarted, record);
    }

    // Cada checkpoint é uma gravação inteira do registro: quem lê nunca vê metade.
    private void Checkpoint(BootRecord record, string state, string? stepId, string note)
    {
        var now = clock.UtcNow;
        record.State = state;
        record.UpdatedAt = now;
        record.Version++;
        record.Checkpoints.Add(new BootCheckpoint { At = now, State = state, StepId = stepId, Note = note });
        store.Save(record);
    }

    // O registro de um projeto só é lido por quem tem a pasta autorizada, como a auditoria e a linha do tempo.
    private void RequireAuthorized(string? projectId)
    {
        if (projectId is not null && !workspace.IsAuthorized(projectId)) throw EngineErrors.ProjectNotAuthorized();
    }

    private BootRecord Find(string? projectId, string bootId)
    {
        if (!BootIds.IsValid(bootId)) throw new EngineException("BOOT_NOT_FOUND", "Pedido não encontrado.");
        return store.Find(projectId, bootId) ?? throw new EngineException("BOOT_NOT_FOUND", "Pedido não encontrado.");
    }

    private void Validate(BootStartRequest request)
    {
        if (request.ProjectId is not null && !workspace.IsAuthorized(request.ProjectId)) throw EngineErrors.ProjectNotAuthorized();
        RequireAutonomy(request.Autonomy);
        if (string.IsNullOrWhiteSpace(request.Text) && request.CapabilityId is null)
            throw new EngineException("BOOT_INVALID_REQUEST", "Escreva o que você quer fazer ou escolha uma funcionalidade.");
        if (request.Text is { Length: > MaxText })
            throw new EngineException("BOOT_INVALID_REQUEST", $"O pedido passa de {MaxText} caracteres.");
        if (request.Hint is not null && !Hints.Contains(request.Hint, StringComparer.Ordinal))
            throw new EngineException("BOOT_INVALID_REQUEST", "Tipo de atividade desconhecido.");
    }

    private static void RequireAutonomy(AutonomyLevel autonomy)
    {
        if (autonomy > PermissionEvaluator.Ceiling)
            throw new EngineException("BOOT_AUTONOMY_NOT_ENABLED", "Este nível de autonomia não está habilitado. O Spock prepara até o nível 1.");
    }

    // O registro de auditoria diz que houve um Boot e como terminou; o texto do pedido não entra.
    // Boot sem projeto fica só no próprio registro: a trilha é de projeto, e uma ação sem projectId não aparece em nenhuma.
    private void Audit(string action, BootRecord record)
    {
        if (record.ProjectId is null) return;
        audit.Record(action, new JsonObject
        {
            ["bootId"] = record.Id,
            ["projectId"] = record.ProjectId,
            ["mode"] = record.Mode.ToString().ToLowerInvariant(),
            ["origin"] = record.Origin,
            ["state"] = record.State,
            ["outcome"] = record.Result?.Outcome,
            ["capabilities"] = new JsonArray([.. (record.Plan?.Capabilities ?? []).Select(id => (JsonNode)JsonValue.Create(id)!)])
        });
    }

    // Como a auditoria de uma pergunta ao assistente: o gasto e o resultado, nunca o texto do pedido nem o que o modelo escreveu.
    private void AuditModel(BootRecord record, ModelIntentProposal proposal) => audit.Record(AuditActions.BootIntentModel, new JsonObject
    {
        ["bootId"] = record.Id,
        ["projectId"] = record.ProjectId,
        ["stage"] = proposal.State,
        ["proposed"] = proposal.Proposed,
        ["failure"] = proposal.FailureCode,
        ["calls"] = proposal.Usage.Calls,
        ["inputTokens"] = proposal.Usage.InputTokens,
        ["outputTokens"] = proposal.Usage.OutputTokens
    });

    private static BootStep Clone(BootStep step) => new()
    {
        Id = step.Id, Kind = step.Kind, Title = step.Title, CapabilityId = step.CapabilityId, Method = step.Method, DependsOn = [.. step.DependsOn],
        Operation = step.Operation, Status = step.Status, StartedAt = step.StartedAt, FinishedAt = step.FinishedAt, Summary = step.Summary,
        Arguments = new Dictionary<string, int>(step.Arguments), ArgumentNotes = [.. step.ArgumentNotes], Evidence = [.. step.Evidence], ErrorCode = step.ErrorCode
    };
}
