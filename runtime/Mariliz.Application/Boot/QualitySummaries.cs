using System.Text.Json.Nodes;
using Mariliz.Application.Quality;
using static Mariliz.Application.Boot.SummaryJson;

namespace Mariliz.Application.Boot;

/// <summary>Resumo da revisão de alterações: o veredito do motor e as contagens, sem caminhos nem trechos do projeto.</summary>
internal static class QualitySummaries
{
    public static CapabilitySummary Review(JsonObject result)
    {
        if (Object(result, "review") is not { } review) return NoData("da revisão");
        var counts = Object(review, "counts");
        var verdict = Text(review, "verdict");
        var files = Number(counts, "files");
        var analyzed = Number(counts, "filesAnalyzed");

        var headline = verdict switch
        {
            ReviewVerdicts.Approved => "Revisão concluída: nenhum problema encontrado pelas verificações determinísticas.",
            ReviewVerdicts.ApprovedWithWarnings => "Revisão concluída com ressalvas: há pontos para conferir.",
            ReviewVerdicts.Rejected => "Revisão concluída: há problema confirmado e grave; a entrega não deve seguir sem corrigir.",
            ReviewVerdicts.Inconclusive => "Revisão inconclusiva: parte da alteração não pôde ser lida.",
            ReviewVerdicts.NotRun => "Não há alterações para revisar.",
            _ => "Revisão concluída."
        };

        var evidence = new List<string>
        {
            $"{Count(analyzed)} de {Plural(files ?? 0, "arquivo alterado", "arquivos alterados")} analisados linha a linha.",
            $"{Plural(Number(counts, "confirmed") ?? 0, "problema confirmado", "problemas confirmados")}, {Plural(Number(counts, "needsCheck") ?? 0, "ponto a conferir", "pontos a conferir")} e {Plural(Number(counts, "recommendations") ?? 0, "recomendação", "recomendações")}."
        };
        // Os juízes que não rodaram ficam ditos: aprovação sem eles não é a revisão completa.
        var notRun = Array(review, "judges").Where(judge => Text(judge, "verdict") == ReviewVerdicts.NotRun && Text(judge, "id") != JudgeIds.Final).Select(judge => Text(judge, "name")).OfType<string>().ToList();
        if (notRun.Count > 0) evidence.Add("Não rodaram: " + string.Join(", ", notRun) + ".");
        evidence.Add("Só leitura: nada no projeto foi alterado e nenhum comando do projeto foi executado.");
        return new CapabilitySummary(headline, evidence);
    }
}
