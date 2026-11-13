namespace Mariliz.Application.Integrations;

// A categoria só repete o que o provedor declara (categoria do status no Jira; aberto ou fechado no GitHub) e não infere andamento.
public static class StatusCategories
{
    public const string Todo = "todo";
    public const string InProgress = "inProgress";
    public const string Done = "done";
}
