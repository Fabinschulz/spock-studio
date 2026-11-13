namespace Mariliz.Application.Integrations;

public sealed record WorkItemTransition(DateTimeOffset At, string From, string To);
