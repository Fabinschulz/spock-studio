using System.Text.Json.Nodes;
using static Mariliz.Application.Boot.SummaryJson;

namespace Mariliz.Application.Boot;

/// <summary>Resumos das contas de nuvem e dos agentes de IA instalados. Nome da conta, identidade e mensagem de erro ficam na tela.</summary>
internal static class ConnectionSummaries
{
    private static readonly (string Id, string Label)[] Providers = [("aws", "AWS"), ("azure", "Azure"), ("gcp", "Google Cloud")];

    private static readonly (string Id, string One, string Many)[] Statuses =
    [
        ("connected", "conectada", "conectadas"), ("notAuthenticated", "sem autenticação", "sem autenticação"), ("expired", "com credencial vencida", "com credencial vencida"),
        ("invalidIdentity", "com identidade inválida", "com identidade inválida"), ("permissionDenied", "sem permissão", "sem permissão"),
        ("invalidRegion", "com região inválida", "com região inválida"), ("unavailable", "indisponível", "indisponíveis")
    ];

    public static CapabilitySummary Cloud(JsonObject result)
    {
        if (!HasList(result, "connections")) return NoData("das contas de nuvem");
        var connections = Array(result, "connections");
        if (connections.Count == 0) return new CapabilitySummary("Nenhuma conta de nuvem conectada.", []);

        var byProvider = Providers.Select(item => (item.Label, Count: connections.Count(connection => Text(connection, "provider") == item.Id))).Where(item => item.Count > 0)
            .Select(item => $"{item.Label} {item.Count}").ToList();
        var others = connections.Count(connection => Providers.All(item => item.Id != Text(connection, "provider")));
        if (others > 0) byProvider.Add($"outros {others}");

        var byStatus = Statuses.Select(item => (Count: connections.Count(connection => Text(connection, "status") == item.Id), item.One, item.Many)).Where(item => item.Count > 0)
            .Select(item => Plural(item.Count, item.One, item.Many)).ToList();
        var evidence = new List<string>();
        if (byStatus.Count > 0) evidence.Add($"Estado: {string.Join(", ", byStatus)}.");
        var inUse = connections.Count(connection => (Number(connection, "projects") ?? 0) > 0);
        evidence.Add($"{Plural(inUse, "conta usada", "contas usadas")} por algum projeto.");
        return new CapabilitySummary($"{Plural(connections.Count, "conta de nuvem", "contas de nuvem")}: {string.Join(", ", byProvider)}.", evidence);
    }

    public static CapabilitySummary Agents(JsonObject result)
    {
        if (!HasList(result, "tools")) return NoData("dos agentes de IA instalados");
        var tools = Array(result, "tools");
        var installed = tools.Where(tool => Flag(tool, "installed") == true).ToList();
        if (installed.Count == 0)
            return new CapabilitySummary("Nenhum agente de IA instalado nesta máquina.", [$"{Plural(tools.Count, "agente conhecido", "agentes conhecidos")} pelo Spock, nenhum encontrado."]);

        var names = installed.Select(tool => Text(tool, "name") is { } name ? $"{name} {Text(tool, "version") ?? $"(versão {NoValue})"}" : $"agente sem nome ({NoValue})");
        var evidence = new List<string> { $"Instalados: {string.Join(", ", names)}." };
        var supported = installed.Count(tool => Flag(tool, "harnessSupport") == true);
        evidence.Add($"{supported} de {installed.Count} aceitam o Harness do Spock.");
        return new CapabilitySummary($"{Plural(installed.Count, "agente de IA instalado", "agentes de IA instalados")}, entre {tools.Count} conhecidos pelo Spock.", evidence);
    }
}
