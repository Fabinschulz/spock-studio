using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Mariliz.Application.Execution;
using Mariliz.Application.Integrations;
using Mariliz.Application.Management.Assistant;
using Mariliz.Application.Models;
using Mariliz.Application.Ports;
using Mariliz.Application.Protocol;
using Mariliz.Application.Timeline;
using Mariliz.Application.Workspace;
using Mariliz.Core.Text;

namespace Mariliz.Application.Management.Investigation;

/// <summary>Uma etapa do caminho da investigação. Só nomes de grupo e ids de fatos: nunca texto de fato, de hipótese nem a pergunta.</summary>
public sealed record InvestigationStep(string Id, string Title, string Detail, int Round, IReadOnlyList<string> Groups, IReadOnlyList<string> Facts);

public sealed record InvestigationLimitation(string Code, string Message);

/// <summary>
/// O resultado de uma investigação: a resposta já conferida pelo motor (a mesma forma do assistente), o caminho percorrido e o que
/// limitou a investigação. <see cref="RunId"/> abre o diário dela na central de execuções.
/// </summary>
public sealed record ProjectInvestigation(
    string InvestigationId,
    string RunId,
    DateTimeOffset AnsweredAt,
    int Rounds,
    bool UsedModel,
    AssistantAnswer Answer,
    IReadOnlyList<InvestigationStep> Path,
    IReadOnlyList<InvestigationLimitation> Limitations);

/// <summary>
/// A investigação de gestão em etapas: o motor reúne os fatos (os do assistente mais os de execuções e de mudanças), o runtime de
/// IA os cruza num workflow com laço e o motor **confere a resposta** antes de devolvê-la, só contra os fatos que a investigação
/// de fato consultou. O caminho fica no diário, na central de execuções. Sem modelo configurado, a investigação ainda roda e
/// lista os sinais, dizendo que o modelo não escreveu a análise.
/// </summary>
public sealed partial class InvestigationService(
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
    /// <summary>Teto de tokens de uma investigação com modelo: até quatro chamadas (três rodadas de hipóteses e a conclusão).</summary>
    public const int TokenBudget = 30_000;

    public const int WindowDays = 28;
    private const int MaxFacts = 300;
    private const int MaxText = 400;
    private const int MaxMemoryDocuments = 1200;
    private const int MaxMemoryChars = 1_000_000;
    private const int MaxMemoryText = 4000;
    private const int MaxRetrieved = 24;
    private static readonly string[] StepIds = ["frame", "gather", "hypothesize", "decide", "retrieve", "conclude"];

    /// <summary>Prazo da conversa com o runtime: até quatro chamadas de modelo, cada uma com o seu prazo.</summary>
    internal TimeSpan Deadline { get; init; } = TimeSpan.FromSeconds(150);

    public async Task<ProjectInvestigation> InvestigateAsync(string projectId, string question, CancellationToken ct)
    {
        var cleanQuestion = question.Trim();
        if (cleanQuestion.Length is < ManagementAssistant.MinQuestion or > ManagementAssistant.MaxQuestion)
            throw new ParamException($"A pergunta precisa ter entre {ManagementAssistant.MinQuestion} e {ManagementAssistant.MaxQuestion} caracteres.");
        if (!workspace.IsAuthorized(projectId)) throw EngineErrors.ProjectNotAuthorized();

        var startedAt = clock.UtcNow;
        var investigationId = "inv-" + Guid.NewGuid().ToString("N")[..12];
        var executionId = "ex-" + Guid.NewGuid().ToString("N")[..12];
        ModelUsage? usage = null;
        try
        {
            var pack = Pack(projectId);
            var request = Request(investigationId, executionId, projectId, cleanQuestion, pack);
            var documents = MemoryDocuments(projectId);
            if (indexRoot is not null && documents.Count > 0) request["memory"] = MemoryRequest(documents);
            var grant = models is { Configured: true } ? models.Open(executionId, TokenBudget) : null;
            request["policy"]!["modelAllowed"] = grant is not null;
            request["limits"]!["maxTokens"] = grant is null ? 0 : TokenBudget;
            if (grant is not null) request["broker"] = new JsonObject { ["url"] = grant.Url, ["token"] = grant.Token };

            JsonObject ack;
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
            deadline.CancelAfter(Deadline);
            try
            {
                ack = await runtime.InvestigateAsync(request, _ => { }, deadline.Token);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                throw new EngineException("INVESTIGATION_TIMEOUT", "A investigação passou do prazo.");
            }
            finally
            {
                if (grant is not null) usage = models!.Close(executionId);
            }

            var result = Verify(ack, pack, investigationId, grant is not null, usage, models?.Calls(executionId), documents);
            hub.Audit.Record("management.investigation_answered", new JsonObject
            {
                ["projectId"] = projectId,
                ["rounds"] = result.Rounds,
                ["consulted"] = result.Answer.Evidence.Count,
                ["rejected"] = result.Answer.Rejected.Count,
                ["modelCalls"] = usage?.Calls ?? 0,
                ["inputTokens"] = usage?.InputTokens ?? 0,
                ["outputTokens"] = usage?.OutputTokens ?? 0
            });
            Record(projectId, investigationId, executionId, startedAt, result, null, usage);
            return result;
        }
        catch (EngineException ex)
        {
            Record(projectId, investigationId, executionId, startedAt, null, ex.Code, usage);
            throw;
        }
    }

    /// <summary>Os fatos do assistente, mais os de execuções e de mudanças dos últimos <see cref="WindowDays"/> dias, só os que o runtime consegue citar.</summary>
    public EvidencePack Pack(string projectId)
    {
        var basePack = assistant.Evidence(projectId);
        var facts = new List<EvidenceFact>(basePack.Facts);
        facts.AddRange(RunFacts(projectId));
        facts.AddRange(ChangeFacts(projectId));

        var sendable = facts.Where(Sendable).Take(MaxFacts).Select(fact => fact with { Group = Clip(fact.Group, 40), Text = Clip(fact.Text, 1200) }).ToList();
        var withheld = basePack.Withheld.ToList();
        if (sendable.Count < facts.Count) withheld.Add($"{facts.Count - sendable.Count} fatos ficaram de fora por não caberem no contrato da investigação.");
        return basePack with { Facts = sendable, Withheld = withheld };
    }

    /// <summary>
    /// A memória do projeto para as buscas do laço: as decisões registradas e os itens sincronizados que o projeto autorizou como
    /// contexto, os mesmos que o índice de engenharia recebe. Arquivo do projeto nunca entra. O que passa do teto fica de fora, e as
    /// decisões (mais recentes primeiro) vêm antes dos itens.
    /// </summary>
    private IReadOnlyList<MemoryDocument> MemoryDocuments(string projectId)
    {
        var documents = new List<MemoryDocument>();
        var chars = 0;
        var candidates = (memory?.Documents(projectId) ?? [])
            .Concat((hub.ExternalKnowledge?.Documents(projectId) ?? []).Select(item => new MemoryDocument(item.Path, item.Hash, item.Text)));
        foreach (var document in candidates)
        {
            var text = Clip(document.Text, MaxMemoryText);
            if (documents.Count >= MaxMemoryDocuments || chars + text.Length > MaxMemoryChars) break;
            if (text.Length == 0) continue;
            documents.Add(document with { Text = text });
            chars += text.Length;
        }
        return documents;
    }

    private JsonObject MemoryRequest(IReadOnlyList<MemoryDocument> documents)
    {
        Directory.CreateDirectory(indexRoot!);
        return new JsonObject
        {
            ["root"] = indexRoot,
            ["documents"] = new JsonArray(documents.Select(document => (JsonNode)new JsonObject
            {
                ["path"] = document.Path,
                ["hash"] = document.Hash,
                ["text"] = document.Text
            }).ToArray())
        };
    }

    private static bool Sendable(EvidenceFact fact) =>
        FactId().IsMatch(fact.Id) && fact.Group.Length > 0 && fact.Text.Length > 0;

    private IEnumerable<EvidenceFact> RunFacts(string projectId)
    {
        if (runs is null) yield break;
        var since = clock.UtcNow.AddDays(-WindowDays);
        // Perguntas ao assistente e investigações não são entrega: ficam de fora dos fatos de execução.
        var recent = runs.ListJournals(projectId)
            .Where(journal => journal.StartedAt >= since && journal.Kind is not (RunKinds.Assistant or RunKinds.Investigation))
            .ToList();
        if (recent.Count == 0)
        {
            yield return new("runs.summary", "execuções", $"Nenhuma execução do projeto nos últimos {WindowDays} dias.");
            yield break;
        }

        var failed = recent.Where(journal => journal.State == RunStates.Failed).ToList();
        var limited = recent.Count(journal => journal.State == RunStates.CompletedWithLimitations);
        var reverted = recent.Count(journal => journal.RevertedByRunId is not null || journal.RolledBack);
        yield return new("runs.summary", "execuções",
            $"Execuções do projeto nos últimos {WindowDays} dias: {recent.Count} ({KindCounts(recent)}); {Count(failed.Count, "falhou", "falharam")}, {Count(limited, "terminou com limitações", "terminaram com limitações")}, {Count(reverted, "foi revertida ou desfeita", "foram revertidas ou desfeitas")}.");
        if (failed.Count > 0)
            yield return new("runs.failed", "execuções", $"Execuções que falharam nos últimos {WindowDays} dias, por tipo: {KindCounts(failed)}.");
    }

    private IEnumerable<EvidenceFact> ChangeFacts(string projectId)
    {
        if (timeline is null) yield break;
        var since = clock.UtcNow.AddDays(-WindowDays);
        // Execuções já têm o seu grupo; aqui só o que mudou no projeto em si.
        var kinds = TimelineKinds.All.Select(kind => kind.Id).Where(kind => kind != TimelineKinds.Run).ToList();
        var page = timeline.Read(projectId, kinds, ProjectTimelineService.MaxLimit, null);
        var inWindow = page.Events.Where(item => item.At >= since).ToList();
        var truncated = page.NextBefore is not null && page.Events.Count > 0 && page.Events[^1].At >= since;
        if (inWindow.Count == 0)
        {
            yield return new("changes.summary", "mudanças", $"Nenhuma mudança registrada no projeto nos últimos {WindowDays} dias.");
            yield break;
        }

        var counts = inWindow.GroupBy(item => item.Kind).OrderBy(group => TimelineKinds.All.Select(kind => kind.Id).ToList().IndexOf(group.Key))
            .Select(group => $"{group.Count()} {Label(group.Key, group.Count())}");
        yield return new("changes.summary", "mudanças",
            $"Mudanças no projeto nos últimos {WindowDays} dias{(truncated ? " (ao menos)" : "")}: {string.Join(", ", counts)}.");
        var problems = inWindow.Count(item => item.Level == "problem");
        if (problems > 0) yield return new("changes.problems", "mudanças", $"Dessas mudanças, {problems} aconteceram com problema.");
    }

    private static string Count(int value, string one, string many) => $"{value} {(value == 1 ? one : many)}";

    private static string KindCounts(IEnumerable<RunJournal> journals) =>
        string.Join(", ", journals.GroupBy(journal => journal.Kind).OrderByDescending(group => group.Count()).ThenBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => $"{group.Count()} {KindLabel(group.Key, group.Count())}"));

    private static string KindLabel(string kind, int count) => (kind, count == 1) switch
    {
        (RunKinds.Apply, true) => "aplicação do Harness",
        (RunKinds.Apply, false) => "aplicações do Harness",
        (RunKinds.Revert, true) => "reversão do Harness",
        (RunKinds.Revert, false) => "reversões do Harness",
        (RunKinds.Validate, true) => "validação",
        (RunKinds.Validate, false) => "validações",
        (RunKinds.Workflow, true) => "pedido de engenharia",
        (RunKinds.Workflow, false) => "pedidos de engenharia",
        (_, true) => "outra execução",
        _ => "outras execuções"
    };

    private static string Label(string kind, int count) => (kind, count == 1) switch
    {
        (TimelineKinds.Harness, true) => "revisão do Harness",
        (TimelineKinds.Harness, false) => "revisões do Harness",
        (TimelineKinds.Profile, true) => "mudança do perfil",
        (TimelineKinds.Profile, false) => "mudanças do perfil",
        (TimelineKinds.Decision, true) => "decisão gerencial",
        (TimelineKinds.Decision, false) => "decisões gerenciais",
        (TimelineKinds.Risk, true) => "mudança de risco",
        (TimelineKinds.Risk, false) => "mudanças de risco",
        (TimelineKinds.Sync, true) => "sincronização",
        (TimelineKinds.Sync, false) => "sincronizações",
        (_, true) => "outro acontecimento",
        _ => "outros acontecimentos"
    };

    private static JsonObject Request(string investigationId, string executionId, string projectId, string question, EvidencePack pack) => new()
    {
        ["schema"] = "ai.investigation.start.v1",
        ["investigationId"] = investigationId,
        ["executionId"] = executionId,
        ["projectId"] = projectId,
        ["question"] = question,
        ["facts"] = new JsonArray(pack.Facts.Select(fact => (JsonNode)new JsonObject
        {
            ["id"] = fact.Id,
            ["group"] = fact.Group,
            ["text"] = fact.Text
        }).ToArray()),
        ["policy"] = new JsonObject { ["modelAllowed"] = false },
        ["limits"] = new JsonObject { ["maxIterations"] = 12, ["maxMilliseconds"] = 150_000, ["maxTokens"] = 0 }
    };

    /// <summary>
    /// O que volta do runtime é conferido antes de valer. O caminho só fala de fatos que existem; a resposta é conferida pelo
    /// <see cref="AnswerVerifier"/> contra **os fatos consultados**, não contra o pacote inteiro: citar um fato que a investigação
    /// nunca olhou não passa, mesmo que ele exista.
    /// </summary>
    private ProjectInvestigation Verify(JsonObject ack, EvidencePack pack, string investigationId, bool usedModel, ModelUsage? usage, IReadOnlyList<ModelCallRecord>? calls,
        IReadOnlyList<MemoryDocument> documents)
    {
        if (ack["schema"]?.GetValue<string>() != "ai.investigation.ack.v1" || ack.Text("investigationId") != investigationId)
            throw EngineErrors.AiRuntimeInvalidResponse();
        switch (ack.Text("status"))
        {
            case "rejected":
                throw new EngineException("INVESTIGATION_REJECTED", "O runtime de IA recusou o pedido da investigação.");
            case "failed":
                throw new EngineException("INVESTIGATION_FAILED", $"A investigação falhou ({Code(ack.Text("failure"))}).");
            case "completed" or "completed_with_limitations":
                break;
            default:
                throw EngineErrors.AiRuntimeInvalidResponse();
        }
        if (ack["answer"] is not JsonObject answer) throw EngineErrors.AiRuntimeInvalidResponse();

        // Os trechos da memória viram fatos escritos aqui, com o texto do documento que o motor mesmo mandou: o que o runtime
        // diz de cada trecho vale só pelo id e pelo caminho, e um caminho que o motor não mandou não vira fato.
        var memoryFacts = Retrieved(ack["retrieved"], documents);
        pack = pack with { Facts = [.. pack.Facts, .. memoryFacts] };
        var known = pack.Facts.Select(fact => fact.Id).ToHashSet(StringComparer.Ordinal);
        var consulted = Strings(ack["consulted"]).Where(known.Contains).Distinct(StringComparer.Ordinal).ToHashSet(StringComparer.Ordinal);
        var path = Path(ack["path"], known);
        var consultedPack = pack with { Facts = pack.Facts.Where(fact => consulted.Contains(fact.Id)).ToList() };

        var verified = AnswerVerifier.Verify(answer.ToJsonString(), consultedPack);
        var rejected = verified.Rejected
            .Concat(Rejected(ack["rejected"]))
            .ToList();
        var limitations = Limitations(ack["limitations"]);
        var rounds = Math.Clamp(ack["rounds"] is JsonValue value && value.TryGetValue<int>(out var number) ? number : path.Count(step => step.Id is "gather" or "retrieve"), 0, 4);

        var answerOut = new AssistantAnswer(
            pack.ProjectId,
            clock.UtcNow,
            verified.Summary,
            verified.Sections["observed"],
            verified.Sections["calculations"],
            verified.Sections["inferences"],
            verified.Sections["recommendations"],
            verified.Unavailable,
            rejected,
            consultedPack.Facts.Where(fact => verified.Cited.Contains(fact.Id)).ToList(),
            calls?.LastOrDefault()?.ModelVersion ?? "",
            usage?.InputTokens ?? 0,
            usage?.OutputTokens ?? 0);
        return new ProjectInvestigation(investigationId, investigationId, clock.UtcNow, rounds, usedModel, answerOut, path, limitations);
    }

    private static IReadOnlyList<EvidenceFact> Retrieved(JsonNode? node, IReadOnlyList<MemoryDocument> documents)
    {
        var facts = new List<EvidenceFact>();
        if (node is not JsonArray array) return facts;
        var byPath = documents.ToDictionary(document => document.Path, StringComparer.Ordinal);
        foreach (var item in array.OfType<JsonObject>().Take(MaxRetrieved))
        {
            var id = item.Text("id");
            if (!MemoryFactId().IsMatch(id) || facts.Any(fact => fact.Id == id) || !byPath.TryGetValue(item.Text("path"), out var document)) continue;
            var kind = document.Path.StartsWith(DecisionMemory.Prefix, StringComparison.Ordinal)
                ? "decisão registrada pela equipe"
                : "item sincronizado, escrito por terceiros";
            facts.Add(new EvidenceFact(id, "memória", Clip($"Memória do projeto ({kind}): {document.Text.ReplaceLineEndings(" ")}", 1200)));
        }
        return facts;
    }

    private static List<InvestigationStep> Path(JsonNode? node, HashSet<string> known)
    {
        var steps = new List<InvestigationStep>();
        if (node is not JsonArray array) return steps;
        foreach (var item in array.OfType<JsonObject>().Take(24))
        {
            var id = item.Text("id");
            if (!StepIds.Contains(id)) continue;
            var round = item["round"] is JsonValue value && value.TryGetValue<int>(out var number) ? Math.Clamp(number, 0, 4) : 0;
            steps.Add(new InvestigationStep(
                id, Clip(item.Text("title"), 120), Clip(item.Text("detail"), MaxText), round,
                Strings(item["groups"]).Select(group => Clip(group, 40)).Take(12).ToList(),
                Strings(item["facts"]).Where(known.Contains).Distinct(StringComparer.Ordinal).ToList()));
        }
        return steps;
    }

    private static IEnumerable<RejectedStatement> Rejected(JsonNode? node)
    {
        if (node is not JsonArray array) yield break;
        foreach (var item in array.OfType<JsonObject>().Take(24))
        {
            var text = Clip(item.Text("text"), MaxText);
            if (text.Length == 0) continue;
            yield return new RejectedStatement("Investigação", text, item.Text("reason") switch
            {
                "no_valid_evidence" => "Não cita nenhum fato consultado na investigação.",
                "too_many" => "Passou do limite de afirmações por seção.",
                _ => "Veio fora do formato esperado."
            });
        }
    }

    private static List<InvestigationLimitation> Limitations(JsonNode? node)
    {
        var result = new List<InvestigationLimitation>();
        if (node is not JsonArray array) return result;
        foreach (var item in array.OfType<JsonObject>().Take(12))
        {
            var code = Code(item.Text("code"));
            var message = Clip(item.Text("message"), MaxText);
            if (message.Length > 0) result.Add(new InvestigationLimitation(code, message));
        }
        return result;
    }

    /// <summary>
    /// O diário da investigação: o caminho como etapas (com os ids dos fatos consultados), contagens e o código do erro, **nunca o
    /// texto da pergunta, das hipóteses nem da resposta**. Não passa pelo rastreador: uma investigação só lê. Só projeto autorizado
    /// tem diário, e uma falha ao gravar nunca atrapalha a resposta.
    /// </summary>
    private void Record(string projectId, string investigationId, string executionId, DateTimeOffset startedAt, ProjectInvestigation? result, string? error, ModelUsage? usage)
    {
        if (runs is null || !workspace.IsAuthorized(projectId)) return;

        var journal = new RunJournal
        {
            RunId = investigationId,
            ProjectId = projectId,
            Kind = RunKinds.Investigation,
            ExecutionId = executionId,
            StartedAt = startedAt,
            FinishedAt = clock.UtcNow
        };
        if (result is null)
        {
            journal.State = RunStates.Failed;
            journal.Error = error;
        }
        else
        {
            var answer = result.Answer;
            var accepted = answer.Observed.Count + answer.Calculations.Count + answer.Inferences.Count + answer.Recommendations.Count;
            journal.State = answer.Rejected.Count > 0 || result.Limitations.Count > 0 ? RunStates.CompletedWithLimitations : RunStates.Completed;
            foreach (var (step, index) in result.Path.Select((step, index) => (step, index)))
                journal.Steps.Add(new JournalStep
                {
                    Id = $"{step.Id}{(step.Round > 0 ? step.Round : "")}-{index}",
                    Title = step.Title,
                    Status = StepStatus.Completed,
                    Detail = step.Detail + (step.Facts.Count > 0 ? $" Fatos: {string.Join(", ", step.Facts)}." : "")
                });
            journal.Steps.Add(new JournalStep
            {
                Id = "verify",
                Title = "Conferência do motor",
                Status = StepStatus.Completed,
                Detail = $"{accepted} afirmações aceitas, {answer.Rejected.Count} descartadas, {answer.Evidence.Count} fatos citados"
            });
            journal.Pending.AddRange(result.Limitations.Select(limitation => limitation.Message));
            if (answer.Rejected.Count > 0) journal.Pending.Add($"{answer.Rejected.Count} afirmações foram descartadas pela conferência do motor.");
        }

        if (usage is { Calls: > 0 })
            journal.ModelUsage = new JournalModelUsage { Calls = usage.Calls, InputTokens = usage.InputTokens, OutputTokens = usage.OutputTokens };
        try
        {
            runs.SaveJournal(journal);
        }
        catch (IOException)
        {
            // A resposta já foi dada; o registro é só histórico.
        }
    }

    private static IEnumerable<string> Strings(JsonNode? node) =>
        node is JsonArray array
            ? array.OfType<JsonValue>().Select(value => value.TryGetValue<string>(out var text) ? text : null).OfType<string>()
            : [];

    private static string Clip(string text, int max)
    {
        var trimmed = text.Trim();
        return trimmed.Length > max ? trimmed[..max] : trimmed;
    }

    private static string Code(string code) => CodePattern().IsMatch(code) ? code : "DESCONHECIDO";

    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9._:-]{0,63}\z")]
    private static partial Regex FactId();

    [GeneratedRegex(@"^[A-Z][A-Z0-9_]{0,63}\z")]
    private static partial Regex CodePattern();

    [GeneratedRegex(@"^memory\.[0-9]{1,3}\z")]
    private static partial Regex MemoryFactId();
}
