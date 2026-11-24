using System.Text.Json.Nodes;
using Mariliz.Application.Ports;
using Mariliz.Core.Policy;

namespace Mariliz.Application.Iac;

/// <param name="Path">Relativo ao projeto.</param>
/// <param name="Blueprint">Nulo quando o arquivo não é um objeto JSON com o desenho em "blueprint".</param>
public sealed record IacArchitectureProposal(string Path, IacBlueprint? Blueprint, IacArchitectureNotes? Notes, IReadOnlyList<IacBlueprintCheck> Checks, bool Valid);

// O desenho que o agente da sessão gravou no projeto: o motor não chamou modelo, só confere com as mesmas checagens e
// mostra na tela, onde a pessoa completa os fatos da conta e decide gerar. O formato é o da resposta do modelo.
public static class IacArchitectureProposals
{
    public const int MaxCharacters = 64_000;

    public static string PathIn(IProjectFiles files) => HarnessNaming.Detect(files.FileExists, files.DirectoryExists).IacProposal;

    public static IacArchitectureProposal? Read(IProjectFiles files)
    {
        var path = PathIn(files);
        var text = files.ReadText(path);
        if (text is null) return null;
        if (text.Length > MaxCharacters) return Rejected(path, $"O arquivo passa de {MaxCharacters} caracteres.");
        var json = IacBlueprintJson.Parse(text);
        if (json?["blueprint"] is not JsonObject proposed) return Rejected(path, null);

        var blueprint = IacBlueprintJson.Blueprint(proposed);
        var checks = IacBlueprintChecks.Check(blueprint);
        return new IacArchitectureProposal(path, blueprint, IacBlueprintJson.Notes(json["notes"] as JsonObject), checks, checks.All(check => check.Passed));
    }

    private static IacArchitectureProposal Rejected(string path, string? detail) =>
        new(path, null, null, [new IacBlueprintCheck("json", "O arquivo é um objeto JSON com o desenho em \"blueprint\"", false, detail)], false);
}
