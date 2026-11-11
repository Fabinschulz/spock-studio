namespace Mariliz.Application.Integrations;

/// <summary>Uma falha como a pessoa a vê: código estável e texto fixo do motor. Nunca carrega resposta do provedor nem credencial.</summary>
public sealed record IntegrationError(string Code, string Message, DateTimeOffset At);
