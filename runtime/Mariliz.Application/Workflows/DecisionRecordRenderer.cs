using System.Text;
using System.Text.Json.Nodes;
using Mariliz.Application.Ports;

namespace Mariliz.Application.Workflows;

/// <summary>O texto do registro de decisão que o motor grava no projeto.</summary>
internal static class DecisionRecordRenderer
{
    /// <summary>
    /// O texto do registro, escrito pelo motor só com o que ele validou (áreas, riscos e trechos citados). Sem o pedido, sem texto
    /// livre de modelo e sem data, então o mesmo impacto gera sempre o mesmo conteúdo.
    /// </summary>
    public static string Render(WorkflowAck ack, JsonObject impact, IReadOnlyList<string> evidence, Dictionary<string, string> known)
    {
        var areas = impact["areas"]?.AsArray().Select(a => a?.GetValue<string>() ?? "").Select(AreaLabel) ?? [];
        var risks = impact["risks"]?.AsArray().Select(r => r?.GetValue<string>() ?? "").Select(RiskLabel).ToList() ?? [];
        var text = new StringBuilder();
        text.Append("# Decisão de arquitetura: impacto técnico\n\n");
        text.Append($"Registro gerado a partir da execução `{ack.ExecutionId}`. Foi proposto por um agente e aprovado por uma pessoa; revise antes de versionar.\n\n");
        text.Append("## Áreas afetadas\n\n");
        foreach (var area in areas) text.Append("- ").Append(area).Append('\n');
        text.Append("\n## Riscos\n\n");
        if (risks.Count == 0) text.Append("- Nenhum risco registrado.\n");
        foreach (var risk in risks) text.Append("- ").Append(risk).Append('\n');
        text.Append("\n## Evidências\n\n");
        if (evidence.Count == 0) text.Append("- Nenhuma evidência do projeto sustenta este registro.\n");
        foreach (var chunk in evidence) text.Append($"- `{known[chunk]}` (`{chunk}`)\n");
        return text.ToString();
    }

    private static string AreaLabel(string area) => area switch { "servico" => "Serviço", "interface" => "Interface", _ => "Projeto" };

    private static string RiskLabel(string risk) => risk switch
    {
        "sem-evidencia" => "Nenhum trecho do projeto sustenta o impacto.",
        "sem-stack" => "Nenhuma stack foi detectada no projeto.",
        _ => "Risco não identificado."
    };
}
