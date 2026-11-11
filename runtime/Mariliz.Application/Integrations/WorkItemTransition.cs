namespace Mariliz.Application.Integrations;

/// <summary>Uma mudança de status registrada pelo provedor.</summary>
public sealed record WorkItemTransition(DateTimeOffset At, string From, string To);
