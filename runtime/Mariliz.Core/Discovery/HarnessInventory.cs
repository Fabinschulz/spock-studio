namespace Mariliz.Core.Discovery;

/// <summary>O que o projeto já tem de agentes, skills, hooks, regras, MCPs e evals.</summary>
public sealed record HarnessInventory(
    IReadOnlyList<InventoryItem> Agents,
    IReadOnlyList<InventoryItem> Skills,
    IReadOnlyList<InventoryItem> Hooks,
    IReadOnlyList<InventoryItem> Rules,
    IReadOnlyList<InventoryItem> McpServers,
    IReadOnlyList<InventoryItem> Evals)
{
    public static HarnessInventory Empty { get; } = new([], [], [], [], [], []);
}
