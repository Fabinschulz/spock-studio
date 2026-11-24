using System.Text.Json.Nodes;
using Mariliz.Application.Compatibility;
using Mariliz.Application.Execution;
using Mariliz.Application.Management;
using Mariliz.Application.Ports;
using Mariliz.Application.Workspace;

namespace Mariliz.Application.Analysis;

public static class ModernizationDecisions
{
    public const string Approved = "approved";
    public const string Rejected = "rejected";
}

/// <param name="Decision">approved ou rejected (ModernizationDecisions).</param>
/// <param name="DecisionId">O registro na memória de decisões do projeto, com os fatos do momento.</param>
public sealed record ModernizationApproval(string StepId, string Fingerprint, string Decision, string DecisionId, DateTimeOffset RecordedAt);

// Fica na pasta de dados do Spock, não no projeto: o agente que escreve o plano não pode aprovar os próprios passos.
public interface IModernizationApprovalStore
{
    IReadOnlyList<ModernizationApproval> List(string projectId);

    // Guarda a última decisão de cada passo.
    void Save(string projectId, ModernizationApproval approval);
}

public static class ModernizationStepStatuses
{
    public const string Pending = "pending";
    public const string Approved = ModernizationDecisions.Approved;
    public const string Rejected = ModernizationDecisions.Rejected;
    // Decidido, mas o passo mudou depois: a decisão não vale para o texto novo.
    public const string Changed = "changed";
}

public sealed record ModernizationStepState(string StepId, string Status, string? DecisionId, DateTimeOffset? RecordedAt);

/// <param name="PlanPath">Onde o agente grava o plano, relativo ao projeto, mesmo antes de existir.</param>
/// <param name="Versions">MV-7: o que está numa linha mais antiga que a recomendada, com o caminho de saltos.</param>
public sealed record ProjectModernization(ArchitectureAssessment? Assessment, ModernizationPlan? Plan, string PlanPath,
    IReadOnlyList<ModernizationStepState> Steps, IReadOnlyList<VersionFinding> Versions);

// A pessoa aprova ou recusa cada passo do plano do agente. Cada decisão vai para a memória de decisões do projeto, e o agente
// só executa o que está aprovado para o texto atual do passo.
public sealed class ModernizationService(
    WorkspaceAuthorizer workspace,
    IProjectFilesFactory filesFactory,
    IClock clock,
    DecisionService decisions,
    IModernizationApprovalStore approvals,
    ReleaseCatalog? catalog = null)
{
    public ProjectModernization Read(string projectId)
    {
        if (!workspace.TryGetRoot(projectId, out var root)) throw EngineErrors.ProjectNotAuthorized();
        return Read(projectId, filesFactory.Open(root), clock.UtcNow, approvals.List(projectId), catalog);
    }

    /// <param name="catalog">Nulo é o catálogo embutido; os testes passam um fixo, para os saltos não mudarem a cada geração.</param>
    public static ProjectModernization Read(string projectId, IProjectFiles files, DateTimeOffset now, IReadOnlyList<ModernizationApproval> approvals,
        ReleaseCatalog? catalog = null)
    {
        var report = ArchitectureAnalyzer.Analyze(projectId, files, now);
        var assessment = ArchitectureAssessments.Read(files, report);
        var versions = VersionUpgrades.Find(files, catalog ?? ReleaseCatalog.Embedded, now);
        var plan = ModernizationPlans.Read(files, assessment, versions);
        var latest = approvals.GroupBy(approval => approval.StepId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.MaxBy(approval => approval.RecordedAt)!, StringComparer.Ordinal);
        var states = (plan?.Steps ?? []).Where(step => step.Id.Length > 0).Select(step => State(step, latest.GetValueOrDefault(step.Id))).ToList();
        return new ProjectModernization(assessment, plan, ModernizationPlans.PathIn(files), states, versions);
    }

    public ProjectModernization Decide(string projectId, string stepId, bool approve, string? rationale)
    {
        var current = Read(projectId);
        if (current.Plan is not { Valid: true } plan)
            throw new EngineException("MODERNIZATION_PLAN_INVALID", "O plano de modernização não existe ou não passa nas verificações do Spock.");
        var step = plan.Steps.FirstOrDefault(item => item.Id == stepId)
            ?? throw new EngineException("MODERNIZATION_STEP_NOT_FOUND", "O passo não existe no plano atual.");
        if (approve)
        {
            var pending = step.DependsOn.Where(id => current.Steps.FirstOrDefault(state => state.StepId == id)?.Status != ModernizationStepStatuses.Approved).ToList();
            if (pending.Count > 0)
                throw new EngineException("MODERNIZATION_DEPENDENCY_PENDING", $"Aprove antes os passos de que este depende: {string.Join(", ", pending)}.");
        }

        var verb = approve ? "Aprovado" : "Recusado";
        var (record, _) = decisions.Record(projectId, Limit($"Modernização {step.Id}: {step.Title}", DecisionService.MaxTitle),
            Limit($"{verb} o passo {step.Id} do plano de modernização: {step.Change}", DecisionService.MaxDecision), rationale, []);
        approvals.Save(projectId, new ModernizationApproval(step.Id, step.Fingerprint,
            approve ? ModernizationDecisions.Approved : ModernizationDecisions.Rejected, record.Id, record.RecordedAt));
        return Read(projectId);
    }

    public static JsonObject Json(ProjectModernization modernization) => new()
    {
        ["assessment"] = modernization.Assessment is null ? null : new JsonObject
        {
            ["valid"] = modernization.Assessment.Valid,
            ["divergences"] = new JsonArray(modernization.Assessment.Divergences
                .Select(divergence => (JsonNode)new JsonObject { ["id"] = divergence.Id, ["title"] = divergence.Title, ["classification"] = divergence.Classification })
                .ToArray())
        },
        ["plan"] = modernization.Plan is null ? null : JournalJson.Node(modernization.Plan),
        ["planPath"] = modernization.PlanPath,
        ["steps"] = JournalJson.Node(modernization.Steps),
        ["versions"] = JournalJson.Node(modernization.Versions)
    };

    private static ModernizationStepState State(ModernizationStep step, ModernizationApproval? approval) =>
        approval is null ? new ModernizationStepState(step.Id, ModernizationStepStatuses.Pending, null, null)
        : approval.Fingerprint != step.Fingerprint ? new ModernizationStepState(step.Id, ModernizationStepStatuses.Changed, approval.DecisionId, approval.RecordedAt)
        : new ModernizationStepState(step.Id, approval.Decision, approval.DecisionId, approval.RecordedAt);

    private static string Limit(string text, int max) => text.Length <= max ? text : text[..(max - 1)] + "…";
}
