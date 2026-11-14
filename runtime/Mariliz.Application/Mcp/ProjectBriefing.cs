using Mariliz.Application.Engineering;
using Mariliz.Application.Management;
using Mariliz.Application.Workspace;
using Mariliz.Core.Discovery;

namespace Mariliz.Application.Mcp;

internal static class ProjectBriefing
{
    public const int Decisions = 5;

    public static IEnumerable<string> Sections(
        AuthorizedProject project, TechnologyProfile? profile, HarnessStatus? harness, IReadOnlyList<MemoryDocument> decisions)
    {
        yield return $"# {project.DisplayName}\nId no Spock: {project.ProjectId} · pasta {project.DisplayPath}";
        yield return Technologies(profile);
        if (profile is { Tooling.Count: > 0 }) yield return Tooling(profile);
        yield return Harness(harness);
        yield return RecentDecisions(decisions);
    }

    private static string Technologies(TechnologyProfile? profile)
    {
        if (profile is null) return "## Tecnologias\nAinda não analisadas no Spock.";
        var lines = new List<string> { "## Tecnologias" };
        lines.AddRange(profile.Technologies.Select(technology =>
        {
            var versions = technology.Versions.Count > 0 ? " " + string.Join(", ", technology.Versions) : "";
            var roots = technology.Roots.Count > 0 ? $" em {string.Join(", ", technology.Roots)}" : "";
            return $"- {technology.Name}{versions} ({technology.Category}){roots}, confiança {Confidence(technology.Confidence)}";
        }));
        if (profile.AiEnvironments.Count > 0) lines.Add($"Ambientes de IA no projeto: {string.Join(", ", profile.AiEnvironments)}.");
        lines.Add($"Analisado em {profile.AnalyzedAt:yyyy-MM-dd}.");
        return string.Join("\n", lines);
    }

    private static string Tooling(TechnologyProfile profile) =>
        string.Join("\n", profile.Tooling
            .Select(tool => tool.Command is null ? $"- {tool.Kind}: {tool.Name}" : $"- {tool.Kind}: {tool.Name}, comando `{tool.Command}`")
            .Prepend("## Comandos do projeto"));

    private static string Harness(HarnessStatus? harness)
    {
        if (harness is null) return "## Harness\nNão foi possível ler o estado agora.";
        if (!harness.Applied) return "## Harness\nAinda não aplicado: o projeto não tem as regras gerenciadas pelo Spock.";

        var applied = harness.AiEnvironment is null ? "Aplicado" : $"Aplicado para {harness.AiEnvironment}";
        var specification = harness.SpecificationVersion is null ? "" : $", especificação {harness.SpecificationVersion}";
        var lines = new List<string>
        {
            "## Harness",
            $"{applied}{specification}. {harness.Intact} íntegros, {harness.Kept} mantidos pela equipe, " +
            $"{harness.Modified} modificados à mão, {harness.Missing} faltando."
        };
        lines.AddRange(harness.Components.Where(component => component.State != "intact")
            .Select(component => $"- {component.Label}: {State(component.State)}"));
        return string.Join("\n", lines);
    }

    private static string RecentDecisions(IReadOnlyList<MemoryDocument> decisions) =>
        decisions.Count == 0
            ? "## Decisões\nNenhuma decisão registrada."
            : string.Join("\n\n", decisions.Take(Decisions).Select(item => item.Text)
                .Prepend($"## Decisões ({Math.Min(Decisions, decisions.Count)} mais recentes de {decisions.Count}; o resto em decisions)"));

    private static string Confidence(Confidence confidence) => confidence switch
    {
        Core.Discovery.Confidence.High => "alta",
        Core.Discovery.Confidence.Medium => "média",
        _ => "baixa"
    };

    private static string State(string state) => state switch
    {
        "kept" => "mantido pela equipe",
        "modified" => "modificado à mão",
        "missing" => "faltando",
        "outOfScope" => "fora do que o Spock gerencia",
        _ => state
    };
}
