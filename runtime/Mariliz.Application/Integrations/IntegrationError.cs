namespace Mariliz.Application.Integrations;

// Nunca carrega resposta do provedor nem credencial.
public sealed record IntegrationError(string Code, string Message, DateTimeOffset At);
