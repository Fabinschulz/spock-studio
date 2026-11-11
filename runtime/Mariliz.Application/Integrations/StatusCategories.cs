namespace Mariliz.Application.Integrations;

/// <summary>
/// Situação de um item numa escala comum aos provedores. O texto original fica em <see cref="WorkItem.Status"/>; a categoria só
/// repete o que o provedor declara (categoria do status no Jira; aberto ou fechado no GitHub) e não infere andamento.
/// </summary>
public static class StatusCategories
{
    public const string Todo = "todo";
    public const string InProgress = "inProgress";
    public const string Done = "done";
}
