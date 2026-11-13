namespace Mariliz.Application.Integrations;

/// <summary>
/// O marco a que o item pertence: o milestone do GitHub ou a versão de correção (fix version) do Jira. O prazo é o do marco no
/// provedor, quando ele tem um.
/// </summary>
public sealed record WorkItemMilestone(string Name, DateOnly? DueDate);
