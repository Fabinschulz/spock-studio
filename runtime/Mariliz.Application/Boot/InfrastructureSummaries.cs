using System.Text.Json.Nodes;
using static Mariliz.Application.Boot.SummaryJson;

namespace Mariliz.Application.Boot;

/// <summary>Resumos da ferramenta e das pastas de infraestrutura como código. Caminhos e mensagens do projeto não entram.</summary>
internal static class InfrastructureSummaries
{
    private static readonly Dictionary<string, string> Engines = new(StringComparer.Ordinal) { ["openTofu"] = "OpenTofu", ["terraform"] = "Terraform" };

    private static readonly Dictionary<string, string> Sources = new(StringComparer.Ordinal) { ["managed"] = "gerenciada pelo Spock", ["external"] = "externa" };

    private static readonly Dictionary<string, string> States = new(StringComparer.Ordinal)
    {
        ["externalAvailable"] = "Há uma ferramenta de infraestrutura instalada fora do Spock, aguardando a sua decisão de uso.",
        ["missing"] = "Nenhuma ferramenta de infraestrutura instalada.",
        ["declined"] = "A instalação da ferramenta de infraestrutura foi recusada.",
        ["incompatible"] = "As ferramentas instaladas não atendem ao mínimo do Spock nem às exigências do projeto.",
        ["unsupportedPlatform"] = "Esta máquina não tem pacote oficial da ferramenta de infraestrutura."
    };

    public static CapabilitySummary Status(JsonObject result)
    {
        if (Object(result, "runtime") is not { } runtime) return NoData("da ferramenta de infraestrutura");

        var state = Text(runtime, "state");
        var selected = Object(runtime, "selected");
        var headline = state switch
        {
            "ready" => selected is null ? "Ferramenta de infraestrutura pronta para uso." : $"{Tool(selected)} pronto para uso.",
            not null when States.TryGetValue(state, out var text) => text,
            _ => $"Estado da ferramenta de infraestrutura: {NoValue}."
        };

        var evidence = new List<string>();
        var installations = Array(runtime, "installations");
        if (installations.Count > 0)
            evidence.Add($"{Plural(installations.Count, "instalação encontrada", "instalações encontradas")}: "
                + string.Join(", ", installations.Take(4).Select(item => $"{Tool(item)} ({Label(Text(item, "source"), Sources)})")) + ".");
        if (Object(runtime, "project") is { } project)
        {
            var requirements = Size(project, "requirements");
            if (requirements > 0) evidence.Add($"{Plural(requirements, "arquivo do projeto exige", "arquivos do projeto exigem")} uma versão mínima da ferramenta.");
            if (project["pin"] is not null) evidence.Add("A versão da ferramenta está fixada no projeto.");
            if (project["problem"] is not null) evidence.Add("O projeto tem um problema de versão da ferramenta a resolver; a tela explica.");
        }
        return new CapabilitySummary(headline, evidence);
    }

    public static CapabilitySummary Layout(JsonObject result)
    {
        if (Object(result, "layout") is not { } layout || !HasList(layout, "folders")) return NoData("das pastas de infraestrutura");
        var folders = Size(layout, "folders");
        return new CapabilitySummary(folders == 0 ? "Nenhuma pasta de infraestrutura encontrada no projeto." : $"{Plural(folders, "pasta de infraestrutura", "pastas de infraestrutura")} no projeto.", []);
    }

    private static string Tool(JsonObject installation) =>
        $"{Label(Text(installation, "engine"), Engines)} {Text(installation, "version") ?? NoValue}";
}
