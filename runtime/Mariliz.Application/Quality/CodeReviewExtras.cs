using Mariliz.Application.Execution;
using Mariliz.Application.Quality.Git;

namespace Mariliz.Application.Quality;

/// <summary>
/// O que a pessoa pede além da revisão determinística. Nada disso roda sozinho: o Boot e o MCP só chamam a revisão simples.
/// </summary>
/// <param name="UseModel">Envia o diff, sem as linhas com cara de segredo, ao provedor de IA configurado.</param>
/// <param name="Requirements">Critérios de aceite, um por linha; só são conferidos com a revisão por modelo.</param>
/// <param name="GateIds">Ids de <see cref="QualityGateCatalog"/>; só rodam com <paramref name="GatesApproved"/>.</param>
public sealed record ReviewOptions(bool UseModel = false, string? Requirements = null, IReadOnlyList<string>? GateIds = null, bool GatesApproved = false)
{
    public static ReviewOptions Default { get; } = new();

    public IReadOnlyList<string> Gates => GateIds ?? [];

    public string? RequirementsText => string.IsNullOrWhiteSpace(Requirements) ? null : Requirements.Trim();
}

internal sealed record Extras(
    IReadOnlyList<ModelRun>? ModelRuns,
    IReadOnlyList<RequirementCheck>? Requirements,
    IReadOnlyList<GateResult>? Gates,
    IReadOnlyDictionary<string, JudgeResult>? Judges,
    bool GatesRequestedNotRun,
    bool OptionalRequestedNotRun,
    bool ModelRan,
    bool RequirementsRan);

public sealed partial class CodeReviewService
{
    private async Task<Extras> RunExtrasAsync(string root, ChangeSet changes, ReviewOptions options, List<FindingDraft> drafts, List<string> limitations, CancellationToken ct)
    {
        if (options.RequirementsText is { Length: > ModelReviewer.MaxRequirementChars })
            throw new EngineException("REVIEW_REQUIREMENTS_TOO_LONG", $"Os critérios de aceite passam de {ModelReviewer.MaxRequirementChars} caracteres.");

        var judges = new Dictionary<string, JudgeResult>();
        var modelRuns = new List<ModelRun>();
        List<RequirementCheck>? checks = null;
        var optionalNotRun = false;
        var modelRan = false;
        var requirementsRan = false;

        var wantsRequirements = options.RequirementsText is not null;
        if ((options.UseModel || wantsRequirements) && modelReviewer is { Available: true })
        {
            limitations.Add("O modelo que revisa é o provedor configurado no Spock. O Spock não sabe qual modelo escreveu a alteração, então a independência entre quem escreveu e quem revisou não é verificada; e o que o modelo aponta é hipótese, nunca bloqueia.");
        }

        if (options.UseModel)
        {
            var outcome = modelReviewer is null ? new ModelReviewOutcome(new ModelRun("review", ModelRunStates.Skipped, null, null, null, 0, 0, 0, 0, "MODEL_NOT_CONFIGURED"), [])
                                               : await modelReviewer.ReviewAsync(changes, ct);
            modelRuns.Add(outcome.Run);
            foreach (var found in outcome.Findings)
                drafts.Add(new FindingDraft(ReviewRules.ModelFinding, found.Path, found.Line, found.Excerpt, EvidenceRelations.AddedLine, null, found.Explanation,
                    "model-review", found.Severity, null, null, found.Category, found.Title, null, "model"));
            modelRan = outcome.Run.State == ModelRunStates.Used;
            optionalNotRun |= !modelRan;
            judges[JudgeIds.Model] = ModelJudge(outcome);
            if (outcome.Run.Discarded > 0) limitations.Add($"{outcome.Run.Discarded} resposta(s) do modelo foram descartadas por não citarem uma linha adicionada do diff.");
        }

        if (wantsRequirements)
        {
            var outcome = modelReviewer is null ? new RequirementsOutcome(new ModelRun("requirements", ModelRunStates.Skipped, null, null, null, 0, 0, 0, 0, "MODEL_NOT_CONFIGURED"), [])
                                                : await modelReviewer.CheckRequirementsAsync(changes, options.RequirementsText!, ct);
            modelRuns.Add(outcome.Run);
            requirementsRan = outcome.Run.State == ModelRunStates.Used;
            optionalNotRun |= !requirementsRan;
            checks = [.. outcome.Checks];
            foreach (var check in checks.Where(check => check.Status != RequirementStatuses.Met))
                drafts.Add(RequirementDraft(check));
            judges[JudgeIds.Requirements] = RequirementsJudge(outcome);
            if (outcome.Run.Discarded > 0) limitations.Add($"{outcome.Run.Discarded} resposta(s) do modelo sobre os critérios foram rebaixadas para \"sem evidência\" por não apontarem uma linha do diff.");
        }

        IReadOnlyList<GateResult>? gates = null;
        var gatesNotRun = false;
        if (options.Gates.Count > 0)
        {
            if (gateRunner is null) throw new EngineException("REVIEW_GATES_UNAVAILABLE", "Esta sessão do motor não executa verificações do projeto.");
            gates = await gateRunner.RunAsync(root, options.Gates, options.GatesApproved, ct);
            foreach (var gate in gates.Where(gate => gate.State is GateStates.Failed or GateStates.TimedOut)) drafts.Add(GateDraft(gate));
            gatesNotRun = gates.Any(gate => gate.State is GateStates.Blocked or GateStates.NotRun or GateStates.TimedOut or GateStates.Cancelled);
            judges[JudgeIds.Execution] = ExecutionJudge(gates);
            limitations.Add("As verificações rodaram como processos filhos do Spock, sem sandbox, na pasta do projeto, sem as variáveis de ambiente com cara de segredo. Rodam código do próprio projeto: só rodaram porque você autorizou.");
        }

        return new Extras(modelRuns.Count == 0 ? null : modelRuns, checks, gates, judges.Count == 0 ? null : judges, gatesNotRun, optionalNotRun, modelRan, requirementsRan);
    }

    private static bool RuleRan(ReviewRule rule, Extras extras) => rule.Judge switch
    {
        JudgeIds.Model => extras.ModelRan,
        JudgeIds.Requirements => extras.RequirementsRan,
        JudgeIds.Execution => extras.Gates is not null,
        _ => true
    };

    private static JudgeResult ModelJudge(ModelReviewOutcome outcome)
    {
        string[] criteria = ["Um modelo procura defeitos que os padrões não alcançam; o que ele aponta só vale com linha do diff e nunca bloqueia."];
        if (outcome.Run.State != ModelRunStates.Used)
            return new JudgeResult(JudgeIds.Model, "Revisão por modelo de IA", ReviewVerdicts.NotRun, FailureText(outcome.Run.FailureCode), criteria, [], false);
        var reason = outcome.Findings.Count == 0
            ? $"O modelo {outcome.Run.Model} não apontou defeito nas linhas adicionadas (isso não prova que não há)."
            : $"{outcome.Findings.Count} hipótese(s) do modelo {outcome.Run.Model}, todas com linha do diff; nenhuma bloqueia.";
        return new JudgeResult(JudgeIds.Model, "Revisão por modelo de IA", outcome.Findings.Count == 0 ? ReviewVerdicts.Approved : ReviewVerdicts.ApprovedWithWarnings, reason, criteria, [], false);
    }

    private static JudgeResult RequirementsJudge(RequirementsOutcome outcome)
    {
        string[] criteria = ["A alteração atende os critérios de aceite, e dá para apontar a linha do diff que atende cada um."];
        if (outcome.Run.State != ModelRunStates.Used)
            return new JudgeResult(JudgeIds.Requirements, "Requisitos", ReviewVerdicts.NotRun, FailureText(outcome.Run.FailureCode), criteria, [], false);
        var met = outcome.Checks.Count(check => check.Status == RequirementStatuses.Met);
        var rest = outcome.Checks.Count - met;
        return new JudgeResult(JudgeIds.Requirements, "Requisitos", rest == 0 ? ReviewVerdicts.Approved : ReviewVerdicts.ApprovedWithWarnings,
            rest == 0 ? $"Os {met} critério(s) têm uma linha do diff como evidência."
                      : $"{met} de {outcome.Checks.Count} critério(s) com evidência no diff; {rest} sem evidência, parcial ou não atendido (hipótese do modelo, a conferir).",
            criteria, [], false);
    }

    private static JudgeResult ExecutionJudge(IReadOnlyList<GateResult> gates)
    {
        string[] criteria = ["As verificações que o próprio projeto define (build, testes, lint) passam com a alteração."];
        var failed = gates.Count(gate => gate.State == GateStates.Failed);
        var notRun = gates.Count(gate => gate.State is GateStates.Blocked or GateStates.NotRun or GateStates.TimedOut or GateStates.Cancelled);
        var verdict = failed > 0 ? ReviewVerdicts.Rejected : notRun > 0 ? ReviewVerdicts.Inconclusive : ReviewVerdicts.Approved;
        var reason = failed > 0 ? $"{failed} verificação(ões) falharam: é um fato do projeto, não uma opinião."
            : notRun > 0 ? $"{notRun} verificação(ões) não rodaram ou não terminaram; sem o resultado delas não há aprovação."
            : $"As {gates.Count} verificação(ões) passaram.";
        return new JudgeResult(JudgeIds.Execution, "Verificações do projeto", verdict, reason, criteria, [], true);
    }

    private static FindingDraft RequirementDraft(RequirementCheck check)
    {
        var status = check.Status switch { RequirementStatuses.Partial => "atendido em parte", RequirementStatuses.NotMet => "não atendido", _ => "sem evidência" };
        var onLine = check.Path is not null && check.Line is not null;
        return new FindingDraft(ReviewRules.RequirementNotMet, check.Path ?? "requisitos", check.Line, check.Excerpt ?? ChangeDetectors.Excerpt(check.Text),
            onLine ? EvidenceRelations.AddedLine : EvidenceRelations.Requirement, null,
            $"Critério {check.Index} ({status}): {check.Text}{(string.IsNullOrWhiteSpace(check.Note) ? "" : $" Observação do modelo: {check.Note}")}",
            "requirements-judge", check.Status == RequirementStatuses.Unknown ? QualitySeverities.Low : QualitySeverities.Medium, null, null, null, null, null, "model");
    }

    private static FindingDraft GateDraft(GateResult gate)
    {
        var timedOut = gate.State == GateStates.TimedOut;
        var last = gate.LogTail?.Split('\n').LastOrDefault(line => !string.IsNullOrWhiteSpace(line));
        return new FindingDraft(ReviewRules.GateFailed, gate.Directory, null, ChangeDetectors.Excerpt(last ?? gate.Command), EvidenceRelations.GateOutput, null,
            $"`{gate.Command}` ({gate.Origin}): {gate.Summary}", "quality-gate",
            timedOut ? QualitySeverities.Medium : null, timedOut ? FindingConfidences.Heuristic : null, null, null, $"Verificação do projeto {(timedOut ? "não terminou" : "falhou")}: {gate.Kind}", null, null);
    }

    private static string FailureText(string? code) => code switch
    {
        "MODEL_NOT_CONFIGURED" => "Nenhum provedor de IA está configurado: configure um em Configurações para pedir a revisão por modelo.",
        "MODEL_TIMEOUT" => "O modelo não respondeu no prazo; a revisão por modelo não rodou.",
        "MODEL_OUTPUT_INVALID" => "A resposta do modelo veio fora do formato esperado e foi descartada.",
        "NOTHING_TO_REVIEW" => "Não havia linha adicionada em arquivo analisado para enviar ao modelo.",
        null => "A revisão por modelo não rodou.",
        _ => $"O provedor falhou ({code}); a revisão por modelo não rodou."
    };
}
