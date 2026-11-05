namespace Mariliz.Application.Execution;

/// <summary>Uso do modelo numa execução: chamadas e tokens, contados pelo proxy do motor. Sem custo em dinheiro (D4).</summary>
public sealed class JournalModelUsage
{
    public int Calls { get; set; }
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
}
