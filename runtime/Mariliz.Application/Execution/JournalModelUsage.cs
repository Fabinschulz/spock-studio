namespace Mariliz.Application.Execution;

// Sem custo em dinheiro (D4).
public sealed class JournalModelUsage
{
    public int Calls { get; set; }
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
}
