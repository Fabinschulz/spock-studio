namespace Mariliz.Core.Discovery;

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
