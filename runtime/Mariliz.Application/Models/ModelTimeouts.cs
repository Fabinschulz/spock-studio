namespace Mariliz.Application.Models;

// Uma conversa com o modelo tem dois relógios: o do motor (o que corta a chamada ao provedor) e o do cliente Python do runtime de IA
// (o que espera o motor). Se o do Python for menor, ele corta antes e a resposta do modelo, mesmo chegando, é perdida; por isso o do
// Python é derivado do do motor, e os dois saem de um único valor.
public static class ModelTimeouts
{
    public const string ChatVariable = "MARILIZ_MODEL_CHAT_TIMEOUT_SECONDS";

    public static readonly TimeSpan MinChat = TimeSpan.FromSeconds(10);
    public static readonly TimeSpan MaxChat = TimeSpan.FromMinutes(10);

    // Modelos de topo levam dezenas de segundos para escrever uma conclusão; 30 s cortava a maioria das respostas longas.
    public static readonly TimeSpan DefaultChat = TimeSpan.FromSeconds(120);

    // O Python espera um pouco mais que o motor, para receber o MODEL_TIMEOUT dele em vez de cortar a conexão primeiro.
    public static readonly TimeSpan ClientMargin = TimeSpan.FromSeconds(5);

    public static TimeSpan Chat { get; } = Resolve(Environment.GetEnvironmentVariable(ChatVariable));

    public static TimeSpan Client => Chat + ClientMargin;

    // Um valor ausente, ilegível ou fora da faixa volta ao padrão: um prazo absurdo não pode travar nem cortar toda conversa.
    internal static TimeSpan Resolve(string? raw) =>
        int.TryParse(raw, out var seconds) && TimeSpan.FromSeconds(seconds) is var value && value >= MinChat && value <= MaxChat
            ? value
            : DefaultChat;
}
