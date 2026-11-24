using System.Text.Json.Nodes;
using Mariliz.Application.Execution;
using Mariliz.Application.Integrations;
using Mariliz.Application.Models;
using Mariliz.Application.Ports;
using Mariliz.Application.Protocol;
using Mariliz.Application.Workspace;
using Mariliz.Application.Audit;

namespace Mariliz.Application.Management.Assistant;

public sealed record PortfolioEvidenceProject(string Alias, string ProjectId, string Name, int Facts);

public sealed record PortfolioEvidence(
    DateTimeOffset GeneratedAt,
    IReadOnlyList<PortfolioEvidenceProject> Projects,
    IReadOnlyList<EvidenceFact> Facts,
    IReadOnlyList<string> Withheld);

public sealed record PortfolioAnswer(IReadOnlyList<PortfolioEvidenceProject> Projects, AssistantAnswer Answer);

// Cada projeto entra com o pacote que o assistente dele montaria, então a política de cada um vale por inteiro. O conferente descarta número que os fatos citados não têm: somar ou comparar por conta própria não passa.
public sealed class PortfolioAssistant(
    ManagementAssistant assistant,
    WorkspaceAuthorizer workspace,
    ModelGateway models,
    IntegrationHub hub,
    IClock clock)
{
    public const int MaxProjects = 10;
    public const int MaxFactsPerProject = 40;
    public const int MaxAnswerTokens = 1500;

    /// <summary>Quantos fatos cada projeto manda. Só os testes mudam o valor.</summary>
    internal int FactsPerProject { get; init; } = MaxFactsPerProject;

    private const string Instructions = """
        Você é o assistente gerencial do Spock Studio e responde sobre vários projetos ao mesmo tempo. Responda em português do Brasil, com frases curtas e diretas.
        Os fatos vêm separados por projeto (p1, p2...), cada um com um id entre colchetes. Use SOMENTE esses fatos. Não use conhecimento externo.
        Não faça contas novas, nem entre projetos: não some, não tire média e não calcule diferença. Compare lendo o fato de cada projeto e citando os dois.
        Todo número que você escrever precisa aparecer nos fatos que você citar. Ao citar o nome de um projeto, cite também o fato pN.name dele.
        Separe a resposta assim:
        - observed: o que os dados sincronizados de cada projeto mostram;
        - calculations: o que o sistema calculou (indicadores, previsão, riscos por regra);
        - inferences: interpretações suas, com linguagem de hipótese ("pode indicar", "sugere");
        - recommendations: ações possíveis, ligadas aos fatos;
        - unavailable: o que a pergunta pede e os fatos não têm (inclusive um projeto sem dados). Diga isso em vez de supor.
        Cada item de observed, calculations, inferences e recommendations cita em "evidence" os ids dos fatos que o sustentam.
        Responda apenas com JSON neste formato:
        {"summary": {"text": "uma ou duas frases", "evidence": ["id"]}, "observed": [{"text": "...", "evidence": ["id"]}], "calculations": [...],
         "inferences": [...], "recommendations": [...], "unavailable": [{"text": "..."}]}
        """;

    // Um projeto pedido que não está aberto neste computador é recusado, não ignorado em silêncio.
    public PortfolioEvidence Evidence(IReadOnlyList<string>? projectIds)
    {
        var open = workspace.Projects();
        var chosen = Choose(open, projectIds);
        var now = clock.UtcNow;
        var projects = new List<PortfolioEvidenceProject>();
        var facts = new List<EvidenceFact>();
        var withheld = new List<string> { "Nenhum código, documento ou arquivo de nenhum projeto é enviado." };

        foreach (var (project, index) in chosen.Select((project, index) => (project, index)))
        {
            var alias = $"p{index + 1}";
            var pack = assistant.Evidence(project.ProjectId);
            var name = Clean(project.DisplayName);
            var own = new List<EvidenceFact> { new($"{alias}.name", "projeto", $"Projeto {alias}: {name}.") };
            var dropped = 0;
            foreach (var fact in pack.Facts)
            {
                // A data de hoje é a mesma para todos: entra uma vez.
                if (fact.Id == "today")
                {
                    if (facts.All(item => item.Id != "today")) facts.Add(fact);
                    continue;
                }
                if (own.Count >= FactsPerProject)
                {
                    dropped++;
                    continue;
                }
                own.Add(fact with { Id = $"{alias}.{fact.Id}" });
            }
            facts.AddRange(own);
            projects.Add(new PortfolioEvidenceProject(alias, project.ProjectId, name, own.Count));
            // O primeiro aviso é geral; os seguintes dizem o que a política DESTE projeto deixou de fora.
            withheld.AddRange(pack.Withheld.Skip(1).Select(line => $"{name}: {line}"));
            if (dropped > 0)
                withheld.Add($"{name}: {dropped} fatos ficaram de fora por passarem do limite de {FactsPerProject} fatos por projeto.");
        }
        if ((projectIds is null || projectIds.Count == 0) && open.Count > MaxProjects)
            withheld.Add($"Só os {MaxProjects} primeiros projetos entram: escolha quais, se quiser outros.");
        return new PortfolioEvidence(now, projects, facts, withheld);
    }

    public async Task<PortfolioAnswer> AskAsync(string question, IReadOnlyList<string>? projectIds, CancellationToken ct)
    {
        var clean = ManagementAssistant.CleanQuestion(question);
        var evidence = Evidence(projectIds);
        var startedAt = clock.UtcNow;
        try
        {
            var answer = await AnswerAsync(clean, evidence, ct);
            foreach (var project in evidence.Projects) assistant.RecordRun(project.ProjectId, startedAt, answer, error: null, coveredProjects: evidence.Projects.Count);
            return new PortfolioAnswer(evidence.Projects, answer);
        }
        catch (EngineException ex)
        {
            foreach (var project in evidence.Projects) assistant.RecordRun(project.ProjectId, startedAt, answer: null, ex.Code);
            throw;
        }
    }

    private async Task<AssistantAnswer> AnswerAsync(string question, PortfolioEvidence evidence, CancellationToken ct)
    {
        var request = new ChatRequest(
            [new ChatMessage("system", Instructions), new ChatMessage("user", $"{Facts(evidence)}\n\nPergunta: {question}")],
            MaxAnswerTokens,
            JsonObject: true);
        var completion = await models.ChatAsync(request, ct, ModelPurposes.Assistant);
        var pack = new EvidencePack("portfolio", "Portfólio", evidence.GeneratedAt, evidence.Facts, evidence.Withheld);
        var verified = AnswerVerifier.Verify(completion.Text, pack);

        hub.Audit.Record(AuditActions.PortfolioAnswered, new JsonObject
        {
            ["projects"] = evidence.Projects.Count,
            ["facts"] = evidence.Facts.Count,
            ["cited"] = verified.Cited.Count,
            ["rejected"] = verified.Rejected.Count,
            ["inputTokens"] = completion.InputTokens,
            ["outputTokens"] = completion.OutputTokens,
            ["promptHash"] = completion.PromptHash
        });

        return verified.ToAnswer("portfolio", clock.UtcNow, evidence.Facts, completion.ModelVersion, completion.InputTokens, completion.OutputTokens);
    }

    private static string Facts(PortfolioEvidence evidence)
    {
        var lines = new List<string>();
        foreach (var fact in evidence.Facts.Where(fact => fact.Id == "today")) lines.Add($"[{fact.Id}] {fact.Text}");
        foreach (var project in evidence.Projects)
        {
            lines.Add($"\nProjeto {project.Alias}:");
            lines.AddRange(evidence.Facts.Where(fact => fact.Id.StartsWith(project.Alias + ".", StringComparison.Ordinal)).Select(fact => $"[{fact.Id}] {fact.Text}"));
        }
        return string.Join("\n", lines);
    }

    private static IReadOnlyList<AuthorizedProject> Choose(IReadOnlyList<AuthorizedProject> open, IReadOnlyList<string>? projectIds)
    {
        if (open.Count == 0) throw new EngineException("PORTFOLIO_EMPTY", "Nenhum projeto aberto neste computador.");
        if (projectIds is null || projectIds.Count == 0) return open.Take(MaxProjects).ToList();

        var ids = projectIds.Distinct(StringComparer.Ordinal).ToList();
        if (ids.Count > MaxProjects) throw new ParamException($"A pergunta cobre até {MaxProjects} projetos.");
        return ids.Select(id => open.FirstOrDefault(project => project.ProjectId == id) ?? throw EngineErrors.ProjectNotAuthorized()).ToList();
    }

    private static string Clean(string name)
    {
        var flat = new string(name.Where(ch => !char.IsControl(ch)).ToArray()).Trim();
        return flat.Length > 80 ? flat[..80] : flat.Length == 0 ? "sem nome" : flat;
    }
}
