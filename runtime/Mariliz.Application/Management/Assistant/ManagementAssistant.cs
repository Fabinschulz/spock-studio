using System.Text.Json.Nodes;
using Mariliz.Application.Execution;
using Mariliz.Application.Integrations;
using Mariliz.Application.Models;
using Mariliz.Application.Ports;
using Mariliz.Application.Protocol;
using Mariliz.Application.Workspace;

namespace Mariliz.Application.Management.Assistant;

/// <summary>
/// O assistente gerencial. O motor reúne os fatos já calculados (indicadores, previsão, riscos), o modelo escreve a
/// explicação separando observado, cálculo, inferência e recomendação, e o motor confere a resposta antes de devolvê-la.
/// Uma pergunta é uma chamada só, pelo proxy de modelo do motor: a chave não sai daqui. Investigações em várias etapas,
/// quando existirem, são trabalho para um workflow no runtime de IA.
/// </summary>
public sealed class ManagementAssistant(
    ManagementService management,
    IntegrationHub hub,
    WorkspaceAuthorizer workspace,
    ModelGateway models,
    IClock clock,
    IRunStore? runs = null)
{
    public const int MinQuestion = 3;
    public const int MaxQuestion = 500;
    public const int MaxAnswerTokens = 1200;

    private const string Instructions = """
        Você é o assistente gerencial do Spock Studio. Responda em português do Brasil, com frases curtas e diretas.
        Use SOMENTE os fatos fornecidos, cada um com um id entre colchetes. Não use conhecimento externo sobre o projeto.
        Não faça contas novas: todo número que você escrever precisa aparecer nos fatos que você citar.
        Separe a resposta assim:
        - observed: o que os dados sincronizados mostram (contagens, situação, datas de leitura);
        - calculations: o que o sistema calculou (indicadores de fluxo, previsão, riscos por regra);
        - inferences: interpretações suas a partir dos fatos, com linguagem de hipótese ("pode indicar", "sugere");
        - recommendations: ações possíveis, ligadas aos fatos;
        - unavailable: o que a pergunta pede e os fatos não têm. Diga isso em vez de supor.
        Cada item de observed, calculations, inferences e recommendations cita em "evidence" os ids dos fatos que o sustentam.
        Responda apenas com JSON neste formato:
        {"summary": {"text": "uma ou duas frases", "evidence": ["id"]}, "observed": [{"text": "...", "evidence": ["id"]}], "calculations": [...],
         "inferences": [...], "recommendations": [...], "unavailable": [{"text": "..."}]}
        """;

    /// <summary>A pergunta sem espaços nas pontas; recusa a que não tem entre <see cref="MinQuestion"/> e <see cref="MaxQuestion"/> caracteres.</summary>
    public static string CleanQuestion(string question)
    {
        var clean = question.Trim();
        if (clean.Length is < MinQuestion or > MaxQuestion)
            throw new ParamException($"A pergunta precisa ter entre {MinQuestion} e {MaxQuestion} caracteres.");
        return clean;
    }

    public EvidencePack Evidence(string projectId)
    {
        var integrations = hub.Projects.List(projectId);
        var name = workspace.Projects().FirstOrDefault(project => project.ProjectId == projectId)?.DisplayName ?? projectId;
        return EvidenceBuilder.Build(projectId, name, clock.UtcNow, integrations, management.Metrics(projectId, null),
            management.Forecast(projectId), management.Risks(projectId), management.Milestones(projectId));
    }

    /// <summary>
    /// Responde e deixa o registro na central de execuções. A pergunta inválida é recusada antes e não vira execução; uma
    /// pergunta que o modelo não conseguiu responder vira uma execução que falhou, com o código do motivo.
    /// </summary>
    public async Task<AssistantAnswer> AskAsync(string projectId, string question, CancellationToken ct)
    {
        var cleanQuestion = CleanQuestion(question);
        var startedAt = clock.UtcNow;
        try
        {
            var answer = await AnswerAsync(projectId, cleanQuestion, ct);
            RecordRun(projectId, startedAt, answer, error: null);
            return answer;
        }
        catch (EngineException ex)
        {
            RecordRun(projectId, startedAt, answer: null, ex.Code);
            throw;
        }
    }

    private async Task<AssistantAnswer> AnswerAsync(string projectId, string cleanQuestion, CancellationToken ct)
    {

        var pack = Evidence(projectId);
        var facts = string.Join("\n", pack.Facts.Select(fact => $"[{fact.Id}] {fact.Text}"));
        var request = new ChatRequest(
            [
                new ChatMessage("system", Instructions),
                new ChatMessage("user", $"Projeto: {pack.ProjectName}\n\nFatos:\n{facts}\n\nPergunta: {cleanQuestion}")
            ],
            MaxAnswerTokens,
            JsonObject: true);

        var completion = await models.ChatAsync(request, ct, ModelPurposes.Assistant);
        var verified = AnswerVerifier.Verify(completion.Text, pack);

        hub.Audit.Record("management.assistant_answered", new JsonObject
        {
            ["projectId"] = projectId,
            ["facts"] = pack.Facts.Count,
            ["cited"] = verified.Cited.Count,
            ["rejected"] = verified.Rejected.Count,
            ["inputTokens"] = completion.InputTokens,
            ["outputTokens"] = completion.OutputTokens,
            ["promptHash"] = completion.PromptHash
        });

        return verified.ToAnswer(projectId, clock.UtcNow, pack.Facts, completion.ModelVersion, completion.InputTokens, completion.OutputTokens);
    }

    /// <summary>
    /// O diário de uma pergunta: contagens e o código do erro, **nunca o texto da pergunta nem o da resposta** (o diário não
    /// guarda texto de pedido). Não passa pelo rastreador: uma pergunta não escreve no projeto e não pode bloquear a execução
    /// que estiver andando nele. Só projeto autorizado tem diário, e uma falha ao gravar nunca atrapalha a resposta.
    /// </summary>
    internal void RecordRun(string projectId, DateTimeOffset startedAt, AssistantAnswer? answer, string? error, int coveredProjects = 1)
    {
        if (runs is null || !workspace.IsAuthorized(projectId)) return;

        var journal = new RunJournal
        {
            RunId = "ask-" + Guid.NewGuid().ToString("N")[..12],
            ProjectId = projectId,
            Kind = RunKinds.Assistant,
            StartedAt = startedAt,
            FinishedAt = clock.UtcNow
        };
        if (answer is null)
        {
            journal.State = RunStates.Failed;
            journal.Error = error;
        }
        else
        {
            var accepted = answer.Observed.Count + answer.Calculations.Count + answer.Inferences.Count + answer.Recommendations.Count;
            journal.State = answer.Rejected.Count > 0 ? RunStates.CompletedWithLimitations : RunStates.Completed;
            // Uma pergunta ao portfólio grava o mesmo uso no diário de cada projeto que ela cobriu: cada um mostra o custo real da
            // pergunta que o envolveu, e este passo diz isso, para ninguém somar entre projetos e achar que custou várias vezes.
            if (coveredProjects > 1)
                journal.Steps.Add(new JournalStep
                {
                    Id = "portfolio",
                    Title = "Pergunta ao portfólio",
                    Status = StepStatus.Completed,
                    Detail = $"A pergunta cobriu {coveredProjects} projetos. O uso de modelo abaixo é o da pergunta inteira, repetido no diário de cada projeto."
                });
            journal.Steps.Add(new JournalStep { Id = "answer", Title = "Resposta escrita pelo modelo", Status = StepStatus.Completed });
            journal.Steps.Add(new JournalStep
            {
                Id = "verify",
                Title = "Conferência do motor",
                Status = StepStatus.Completed,
                Detail = $"{accepted} afirmações aceitas, {answer.Rejected.Count} descartadas, {answer.Evidence.Count} fatos citados"
            });
            if (answer.Rejected.Count > 0)
                journal.Pending.Add($"{answer.Rejected.Count} afirmações do modelo foram descartadas pela conferência do motor.");
            journal.ModelUsage = new JournalModelUsage { Calls = 1, InputTokens = answer.InputTokens, OutputTokens = answer.OutputTokens };
        }

        try
        {
            runs.SaveJournal(journal);
        }
        catch (IOException)
        {
            // A resposta já foi dada; o registro é só histórico.
        }
    }
}
