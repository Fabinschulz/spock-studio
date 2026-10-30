using Mariliz.Application.Discovery;
using Mariliz.Application.Harness;
using Mariliz.Core.Discovery;
using Mariliz.Core.Plan;

namespace Mariliz.Application.Execution;

/// <summary>Plano calculado e guardado na memória do motor até ser aplicado ou descartado.</summary>
public sealed record PlanSession(string ProjectId, string Root, PlanOptions Options, ConfigurationPlan Plan, ComposeResult Composed);

public sealed record Replanned(ConfigurationPlan Plan, ComposeResult Composed, DiscoveryReport Discovery);

public sealed record ApplyApprovals(
    IReadOnlyList<string> ChangeIds,
    IReadOnlyList<string> CommandIds,
    IReadOnlyDictionary<string, ConflictResolution> Resolutions)
{
    public void EnsureBelongTo(ConfigurationPlan plan)
    {
        var changeIds = plan.Changes.Select(c => c.Id).ToHashSet();
        if (ChangeIds.Any(id => !changeIds.Contains(id)))
            throw new EngineException("UNKNOWN_CHANGE", "A aprovação cita uma mudança que não faz parte do plano.");

        if (CommandIds.Any(id => plan.Commands.All(c => c.Id != id)))
            throw new EngineException("UNKNOWN_COMMAND", "A aprovação cita um comando que não faz parte do plano.");

        foreach (var id in Resolutions.Keys)
            if (plan.Changes.FirstOrDefault(c => c.Id == id) is not { Action: ChangeAction.Conflict })
                throw new EngineException("INVALID_RESOLUTION", "Só conflitos aceitam uma decisão de manter ou substituir.");
    }
}

internal enum OutcomeKind { Written, Removed, Preserved, Kept, NotApplied }

internal sealed record FileOutcome(OutcomeKind Kind, string? Hash);
