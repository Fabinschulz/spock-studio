using System.Text.Json.Nodes;
using Mariliz.Application.Execution;

namespace Mariliz.Application.Ideas;

public static class IdeaStages
{
    public const string Specification = "specification";
    public const string Architecture = "architecture";
    public const string Backlog = "backlog";

    public static readonly IReadOnlyList<string> All = [Specification, Architecture, Backlog];
}

public sealed record IdeaCheck(string Id, string Label, bool Passed, string? Detail);

/// <param name="Attempts">Quantas vezes o modelo foi chamado: a segunda recebe o que a primeira errou.</param>
public sealed record IdeaDraft(
    string Stage,
    JsonObject? Artifact,
    IReadOnlyList<IdeaCheck> Checks,
    bool Valid,
    int Attempts,
    int InputTokens,
    int OutputTokens);

public sealed record IdeaPlan(string PlanId, string ProjectId, IReadOnlyList<ReviewedChange> Changes);

/// <param name="Specification">Só o que passou nas verificações da etapa entra na seguinte.</param>
public sealed record IdeaArtifacts(string Idea, JsonObject? Specification, JsonObject? Architecture, JsonObject? Backlog);
