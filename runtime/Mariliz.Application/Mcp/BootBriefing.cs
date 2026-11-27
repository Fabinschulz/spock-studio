using Mariliz.Application.Boot;

namespace Mariliz.Application.Mcp;

// O texto curto de spock_boot para o agente. Só repete o que o resultado do Boot já diz (feito, consultado, conferido, problemas,
// pendências, próximo passo); o que o agente não pode fazer sozinho vira a rota da tela, nunca uma ação.
internal static class BootBriefing
{
    private const int MaxChars = 7000;

    public static string Render(BootRecord record)
    {
        var result = record.Result;
        var blocks = new List<string>
        {
            $"# Spock Boot: {result?.Headline ?? "sem resultado"}",
            $"Modo {Mode(record.Mode)}, nível 0 (só lê dados deste projeto), estado {record.State}{(result is null ? "" : $", resultado {result.Outcome}")}. Registro {record.Id}."
        };
        if (result is null) return string.Join("\n\n", blocks);

        Add(blocks, "Feito", result.Done);
        Add(blocks, "Consultado", result.Consulted);
        blocks.Add("## Alterações\n" + (result.Changes.Count == 0 ? "Nenhuma: o Boot por MCP só lê." : string.Join("\n", result.Changes.Select(Line))));
        Add(blocks, "Conferido", result.Validations);
        Add(blocks, "Problemas", result.Problems);
        Add(blocks, "Pendente", result.Pending);
        if (result.Alternatives.Count > 0)
            blocks.Add("## Opções\n" + string.Join("\n", result.Alternatives.Select(item => $"- {item.CapabilityId}: {item.Name}. {item.Reason}"))
                       + "\nChame spock_boot de novo com capabilityId para escolher uma.");
        if (result.NextStep is not null || result.Route is not null) blocks.Add("## Próximo passo\n" + Next(result));
        if (record.Mode == BootMode.Project && result.Pending.Any(item => item.Contains("ainda não foi analisado", StringComparison.Ordinal)))
            blocks.Add("O perfil do projeto falta. O Boot por MCP não analisa nem grava: abra o projeto no Spock Studio e use Analisar o projeto.");
        return McpText.Fit(blocks, MaxChars, "O registro inteiro vem em structuredContent.");
    }

    private static string Next(BootResult result)
    {
        var lines = new List<string>();
        if (result.NextStep is not null) lines.Add(result.NextStep);
        if (result.Route is not null)
            lines.Add(result.Outcome == BootOutcomes.Handoff
                ? $"Quem abre é a pessoa, no Spock Studio: {result.RouteLabel ?? "Abrir a tela"} ({result.Route}). A tela confere e pede a aprovação; o Boot não executa o que altera algo."
                : $"Tela no Spock Studio: {result.RouteLabel ?? "Abrir a tela"} ({result.Route}).");
        return string.Join("\n", lines);
    }

    private static void Add(List<string> blocks, string title, IReadOnlyList<string> lines)
    {
        if (lines.Count > 0) blocks.Add($"## {title}\n" + string.Join("\n", lines.Select(Line)));
    }

    private static string Line(string text) => "- " + text;

    private static string Mode(BootMode mode) => mode switch
    {
        BootMode.Project => "Project Boot",
        BootMode.Recovery => "de recuperação",
        _ => "de pedido"
    };
}
