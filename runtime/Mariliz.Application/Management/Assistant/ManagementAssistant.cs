using System.Text.Json.Nodes;
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
    IClock clock)
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

    public EvidencePack Evidence(string projectId)
    {
        var integrations = hub.Projects.List(projectId);
        var name = workspace.Projects().FirstOrDefault(project => project.ProjectId == projectId)?.DisplayName ?? projectId;
        return EvidenceBuilder.Build(projectId, name, clock.UtcNow, integrations, management.Metrics(projectId, null),
            management.Forecast(projectId), management.Risks(projectId));
    }

    public async Task<AssistantAnswer> AskAsync(string projectId, string question, CancellationToken ct)
    {
        var cleanQuestion = question.Trim();
        if (cleanQuestion.Length is < MinQuestion or > MaxQuestion)
            throw new ParamException($"A pergunta precisa ter entre {MinQuestion} e {MaxQuestion} caracteres.");

        var pack = Evidence(projectId);
        var facts = string.Join("\n", pack.Facts.Select(fact => $"[{fact.Id}] {fact.Text}"));
        var request = new ChatRequest(
            [
                new ChatMessage("system", Instructions),
                new ChatMessage("user", $"Projeto: {pack.ProjectName}\n\nFatos:\n{facts}\n\nPergunta: {cleanQuestion}")
            ],
            MaxAnswerTokens,
            JsonObject: true);

        var completion = await models.ChatAsync(request, ct);
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

        return new AssistantAnswer(
            projectId,
            clock.UtcNow,
            verified.Summary,
            verified.Sections["observed"],
            verified.Sections["calculations"],
            verified.Sections["inferences"],
            verified.Sections["recommendations"],
            verified.Unavailable,
            verified.Rejected,
            pack.Facts.Where(fact => verified.Cited.Contains(fact.Id)).ToList(),
            completion.ModelVersion,
            completion.InputTokens,
            completion.OutputTokens);
    }
}
