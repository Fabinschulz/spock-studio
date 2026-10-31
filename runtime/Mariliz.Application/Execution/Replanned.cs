using Mariliz.Application.Harness;
using Mariliz.Core.Discovery;
using Mariliz.Core.Plan;

namespace Mariliz.Application.Execution;

public sealed record Replanned(ConfigurationPlan Plan, ComposeResult Composed, DiscoveryReport Discovery);
