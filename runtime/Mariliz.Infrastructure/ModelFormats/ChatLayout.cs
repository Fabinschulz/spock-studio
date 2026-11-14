using Mariliz.Application.Models;

namespace Mariliz.Infrastructure.ModelFormats;

/// <summary>
/// Os provedores que levam as instruções de sistema à parte precisam de ao menos uma mensagem do usuário: numa conversa só
/// de instruções, a instrução vira o pedido.
/// </summary>
internal sealed record ChatLayout(string? System, IReadOnlyList<ChatMessage> Conversation)
{
    public static ChatLayout Split(IReadOnlyList<ChatMessage> messages, params string[] extraInstructions)
    {
        var instructions = messages.Where(m => m.Role == "system").Select(m => m.Content).Concat(extraInstructions).ToList();
        var conversation = messages.Where(m => m.Role != "system").ToList();
        if (conversation.Count > 0) return new ChatLayout(instructions.Count > 0 ? string.Join("\n\n", instructions) : null, conversation);

        var request = instructions.Count > 0 ? string.Join("\n\n", instructions) : "Continue.";
        return new ChatLayout(null, [new ChatMessage("user", request)]);
    }
}
