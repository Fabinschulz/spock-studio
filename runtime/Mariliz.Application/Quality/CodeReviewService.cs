using System.Text.Json.Nodes;
using Mariliz.Application.Analysis;
using Mariliz.Application.Audit;
using Mariliz.Application.Context;
using Mariliz.Application.Engineering;
using Mariliz.Application.Execution;
using Mariliz.Application.Integrations;
using Mariliz.Application.Ports;
using Mariliz.Application.Quality.Git;
using Mariliz.Application.Workspace;

namespace Mariliz.Application.Quality;

/// <summary>
/// A revisão de código do próprio motor: lê o que mudou (só leitura), aplica os detectores e a análise de arquitetura, deixa os
/// juízes decidirem e guarda o resultado na pasta de dados do Spock. Nada no projeto analisado é escrito, e nenhum comando do
/// projeto roda. O que não pôde ser feito aparece como limitação no relatório, nunca como aprovação.
/// </summary>
public sealed class CodeReviewService(
    WorkspaceAuthorizer workspace,
    GitChangeReader git,
    IReviewStore store,
    IClock clock,
    IProjectFilesFactory filesFactory,
    IAuditLog audit,
    HarnessStatusService? harness = null,
    ContextEngine? context = null)
{
    public const int MaxFindings = 200;
    public const int MaxStored = 100;

    public async Task<ReviewRecord> ReviewWorkingTreeAsync(string projectId, CancellationToken ct)
    {
        var root = RequireRoot(projectId);
        var changes = await git.ReadWorkingTreeAsync(root, ct);
        return Complete(projectId, root, changes, clock.UtcNow);
    }

    public async Task<ReviewRecord> ReviewBranchAsync(string projectId, string baseRef, string? headRef, CancellationToken ct)
    {
        var root = RequireRoot(projectId);
        var changes = await git.ReadBranchAsync(root, baseRef, headRef, ct);
        return Complete(projectId, root, changes, clock.UtcNow);
    }

    public IReadOnlyList<ReviewRecord> List(string projectId, int limit = 20)
    {
        RequireRoot(projectId);
        return [.. store.List(projectId).Take(Math.Clamp(limit, 1, MaxStored))];
    }

    public ReviewRecord Detail(string projectId, string reviewId)
    {
        RequireRoot(projectId);
        if (!ReviewIds.IsValid(reviewId)) throw new EngineException("REVIEW_NOT_FOUND", "Revisão não encontrada.");
        return store.Find(projectId, reviewId) ?? throw new EngineException("REVIEW_NOT_FOUND", "Revisão não encontrada.");
    }

    private string RequireRoot(string projectId) => workspace.TryGetRoot(projectId, out var root) ? root : throw EngineErrors.ProjectNotAuthorized();

    private ReviewRecord Complete(string projectId, string root, ChangeSet changes, DateTimeOffset startedAt)
    {
        var reviewId = ReviewIds.New();
        var limitations = new List<string>(changes.Limitations);
        var files = filesFactory.Open(root);

        var drafts = new List<FindingDraft>(ChangeDetectors.Run(changes));
        var architectureNotRun = ArchitectureDrafts(projectId, files, changes, drafts, limitations);
        var findings = Build(reviewId, projectId, changes, drafts, files, limitations);

        var analyzed = changes.Files.Count(file => file.Analyzable);
        var input = new JudgeInput(findings, changes.Files.Count + changes.Conflicts.Count, analyzed, changes.Truncated, limitations, architectureNotRun);
        var judges = ReviewJudges.Evaluate(input);
        var final = judges[^1];

        limitations.Add("Nenhum comando do projeto foi executado: build, testes e lint não rodaram nesta revisão (quality gates: Q2).");
        limitations.Add("Os detectores olham padrões nas linhas adicionadas; não seguem o fluxo dos dados nem leem código fora do diff.");
        limitations.Add("As regras do Harness são texto para os agentes; o motor aplica as regras QR-* e a análise de arquitetura, e ainda não verifica cada regra do Harness (modelo de regras: Q4).");

        var consulted = Consulted(projectId);
        var finished = clock.UtcNow;
        var record = new ReviewRecord(
            ReviewContract.Version, reviewId, projectId, changes.Mode, changes.Branch, changes.Base, changes.Head, changes.HeadCommit, startedAt, finished,
            final.Verdict, Summary(final, findings, analyzed), Counts(changes, findings, analyzed), FileList(changes), findings, judges,
            [.. ReviewRules.All.Select(rule => rule.Id)], consulted, [.. limitations.Distinct(StringComparer.Ordinal)], NextActions(findings));

        store.Save(record);
        Prune(projectId);
        audit.Record(AuditActions.QualityReviewed, new JsonObject
        {
            ["projectId"] = projectId,
            ["reviewId"] = record.Id,
            ["mode"] = record.Mode,
            ["verdict"] = record.Verdict,
            ["files"] = record.Counts.Files,
            ["findings"] = record.Counts.Findings
        });
        return record;
    }

    // Devolve por que a análise não rodou (ou nulo). O que ela acha vale para o arquivo alterado, não para a linha: a análise
    // lê o projeto inteiro e não separa o que já existia antes da alteração.
    private static string? ArchitectureDrafts(string projectId, IProjectFiles files, ChangeSet changes, List<FindingDraft> drafts, List<string> limitations)
    {
        var changed = changes.Files.Where(file => file.Status != FileStatuses.Deleted && file.SkippedReason != SkipReasons.Generated && file.SkippedReason != SkipReasons.ThirdParty)
            .Select(file => file.Path).ToHashSet(StringComparer.Ordinal);
        if (changed.Count == 0) return "Nenhum arquivo de código alterado para a análise de arquitetura olhar.";

        ArchitectureReport report;
        try
        {
            report = ArchitectureAnalyzer.Analyze(projectId, files, DateTimeOffset.UtcNow);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            limitations.Add($"A análise de arquitetura falhou ({ex.GetType().Name}) e não entrou na revisão.");
            return "A análise de arquitetura falhou.";
        }
        if (report.DotnetProjects.Count == 0 && report.Frontends.Count == 0)
            return "Nenhum projeto .NET nem frontend reconhecido: a análise de arquitetura não se aplica a este projeto.";

        foreach (var finding in report.Findings)
        {
            // "Sem lint de camadas" e "projeto sem teste" falam do projeto, não de um arquivo alterado.
            if (finding.Code is ArchitectureFindingCodes.NoLayerLint or ArchitectureFindingCodes.UntestedProject or ArchitectureFindingCodes.UntestedFeature) continue;
            foreach (var evidence in finding.Evidence.Where(item => changed.Contains(item.Path)))
            {
                var severity = finding.Severity == ArchitectureSeverities.Warning ? QualitySeverities.Medium : QualitySeverities.Informational;
                drafts.Add(new FindingDraft(ReviewRules.Architecture, evidence.Path, null, evidence.Detail ?? finding.Title, EvidenceRelations.ProjectAnalysis, null,
                    $"{finding.Title}.", "architecture-analyzer", severity, null, finding.Code));
            }
        }
        return null;
    }

    private List<ReviewFinding> Build(string reviewId, string projectId, ChangeSet changes, List<FindingDraft> drafts, IProjectFiles files, List<string> limitations)
    {
        var now = clock.UtcNow;
        var byLine = changes.Files.SelectMany(file => file.AddedLines.Where(line => line.NewNumber is not null).Select(line => ((file.Path, line.NewNumber!.Value), line.Text)))
            .GroupBy(pair => pair.Item1).ToDictionary(group => group.Key, group => group.First().Text);
        var currentLines = new Dictionary<string, string[]?>(StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<ReviewFinding>();
        var stale = 0;

        foreach (var draft in drafts)
        {
            var rule = ReviewRules.Find(draft.RuleId);
            var id = ReviewIds.FindingId(rule.Id, draft.Path, draft.Line, draft.Excerpt);
            if (!seen.Add(id)) continue;

            // A linha tem de existir de verdade: na pasta de trabalho, o arquivo pode ter mudado desde que o diff foi lido.
            if (changes.Mode == ReviewModes.WorkingTree && draft.Relation == EvidenceRelations.AddedLine && draft.Line is { } line
                && byLine.TryGetValue((draft.Path, line), out var original) && !LineStillExists(files, currentLines, draft.Path, line, original))
            {
                stale++;
                continue;
            }

            var onChanged = draft.Relation == EvidenceRelations.AddedLine;
            result.Add(new ReviewFinding(
                id, rule.Id, reviewId, projectId, draft.Path, draft.Line, draft.Line, onChanged, rule.Category,
                draft.Severity ?? rule.Severity, draft.Confidence ?? rule.Confidence, rule.Title, Description(rule, draft),
                new FindingEvidence(draft.Excerpt, draft.Relation, Origin(draft), draft.Symbol, Consulted(draft)),
                rule.Expected, draft.Actual, rule.Suggestion, null, draft.ArchitectureRule is { } code ? $"architecture:{code}" : null,
                "deterministic", draft.Detector, FindingStatuses.Open, now));
        }

        if (stale > 0) limitations.Add($"{stale} achado(s) foram descartados porque o arquivo mudou enquanto a revisão rodava; revise de novo.");
        var ordered = result.OrderByDescending(finding => QualitySeverities.Rank(finding.Severity))
            .ThenBy(finding => finding.Confidence == FindingConfidences.Confirmed ? 0 : finding.Confidence == FindingConfidences.Heuristic ? 1 : 2)
            .ThenBy(finding => finding.Path, StringComparer.Ordinal).ThenBy(finding => finding.StartLine ?? 0).ToList();
        if (ordered.Count > MaxFindings)
        {
            limitations.Add($"A revisão achou {ordered.Count} pontos; os {MaxFindings} mais graves ficaram no relatório.");
            ordered = ordered[..MaxFindings];
        }
        return ordered;
    }

    private static bool LineStillExists(IProjectFiles files, Dictionary<string, string[]?> cache, string path, int line, string original)
    {
        if (!cache.TryGetValue(path, out var lines))
        {
            lines = files.ReadText(path)?.Split('\n');
            cache[path] = lines;
        }
        return lines is not null && line >= 1 && line <= lines.Length && lines[line - 1].TrimEnd('\r').Trim() == original.Trim();
    }

    private static string Description(ReviewRule rule, FindingDraft draft) =>
        draft.RuleId == ReviewRules.Architecture ? draft.Actual : $"{rule.Title}. {draft.Actual}";

    private static string Origin(FindingDraft draft) => draft.Relation switch
    {
        EvidenceRelations.ProjectAnalysis => "análise de arquitetura do projeto",
        EvidenceRelations.ChangedFile or EvidenceRelations.RemovedLines => "lista de arquivos do diff",
        _ => "linhas adicionadas no diff"
    };

    private static IReadOnlyList<string> Consulted(FindingDraft draft) => draft.Relation switch
    {
        EvidenceRelations.ProjectAnalysis => ["O projeto inteiro (a análise não separa o que já existia antes da alteração)."],
        EvidenceRelations.AddedLine => ["O diff e as linhas ao redor da alteração."],
        _ => ["A lista de arquivos alterados do diff."]
    };

    private List<string> Consulted(string projectId)
    {
        var consulted = new List<string>();
        try
        {
            if (harness is not null)
            {
                var rules = harness.Inspect(projectId).Components.Where(component => component.Kind.Equals("rule", StringComparison.OrdinalIgnoreCase) && component.State != "missing").ToList();
                consulted.Add(rules.Count == 0
                    ? "Harness: nenhuma regra aplicada neste projeto."
                    : $"Harness: {rules.Count} regra(s) presente(s); o texto delas é contexto para os agentes e não é verificado linha a linha pelo motor.");
            }
            if (context is not null)
                consulted.Add($"Contexto do projeto, versão {context.Compose(projectId, ContextIntents.Bootstrap).ContextVersion}.");
        }
        catch (EngineException)
        {
            consulted.Add("O contexto do projeto não pôde ser lido nesta revisão.");
        }
        return consulted;
    }

    private static ReviewCounts Counts(ChangeSet changes, IReadOnlyList<ReviewFinding> findings, int analyzed)
    {
        var graded = findings.Where(finding => finding.Severity != QualitySeverities.Informational).ToList();
        return new ReviewCounts(
            changes.Files.Count + changes.Conflicts.Count, analyzed, findings.Count,
            graded.Count(finding => finding.Confidence == FindingConfidences.Confirmed),
            graded.Count(finding => finding.Confidence != FindingConfidences.Confirmed),
            findings.Count - graded.Count,
            QualitySeverities.All.ToDictionary(severity => severity, severity => findings.Count(finding => finding.Severity == severity)));
    }

    private static List<ReviewFile> FileList(ChangeSet changes) =>
    [
        .. changes.Files.Select(file => new ReviewFile(file.Path, file.OldPath, file.Status, file.Added, file.Removed, file.Analyzable, file.SkippedReason)),
        .. changes.Conflicts.Select(path => new ReviewFile(path, null, FileStatuses.Conflicted, 0, 0, false, SkipReasons.Conflict))
    ];

    private static string Summary(JudgeResult final, IReadOnlyList<ReviewFinding> findings, int analyzed)
    {
        var head = final.Verdict switch
        {
            ReviewVerdicts.Approved => "Aprovada",
            ReviewVerdicts.ApprovedWithWarnings => "Aprovada com ressalvas",
            ReviewVerdicts.Rejected => "Reprovada",
            ReviewVerdicts.Inconclusive => "Inconclusiva",
            _ => "Não executada"
        };
        return $"{head}: {final.Reason} {(analyzed == 1 ? "1 arquivo analisado" : $"{analyzed} arquivos analisados")}, {(findings.Count == 1 ? "1 achado" : $"{findings.Count} achados")}.";
    }

    private static List<string> NextActions(IReadOnlyList<ReviewFinding> findings)
    {
        var actions = new List<string>();
        foreach (var finding in findings.Where(ReviewJudges.IsBlocking).Take(5))
            actions.Add($"Corrija antes de seguir: {finding.Title} em {Where(finding)}.");
        var warnings = findings.Count(finding => finding.Severity != QualitySeverities.Informational && !ReviewJudges.IsBlocking(finding));
        if (warnings > 0) actions.Add($"Confira {(warnings == 1 ? "1 ponto" : $"{warnings} pontos")} que o motor não consegue provar sozinho.");
        actions.Add("Rode o build e os testes do projeto: o Spock não os executou nesta revisão.");
        return actions;
    }

    private static string Where(ReviewFinding finding) => finding.StartLine is { } line ? $"{finding.Path}:{line}" : finding.Path;

    private void Prune(string projectId)
    {
        foreach (var old in store.List(projectId).Skip(MaxStored)) store.Delete(projectId, old.Id);
    }
}
