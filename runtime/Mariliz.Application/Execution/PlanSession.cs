using Mariliz.Application.Harness;
using Mariliz.Core.Plan;

namespace Mariliz.Application.Execution;

/// <summary>Plano calculado e guardado na memória do motor até ser aplicado ou descartado.</summary>
public sealed record PlanSession(string ProjectId, string Root, PlanOptions Options, ConfigurationPlan Plan, ComposeResult Composed);
