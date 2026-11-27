using System.Text.Json.Nodes;
using static Mariliz.Application.Boot.SummaryJson;

namespace Mariliz.Application.Boot;

/// <summary>
/// Resume o resultado de uma capacidade em linhas que o motor lê do próprio JSON: contagens, estados e nomes de tecnologias.
/// Nada é escrito por modelo e nenhum texto livre do projeto ou das integrações é guardado no registro do Boot; quem quer o
/// detalhe abre a tela da capacidade, que mostra o dado inteiro.
/// </summary>
public static class CapabilitySummaries
{
    public static CapabilitySummary Describe(string capabilityId, JsonObject result) => capabilityId switch
    {
        "engineering.project_overview" or "engineering.analyze_project" => Profile(result),
        "engineering.harness_status" => Harness(result),
        "engineering.catalog" => EngineeringSummaries.Catalog(result),
        "engineering.knowledge" => EngineeringSummaries.Knowledge(result),
        "engineering.mcp_inventory" => EngineeringSummaries.Mcp(result),
        "engineering.harness_revisions" => EngineeringSummaries.Revisions(result),
        "standards.architecture" => StandardsSummaries.Architecture(result),
        "standards.agent_policy" => StandardsSummaries.AgentPolicy(result),
        "management.portfolio" => Portfolio(result),
        "management.risks" => Risks(result),
        "management.tracking" => ManagementSummaries.Tracking(result),
        "management.forecast" => ManagementSummaries.Forecast(result),
        "management.milestones" => ManagementSummaries.Milestones(result),
        "management.report" => ManagementSummaries.Report(result),
        "management.decisions" => ManagementSummaries.Decisions(result),
        "management.risk_history" => ManagementSummaries.RiskHistory(result),
        "platform.timeline" => PlatformSummaries.Timeline(result),
        "platform.audit" => PlatformSummaries.Audit(result),
        "platform.runs" => Runs(result),
        "infrastructure.status" => InfrastructureSummaries.Status(result),
        "infrastructure.layout" => InfrastructureSummaries.Layout(result),
        "ai.model_status" => Models(result),
        "ai.agents_detect" => ConnectionSummaries.Agents(result),
        "connections.list" => Connections(result),
        "connections.cloud" => ConnectionSummaries.Cloud(result),
        _ => Generic(result)
    };

    private static CapabilitySummary Profile(JsonObject result)
    {
        if (result["profile"] is not JsonObject profile) return new CapabilitySummary("O projeto ainda não foi analisado.", ["Sem perfil tecnológico guardado."]);
        var technologies = Array(profile, "technologies");
        var names = technologies.Select(item => Text(item, "name")).Where(name => name is not null).Cast<string>().Distinct().Take(8).ToList();
        var evidence = new List<string> { $"{Plural(technologies.Count, "tecnologia encontrada", "tecnologias encontradas")}." };
        if (names.Count > 0) evidence.Add("Entre elas: " + string.Join(", ", names) + ".");
        var tooling = Array(profile, "tooling").Count;
        if (tooling > 0) evidence.Add($"{Plural(tooling, "ferramenta de build e teste", "ferramentas de build e teste")}.");
        return new CapabilitySummary(technologies.Count == 0 ? "Nenhuma tecnologia reconhecida no projeto." : $"{Plural(technologies.Count, "tecnologia", "tecnologias")} no projeto.", evidence);
    }

    private static CapabilitySummary Harness(JsonObject result)
    {
        var applied = result["applied"] is JsonValue flag && flag.TryGetValue<bool>(out var value) && value;
        if (!applied) return new CapabilitySummary("O Harness ainda não foi aplicado.", ["O projeto não tem as regras gerenciadas pelo Spock."]);
        var components = Array(result, "components");
        var byState = components.GroupBy(item => Text(item, "state") ?? "?").OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => $"{group.Count()} {StateLabel(group.Key)}").ToList();
        var environment = Text(result, "aiEnvironment");
        return new CapabilitySummary($"Harness aplicado{(environment is null ? "" : $" para {environment}")}: {Plural(components.Count, "componente", "componentes")}.",
            [byState.Count == 0 ? "Sem componentes listados." : string.Join(", ", byState) + "."]);
    }

    private static CapabilitySummary Portfolio(JsonObject result)
    {
        var projects = Array(result, "projects");
        var attention = projects.Count(project => Array(project, "signals").Count > 0);
        return new CapabilitySummary($"{Plural(projects.Count, "projeto", "projetos")} no portfólio, {attention} com ponto de atenção.",
            [$"{Plural(projects.Count(project => (Number(project, "sources") ?? 0) == 0), "projeto sem fonte de dados", "projetos sem fonte de dados")}."]);
    }

    private static CapabilitySummary Risks(JsonObject result)
    {
        var hasData = result["hasData"] is not JsonValue flag || !flag.TryGetValue<bool>(out var value) || value;
        if (!hasData) return new CapabilitySummary("Sem dados para avaliar riscos.", ["As integrações do projeto ainda não trouxeram itens."]);
        var risks = Array(result, "risks");
        var bySeverity = risks.GroupBy(item => Text(item, "severity") ?? "?").OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => $"{group.Count()} de gravidade {group.Key}").ToList();
        var evidence = new List<string>();
        if (bySeverity.Count > 0) evidence.Add(string.Join(", ", bySeverity) + ".");
        evidence.AddRange(risks.Take(5).Select(item => $"{Text(item, "title") ?? "Risco"} ({Text(item, "severity") ?? "?"})."));
        var partial = result["complete"] is JsonValue c && c.TryGetValue<bool>(out var done) && !done;
        if (partial) evidence.Add("A última sincronização foi parcial: o resultado pode estar incompleto.");
        return new CapabilitySummary(risks.Count == 0 ? "Nenhum risco encontrado pelas regras do motor." : $"{Plural(risks.Count, "risco encontrado", "riscos encontrados")}.", evidence);
    }

    private static CapabilitySummary Runs(JsonObject result)
    {
        var summary = result["summary"] as JsonObject;
        if (summary is null && !HasList(result, "runs")) return NoData("das execuções");
        // Campo ausente é "sem dado", nunca zero: zero diria que o motor olhou e não achou.
        var total = Number(summary, "total") ?? (HasList(result, "runs") ? Array(result, "runs").Count : null);
        var head = total is { } known ? Plural(known, "execução", "execuções") : "Total sem dado";
        return new CapabilitySummary($"{head}: {Count(Number(summary, "running"))} em andamento e {Count(Number(summary, "awaitingApproval"))} aguardando aprovação.", []);
    }

    private static CapabilitySummary Models(JsonObject result)
    {
        var providers = Array(result, "providers");
        var configured = providers.Where(provider => provider["configured"] is JsonValue flag && flag.TryGetValue<bool>(out var value) && value).ToList();
        if (configured.Count == 0) return new CapabilitySummary("Nenhum provedor de IA configurado.", ["Sem provedor, o Spock trabalha só com o que calcula; as respostas por IA ficam indisponíveis."]);
        var names = configured.Select(provider => Text(provider, "name")).Where(name => name is not null).Cast<string>().ToList();
        return new CapabilitySummary($"{Plural(configured.Count, "provedor configurado", "provedores configurados")}.", [string.Join(", ", names) + "."]);
    }

    private static CapabilitySummary Connections(JsonObject result)
    {
        var connections = Array(result, "connections");
        var byStatus = connections.GroupBy(item => Text(item, "status") ?? "?").OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => $"{group.Count()} {group.Key}").ToList();
        return new CapabilitySummary(connections.Count == 0 ? "Nenhuma conexão cadastrada." : $"{Plural(connections.Count, "conexão", "conexões")}.",
            byStatus.Count == 0 ? [] : [string.Join(", ", byStatus) + "."]);
    }

    // Só contagens das listas de primeiro nível: o que sobra do JSON não vira texto guardado.
    private static CapabilitySummary Generic(JsonObject result)
    {
        var counts = result.Where(pair => pair.Value is JsonArray).Select(pair => $"{pair.Key}: {((JsonArray)pair.Value!).Count}").ToList();
        foreach (var pair in result.Where(pair => pair.Value is JsonObject nested && nested.Any(inner => inner.Value is JsonArray)))
            counts.AddRange(((JsonObject)pair.Value!).Where(inner => inner.Value is JsonArray).Select(inner => $"{pair.Key}.{inner.Key}: {((JsonArray)inner.Value!).Count}"));
        return new CapabilitySummary("Leitura concluída.", counts.Count == 0 ? [] : [string.Join("; ", counts.Take(8)) + "."]);
    }

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
