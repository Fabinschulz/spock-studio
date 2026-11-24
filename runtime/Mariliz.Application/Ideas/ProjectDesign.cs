using System.Text.Json;
using System.Text.Json.Nodes;
using Mariliz.Application.Ports;
using Mariliz.Core.Policy;

namespace Mariliz.Application.Ideas;

public sealed record ProjectDesignStage(string Stage, JsonObject? Artifact, IReadOnlyList<IdeaCheck> Checks, bool Valid);

/// <param name="Checks">O que vale para o arquivo inteiro (JSON, tamanho, ideia); cada etapa traz as próprias verificações.</param>
public sealed record ProjectDesign(string Path, bool Found, string? Idea, IReadOnlyList<IdeaCheck> Checks, IReadOnlyList<ProjectDesignStage> Stages, bool Valid);

// W4-3 no modo Sessão: o agente da pessoa escreve a ideia, a especificação, a arquitetura e o backlog num arquivo só. O motor
// não chama modelo: confere cada etapa com as mesmas verificações do modo Chave (IdeaChecks), e a pessoa aprova e grava os
// documentos na tela Da ideia ao projeto, pelo mesmo caminho revisado.
public static class ProjectDesigns
{
    public const int MaxCharacters = 128_000;

    public static string PathIn(IProjectFiles files) => HarnessNaming.Detect(files.FileExists, files.DirectoryExists).ProjectDesign;

    public static ProjectDesign Read(IProjectFiles files)
    {
        var path = PathIn(files);
        var text = files.ReadText(path);
        if (text is null) return new(path, false, null, [], [], false);
        if (text.Length > MaxCharacters)
            return Rejected(path, new IdeaCheck("limits", $"O arquivo tem até {MaxCharacters} caracteres", false, null));

        var json = Parse(text);
        if (json is null) return Rejected(path, new IdeaCheck("json", "O arquivo é um objeto JSON", false, null));

        var idea = IdeaChecks.Text(json, "idea");
        IReadOnlyList<IdeaCheck> checks =
        [
            new("idea", $"Traz a ideia, com {IdeaArchitectService.MinIdea} a {IdeaArchitectService.MaxIdea} caracteres",
                idea.Length is >= IdeaArchitectService.MinIdea and <= IdeaArchitectService.MaxIdea, null)
        ];
        var specification = json[IdeaStages.Specification] as JsonObject;
        var stages = IdeaStages.All.Select(stage => Stage(stage, json[stage] as JsonObject, specification)).ToList();
        return new(path, true, idea.Length > 0 ? idea : null, checks, stages, checks.All(check => check.Passed) && stages.All(stage => stage.Valid));
    }

    public static IdeaArtifacts? Artifacts(ProjectDesign design) =>
        design.Idea is null
            ? null
            : new IdeaArtifacts(design.Idea, Artifact(design, IdeaStages.Specification), Artifact(design, IdeaStages.Architecture), Artifact(design, IdeaStages.Backlog));

    private static JsonObject? Artifact(ProjectDesign design, string stage) =>
        design.Stages.FirstOrDefault(item => item.Stage == stage)?.Artifact?.DeepClone().AsObject();

    // A arquitetura e o backlog são conferidos contra a especificação do mesmo arquivo: sem ela, os ids citados não existem.
    private static ProjectDesignStage Stage(string stage, JsonObject? artifact, JsonObject? specification)
    {
        if (artifact is null)
            return new(stage, null, [new IdeaCheck("present", $"O arquivo traz a etapa \"{stage}\" como objeto", false, null)], false);
        var spec = specification ?? new JsonObject();
        IReadOnlyList<IdeaCheck> checks = stage switch
        {
            IdeaStages.Specification => IdeaChecks.Specification(artifact),
            IdeaStages.Architecture => IdeaChecks.Architecture(artifact, spec),
            _ => IdeaChecks.Backlog(artifact, spec)
        };
        return new(stage, artifact, checks, checks.All(check => check.Passed));
    }

    private static ProjectDesign Rejected(string path, IdeaCheck check) => new(path, true, null, [check], [], false);

    private static JsonObject? Parse(string text)
    {
        try
        {
            return JsonNode.Parse(text) as JsonObject;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
