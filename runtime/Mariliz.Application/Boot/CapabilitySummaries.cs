using System.Text.Json.Nodes;

namespace Mariliz.Application.Boot;

/// <summary>
/// Resume o resultado de uma capacidade em linhas que o motor lê do próprio JSON: contagens, estados e nomes de tecnologias.
/// Nada é escrito por modelo e nenhum texto livre do projeto ou das integrações é guardado no registro do Boot; quem quer o
/// detalhe abre a tela da capacidade, que mostra o dado inteiro.
/// </summary>
public static class CapabilitySummaries
{
    public sealed record Summary(string Headline, IReadOnlyList<string> Evidence);

    public static Summary Describe(string capabilityId, JsonObject result) => capabilityId switch
    {
        "engineering.project_overview" or "engineering.analyze_project" => Profile(result),
        "engineering.harness_status" => Harness(result),
        "management.portfolio" => Portfolio(result),
        "management.risks" => Risks(result),
        "platform.runs" => Runs(result),
        "ai.model_status" => Models(result),
        "connections.list" or "connections.cloud" => Connections(result),
        _ => Generic(result)
    };

    private static Summary Profile(JsonObject result)
    {
        if (result["profile"] is not JsonObject profile) return new Summary("O projeto ainda não foi analisado.", ["Sem perfil tecnológico guardado."]);
        var technologies = Array(profile, "technologies");
        var names = technologies.Select(item => Text(item, "name")).Where(name => name is not null).Cast<string>().Distinct().Take(8).ToList();
        var evidence = new List<string> { $"{Plural(technologies.Count, "tecnologia encontrada", "tecnologias encontradas")}." };
        if (names.Count > 0) evidence.Add("Entre elas: " + string.Join(", ", names) + ".");
        var tooling = Array(profile, "tooling").Count;
        if (tooling > 0) evidence.Add($"{Plural(tooling, "ferramenta de build e teste", "ferramentas de build e teste")}.");
        return new Summary(technologies.Count == 0 ? "Nenhuma tecnologia reconhecida no projeto." : $"{Plural(technologies.Count, "tecnologia", "tecnologias")} no projeto.", evidence);
    }

    private static Summary Harness(JsonObject result)
    {
        var applied = result["applied"] is JsonValue flag && flag.TryGetValue<bool>(out var value) && value;
        if (!applied) return new Summary("O Harness ainda não foi aplicado.", ["O projeto não tem as regras gerenciadas pelo Spock."]);
        var components = Array(result, "components");
        var byState = components.GroupBy(item => Text(item, "state") ?? "?").OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => $"{group.Count()} {StateLabel(group.Key)}").ToList();
        var environment = Text(result, "aiEnvironment");
        return new Summary($"Harness aplicado{(environment is null ? "" : $" para {environment}")}: {Plural(components.Count, "componente", "componentes")}.",
            [byState.Count == 0 ? "Sem componentes listados." : string.Join(", ", byState) + "."]);
    }

    private static Summary Portfolio(JsonObject result)
    {
        var projects = Array(result, "projects");
        var attention = projects.Count(project => Array(project, "signals").Count > 0);
        return new Summary($"{Plural(projects.Count, "projeto", "projetos")} no portfólio, {attention} com ponto de atenção.",
            [$"{Plural(projects.Count(project => (Number(project, "sources") ?? 0) == 0), "projeto sem fonte de dados", "projetos sem fonte de dados")}."]);
    }

    private static Summary Risks(JsonObject result)
    {
        var hasData = result["hasData"] is not JsonValue flag || !flag.TryGetValue<bool>(out var value) || value;
        if (!hasData) return new Summary("Sem dados para avaliar riscos.", ["As integrações do projeto ainda não trouxeram itens."]);
        var risks = Array(result, "risks");
        var bySeverity = risks.GroupBy(item => Text(item, "severity") ?? "?").OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => $"{group.Count()} de gravidade {group.Key}").ToList();
        var evidence = new List<string>();
        if (bySeverity.Count > 0) evidence.Add(string.Join(", ", bySeverity) + ".");
        evidence.AddRange(risks.Take(5).Select(item => $"{Text(item, "title") ?? "Risco"} ({Text(item, "severity") ?? "?"})."));
        var partial = result["complete"] is JsonValue c && c.TryGetValue<bool>(out var done) && !done;
        if (partial) evidence.Add("A última sincronização foi parcial: o resultado pode estar incompleto.");
        return new Summary(risks.Count == 0 ? "Nenhum risco encontrado pelas regras do motor." : $"{Plural(risks.Count, "risco encontrado", "riscos encontrados")}.", evidence);
    }

    private static Summary Runs(JsonObject result)
    {
        var summary = result["summary"] as JsonObject;
        var total = Number(summary, "total") ?? Array(result, "runs").Count;
        var running = Number(summary, "running") ?? 0;
        var awaiting = Number(summary, "awaitingApproval") ?? 0;
        return new Summary($"{Plural(total, "execução", "execuções")}: {running} em andamento e {awaiting} aguardando aprovação.", []);
    }

    private static Summary Models(JsonObject result)
    {
        var providers = Array(result, "providers");
        var configured = providers.Where(provider => provider["configured"] is JsonValue flag && flag.TryGetValue<bool>(out var value) && value).ToList();
        if (configured.Count == 0) return new Summary("Nenhum provedor de IA configurado.", ["Sem provedor, o Spock trabalha só com o que calcula; as respostas por IA ficam indisponíveis."]);
        var names = configured.Select(provider => Text(provider, "name")).Where(name => name is not null).Cast<string>().ToList();
        return new Summary($"{Plural(configured.Count, "provedor configurado", "provedores configurados")}.", [string.Join(", ", names) + "."]);
    }

    private static Summary Connections(JsonObject result)
    {
        var connections = Array(result, "connections");
        var byStatus = connections.GroupBy(item => Text(item, "status") ?? "?").OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => $"{group.Count()} {group.Key}").ToList();
        return new Summary(connections.Count == 0 ? "Nenhuma conexão cadastrada." : $"{Plural(connections.Count, "conexão", "conexões")}.",
            byStatus.Count == 0 ? [] : [string.Join(", ", byStatus) + "."]);
    }

    // Só contagens das listas de primeiro nível: o que sobra do JSON não vira texto guardado.
    private static Summary Generic(JsonObject result)
    {
        var counts = result.Where(pair => pair.Value is JsonArray).Select(pair => $"{pair.Key}: {((JsonArray)pair.Value!).Count}").ToList();
        foreach (var pair in result.Where(pair => pair.Value is JsonObject nested && nested.Any(inner => inner.Value is JsonArray)))
            counts.AddRange(((JsonObject)pair.Value!).Where(inner => inner.Value is JsonArray).Select(inner => $"{pair.Key}.{inner.Key}: {((JsonArray)inner.Value!).Count}"));
        return new Summary("Leitura concluída.", counts.Count == 0 ? [] : [string.Join("; ", counts.Take(8)) + "."]);
    }

    private static List<JsonObject> Array(JsonObject? node, string name) =>
        node?[name] is JsonArray array ? [.. array.OfType<JsonObject>()] : [];

    private static string? Text(JsonObject node, string name) =>
        node[name] is JsonValue value && value.TryGetValue<string>(out var text) && !string.IsNullOrWhiteSpace(text) ? text : null;

    private static int? Number(JsonObject? node, string name) =>
        node?[name] is JsonValue value && value.TryGetValue<int>(out var number) ? number : null;

    private static string Plural(int count, string one, string many) => $"{count} {(count == 1 ? one : many)}";

    private static string StateLabel(string state) => state switch
    {
        "intact" => "íntegros",
        "kept" => "mantidos pela equipe",
        "modified" => "modificados à mão",
        "missing" => "faltando",
        "outOfScope" => "fora do que o Spock gerencia",
        _ => state
    };
}
