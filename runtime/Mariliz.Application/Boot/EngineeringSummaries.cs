using System.Text.Json.Nodes;
using static Mariliz.Application.Boot.SummaryJson;

namespace Mariliz.Application.Boot;

/// <summary>Resumos das leituras de engenharia: catálogo, conhecimento, MCP e revisões do Harness.</summary>
internal static class EngineeringSummaries
{
    private static readonly (string Kind, string One, string Many)[] ComponentKinds =
    [
        ("agent", "agente", "agentes"), ("skill", "skill", "skills"), ("rule", "regra", "regras"), ("hook", "hook", "hooks"),
        ("tool", "ferramenta", "ferramentas"), ("specification", "especificação", "especificações"), ("config", "configuração", "configurações")
    ];

    private static readonly Dictionary<string, string> IndexNotes = new(StringComparer.Ordinal)
    {
        ["failed"] = "O índice não pôde ser lido agora.",
        ["runtimeUnavailable"] = "O runtime de IA não está disponível, então o índice não foi lido.",
        ["runtimeStopped"] = "O runtime de IA está parado, então o índice não foi lido.",
        ["unknown"] = "O índice não foi lido."
    };

    public static CapabilitySummary Catalog(JsonObject result)
    {
        if (!HasList(result, "entries")) return NoData("do catálogo do Harness");
        var entries = Array(result, "entries");
        if (entries.Count == 0) return new CapabilitySummary("Nenhum componente no catálogo do Harness.", []);

        static string Presence(JsonObject entry) => Text(entry, "presence") ?? "";
        static string Kind(JsonObject entry) => Text(Object(entry, "sheet"), "kind") ?? "";

        var applied = entries.Count(entry => Presence(entry) == "applied");
        var evidence = new List<string>();
        foreach (var (kind, one, many) in ComponentKinds)
        {
            var ofKind = entries.Where(entry => Kind(entry) == kind).ToList();
            if (ofKind.Count > 0) evidence.Add($"{Plural(ofKind.Count, one, many)} ({Plural(ofKind.Count(entry => Presence(entry) == "applied"), "aplicado", "aplicados")}).");
        }
        var others = entries.Count(entry => ComponentKinds.All(known => known.Kind != Kind(entry)));
        if (others > 0) evidence.Add($"{Plural(others, "componente de outro tipo", "componentes de outros tipos")}.");
        var excluded = entries.Count(entry => Presence(entry) == "excluded");
        if (excluded > 0) evidence.Add($"{Plural(excluded, "componente excluído", "componentes excluídos")} pela declaração do projeto.");
        if (Flag(result, "applied") == false) evidence.Add("O Harness ainda não foi aplicado neste projeto.");
        return new CapabilitySummary($"{Plural(entries.Count, "componente", "componentes")} no catálogo do Harness, {Plural(applied, "aplicado", "aplicados")} neste projeto.", evidence);
    }

    public static CapabilitySummary Knowledge(JsonObject result)
    {
        if (Number(result, "allowed") is not { } allowed) return NoData("do índice de conhecimento");
        var state = Text(result, "indexState") ?? "unknown";
        var evidence = new List<string>();
        if (state == "read")
        {
            var parts = new[] { ("indexado", "indexados", "indexed"), ("desatualizado", "desatualizados", "outdated"), ("pendente", "pendentes", "pending"), ("órfão", "órfãos", "orphaned") }
                .Select(item => Plural(Number(result, item.Item3) ?? 0, item.Item1, item.Item2));
            evidence.Add(string.Join(", ", parts) + ".");
        }
        else evidence.Add(IndexNotes.GetValueOrDefault(state, IndexNotes["unknown"]));

        var excluded = Array(result, "excluded");
        var left = excluded.Sum(item => Number(item, "count") ?? 0);
        var secrets = excluded.Where(item => Text(item, "reason") == "secret").Sum(item => Number(item, "count") ?? 0);
        if (left > 0) evidence.Add($"{Plural(left, "arquivo fica", "arquivos ficam")} de fora da leitura da IA{(secrets > 0 ? $", {secrets} por possível segredo" : "")}.");
        var external = Array(result, "external").Count(source => Flag(source, "authorized") == true);
        if (external > 0) evidence.Add($"{Plural(external, "fonte externa autorizada", "fontes externas autorizadas")} no índice.");
        if (Number(Object(result, "memory"), "documents") is > 0 and var memory) evidence.Add($"{Plural(memory, "decisão", "decisões")} na memória do índice.");

        return new CapabilitySummary(allowed == 0 ? "Nenhum arquivo do projeto pode ser lido pela IA." : $"{Plural(allowed, "arquivo", "arquivos")} que a IA pode ler no projeto.", evidence);
    }

    public static CapabilitySummary Mcp(JsonObject result)
    {
        if (Flag(result, "present") is not { } present) return NoData("do inventário de MCP");
        if (!present) return new CapabilitySummary("O projeto não tem configuração de MCP.", []);
        if (Flag(result, "readable") == false) return new CapabilitySummary("Há uma configuração de MCP, mas o arquivo não pôde ser lido.", []);

        var servers = Array(result, "servers");
        if (servers.Count == 0) return new CapabilitySummary("Nenhum servidor MCP configurado.", []);
        var transports = servers.GroupBy(server => Text(server, "transport") ?? NoValue).OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => $"{group.Count()} {group.Key}");
        var literal = servers.Sum(server => Array(server, "literalSecrets").Count);
        return new CapabilitySummary(Plural(servers.Count, "servidor MCP configurado", "servidores MCP configurados") + ".",
        [
            $"Transporte: {string.Join(", ", transports)}.",
            literal == 0
                ? "Nenhuma credencial escrita direto no arquivo."
                : $"{Plural(literal, "credencial escrita direto", "credenciais escritas direto")} no arquivo, em vez de variável de ambiente."
        ]);
    }

    public static CapabilitySummary Revisions(JsonObject result)
    {
        if (!HasList(result, "revisions")) return NoData("das revisões do Harness");
        var revisions = Array(result, "revisions");
        if (revisions.Count == 0) return new CapabilitySummary("Nenhuma revisão do Harness registrada.", ["Sem aplicações nem reversões gravadas."]);

        var latest = revisions.OrderByDescending(revision => Number(revision, "number") ?? 0).First();
        var kind = Label(Text(latest, "kind"), new Dictionary<string, string> { ["apply"] = "aplicação", ["revert"] = "reversão" });
        var when = Day(latest, "at") is { } day ? $" em {day}" : "";
        var applies = revisions.Count(revision => Text(revision, "kind") == "apply");
        var reverts = revisions.Count(revision => Text(revision, "kind") == "revert");
        var evidence = new List<string> { $"{Plural(applies, "aplicação", "aplicações")} e {Plural(reverts, "reversão", "reversões")}." };
        var rolledBack = revisions.Count(revision => Flag(revision, "rolledBack") == true);
        if (rolledBack > 0) evidence.Add($"{Plural(rolledBack, "aplicação foi desfeita", "aplicações foram desfeitas")} na hora por falha no meio do caminho.");
        if (Text(result, "appliedByEngine") is { } by && Text(result, "currentEngine") is { } current && by != current) evidence.Add("O Harness foi aplicado por outra versão do motor.");
        return new CapabilitySummary($"{Plural(revisions.Count, "revisão do Harness", "revisões do Harness")}; a mais recente é a nº {Count(Number(latest, "number"))}, uma {kind}{when}.", evidence);
    }
}
