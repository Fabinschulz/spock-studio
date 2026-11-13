using Mariliz.Application.Harness;
using Mariliz.Core.Plan;

namespace Mariliz.Application.Execution;

public sealed record PlanSession(string ProjectId, string Root, PlanOptions Options, ConfigurationPlan Plan, ComposeResult Composed);
