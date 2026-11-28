using Mariliz.Application.Audit;
using Mariliz.Application.Execution;
using Mariliz.Application.Integrations;
using Mariliz.Application.Ports;
using Mariliz.Application.Workspace;
using Mariliz.Core.Policy;
using System.Text.Json.Nodes;

namespace Mariliz.Application.Quality;

/// <summary>
/// O que a pessoa decide sobre um achado. Nunca apaga o achado: o relatório do motor fica como foi, e a decisão aparece ao lado, com
/// a justificativa e a hora.
/// </summary>
public static class FindingDecisions
{
    public const string Confirmed = "confirmed";
    public const string FalsePositive = "dismissed_false_positive";
    public const string AcceptedRisk = "accepted_risk";
    public const string FixRequested = "fix_requested";

    public static readonly IReadOnlyList<string> All = [Confirmed, FalsePositive, AcceptedRisk, FixRequested];

    // Descartar e aceitar um risco são as duas decisões que tiram o achado do caminho da entrega: exigem dizer por quê.
    public static bool RequiresJustification(string decision) => decision is FalsePositive or AcceptedRisk;

    public static bool Resolves(string decision) => decision is FalsePositive or AcceptedRisk;
}

public static class DeliveryDecisions
{
    public const string Approved = "approved";
    public const string Rejected = "rejected";

    public static bool IsKnown(string? decision) => decision is Approved or Rejected;
}

public sealed record FindingDecision(string FindingId, string Decision, string? Justification, DateTimeOffset DecidedAt, string ReviewId);

public sealed record DeliveryDecision(string ReviewId, string Decision, string Justification, DateTimeOffset DecidedAt);

/// <summary>O histórico só cresce: a decisão em vigor de um achado é a última dele.</summary>
public sealed record DecisionLog(IReadOnlyList<FindingDecision> Findings, IReadOnlyList<DeliveryDecision> Deliveries)
{
    public static DecisionLog Empty { get; } = new([], []);
}

/// <param name="OpenBlocking">Achados confirmados e graves que ninguém descartou nem aceitou.</param>
/// <param name="ReadyForDelivery">Nada bloqueante em aberto e a revisão não é inconclusiva nem vazia. A pessoa ainda assina a entrega.</param>
public sealed record ReviewStatus(string EngineVerdict, int OpenBlocking, int OpenNeedsCheck, bool ReadyForDelivery, DeliveryDecision? Delivery);

public sealed record ReviewView(ReviewRecord Review, IReadOnlyList<FindingDecision> Decisions, ReviewStatus Status);

/// <param name="Stopped">O limite de tentativas de um achado foi atingido: o motor não monta mais pedido e a decisão é de uma pessoa.</param>
public sealed record BriefItem(string FindingId, string Title, string Location, string Excerpt, string Expected, string Suggestion, string Confidence, int Attempt);

public sealed record CorrectionBrief(string ReviewId, int Attempt, int MaxAttempts, bool Stopped, IReadOnlyList<BriefItem> Items, IReadOnlyList<string> Constraints, string Text);

/// <summary>
/// A jornada de decisão da revisão: a pessoa confirma, descarta ou aceita o risco de cada achado e, por fim, assina a entrega.
/// O veredito do motor nunca muda. O que muda é o que ainda bloqueia, e a assinatura da pessoa é a autorização humana que um juiz
/// nunca substitui.
/// </summary>
public sealed class ReviewDecisionService(WorkspaceAuthorizer workspace, IReviewStore store, IClock clock, IAuditLog audit)
{
    public const int MinJustification = 10;
    public const int MaxJustification = 500;

    public ReviewView View(string projectId, ReviewRecord review)
    {
        var log = store.LoadDecisions(projectId);
        return Build(review, log);
    }

    public ReviewView DecideFinding(string projectId, string reviewId, string findingId, string decision, string? justification)
    {
        var review = Require(projectId, reviewId);
        if (!FindingDecisions.All.Contains(decision)) throw new EngineException("REVIEW_DECISION_INVALID", "Decisão desconhecida.");
        var finding = review.Findings.FirstOrDefault(item => item.Id == findingId)
            ?? throw new EngineException("REVIEW_FINDING_NOT_FOUND", "Este achado não está nesta revisão.");
        var text = Justification(decision, justification);
        // Risco crítico não se aceita: o caminho é corrigir, ou dizer que o motor errou (e por quê).
        if (decision == FindingDecisions.AcceptedRisk && finding.Severity == QualitySeverities.Critical)
            throw new EngineException("REVIEW_RISK_NOT_ACCEPTABLE", "Um achado crítico não se aceita como risco: corrija, ou descarte dizendo por que o motor errou.");

        var log = store.LoadDecisions(projectId);
        var entry = new FindingDecision(findingId, decision, text, clock.UtcNow, reviewId);
        store.SaveDecisions(projectId, log with { Findings = [.. log.Findings, entry] });
        audit.Record(AuditActions.QualityFindingDecided, new JsonObject
        {
            ["projectId"] = projectId, ["reviewId"] = reviewId, ["ruleId"] = finding.RuleId, ["decision"] = decision
        });
        return View(projectId, review);
    }

    public ReviewView DecideDelivery(string projectId, string reviewId, string decision, string? justification)
    {
        var review = Require(projectId, reviewId);
        if (!DeliveryDecisions.IsKnown(decision)) throw new EngineException("REVIEW_DECISION_INVALID", "Decisão desconhecida.");
        var text = Justification(DeliveryDecisions.Approved, justification) ?? "";
        var log = store.LoadDecisions(projectId);
        if (decision == DeliveryDecisions.Approved && !Build(review, log).Status.ReadyForDelivery)
            throw new EngineException("REVIEW_NOT_READY", "Há achado bloqueante em aberto, ou a revisão não foi concluída: resolva antes de aprovar a entrega.");

        var entry = new DeliveryDecision(reviewId, decision, text, clock.UtcNow);
        store.SaveDecisions(projectId, log with { Deliveries = [.. log.Deliveries, entry] });
        audit.Record(AuditActions.QualityDeliveryDecided, new JsonObject { ["projectId"] = projectId, ["reviewId"] = reviewId, ["decision"] = decision });
        return View(projectId, review);
    }

    public const int MaxAttempts = 3;

    // O que o agente que implementou recebe de volta: só o que ainda está aberto, com as regras que ele não pode quebrar para passar.
    // Montar o pedido registra "correção pedida" nos achados, e é isso que conta as tentativas: depois de três, o motor para.
    public CorrectionBrief Brief(string projectId, string reviewId)
    {
        var review = Require(projectId, reviewId);
        var log = store.LoadDecisions(projectId);
        var latest = log.Findings.GroupBy(entry => entry.FindingId).ToDictionary(group => group.Key, group => group.Last());
        var open = review.Findings.Where(finding => finding.Severity != QualitySeverities.Informational
            && !(latest.TryGetValue(finding.Id, out var d) && FindingDecisions.Resolves(d.Decision))).Take(20).ToList();
        var attempts = open.ToDictionary(finding => finding.Id, finding => log.Findings.Count(entry => entry.FindingId == finding.Id && entry.Decision == FindingDecisions.FixRequested) + 1);
        var attempt = attempts.Count == 0 ? 1 : attempts.Values.Max();
        var stopped = attempts.Values.Any(value => value > MaxAttempts);

        var items = open.Select(finding => new BriefItem(finding.Id, finding.Title, Where(finding), finding.Evidence.Excerpt, finding.ExpectedBehavior,
            finding.SuggestedCorrection, finding.Confidence, attempts[finding.Id])).ToList();
        var text = stopped ? StoppedText(items) : Text(review, items, attempt);

        if (!stopped && items.Count > 0)
        {
            var now = clock.UtcNow;
            store.SaveDecisions(projectId, log with { Findings = [.. log.Findings, .. open.Select(finding => new FindingDecision(finding.Id, FindingDecisions.FixRequested, null, now, reviewId))] });
        }
        return new CorrectionBrief(reviewId, attempt, MaxAttempts, stopped, items, Constraints, text);
    }

    public static readonly IReadOnlyList<string> Constraints =
    [
        "Corrija só o que está listado. Não mude outros arquivos nem o comportamento fora do escopo da atividade original.",
        "Não remova, desligue nem enfraqueça testes, verificações, regras do Harness ou configuração de segurança para passar.",
        "O relatório e a decisão são do Spock e da pessoa: não os altere nem apague achados. Se discordar de um achado, diga por quê na resposta, e quem decide é a pessoa.",
        "Achados marcados como hipótese vêm de um modelo e podem estar errados: confira a linha antes de mudar qualquer coisa.",
        $"Ao terminar, peça uma nova revisão ao Spock. Depois de {MaxAttempts} tentativas sobre o mesmo achado, pare e peça uma pessoa."
    ];

    private static string Where(ReviewFinding finding) => finding.StartLine is { } line ? $"{finding.Path}:{line}" : finding.Path;

    private static string Text(ReviewRecord review, IReadOnlyList<BriefItem> items, int attempt)
    {
        var lines = new List<string>
        {
            $"# Correções pedidas pela revisão do Spock (tentativa {attempt} de {MaxAttempts})",
            "",
            items.Count == 0 ? "Não há achado aberto: nada a corrigir." : $"A revisão {review.Id} deixou {items.Count} ponto(s) em aberto. Corrija cada um e peça nova revisão.",
            ""
        };
        foreach (var item in items)
        {
            lines.Add($"## {item.Title}");
            lines.Add($"- Onde: {item.Location}");
            lines.Add($"- Trecho: {item.Excerpt}");
            lines.Add($"- Esperado: {item.Expected}");
            lines.Add($"- Sugestão: {item.Suggestion}");
            lines.Add($"- Confiança: {item.Confidence}");
            lines.Add("");
        }
        lines.Add("## Regras que não mudam");
        lines.AddRange(Constraints.Select(rule => $"- {rule}"));
        return string.Join('\n', lines);
    }

    private static string StoppedText(IReadOnlyList<BriefItem> items) =>
        $"O limite de {MaxAttempts} tentativas foi atingido em {items.Count(item => item.Attempt > MaxAttempts)} achado(s). O Spock não monta mais pedido de correção: uma pessoa precisa decidir se corrige à mão, descarta com justificativa ou aceita o risco.";

    private ReviewRecord Require(string projectId, string reviewId)
    {
        if (!workspace.IsAuthorized(projectId)) throw EngineErrors.ProjectNotAuthorized();
        if (!ReviewIds.IsValid(reviewId)) throw new EngineException("REVIEW_NOT_FOUND", "Revisão não encontrada.");
        return store.Find(projectId, reviewId) ?? throw new EngineException("REVIEW_NOT_FOUND", "Revisão não encontrada.");
    }

    // A justificativa é texto da pessoa: passa pela redação de segredos antes de ser gravada.
    private static string? Justification(string decision, string? text)
    {
        var trimmed = text?.Trim();
        var needed = FindingDecisions.RequiresJustification(decision) || decision == DeliveryDecisions.Approved;
        if (string.IsNullOrEmpty(trimmed)) return needed ? throw Short() : null;
        if (needed && trimmed.Length < MinJustification) throw Short();
        if (trimmed.Length > MaxJustification) throw new EngineException("REVIEW_JUSTIFICATION_INVALID", $"A justificativa passa de {MaxJustification} caracteres.");
        return SecretScanner.RedactLines(trimmed);
    }

    private static EngineException Short() =>
        new("REVIEW_JUSTIFICATION_INVALID", $"Escreva a justificativa (de {MinJustification} a {MaxJustification} caracteres).");

    private static ReviewView Build(ReviewRecord review, DecisionLog log)
    {
        // Vale a última decisão de cada achado, de qualquer revisão do projeto: o mesmo defeito no mesmo lugar tem o mesmo id.
        var latest = log.Findings.GroupBy(entry => entry.FindingId).ToDictionary(group => group.Key, group => group.Last());
        var applied = review.Findings.Where(finding => latest.ContainsKey(finding.Id)).Select(finding => latest[finding.Id]).ToList();

        bool Resolved(ReviewFinding finding) => latest.TryGetValue(finding.Id, out var d) && FindingDecisions.Resolves(d.Decision);
        var openBlocking = review.Findings.Count(finding => ReviewJudges.IsBlocking(finding) && !Resolved(finding));
        var openNeedsCheck = review.Findings.Count(finding => finding.Severity != QualitySeverities.Informational && !ReviewJudges.IsBlocking(finding) && !Resolved(finding));
        var ready = openBlocking == 0 && review.Verdict is not (ReviewVerdicts.Inconclusive or ReviewVerdicts.NotRun);
        var delivery = log.Deliveries.LastOrDefault(item => item.ReviewId == review.Id);
        return new ReviewView(review, applied, new ReviewStatus(review.Verdict, openBlocking, openNeedsCheck, ready, delivery));
    }
}
