namespace Mariliz.Application.Quality;

/// <param name="ArchitectureNotRunReason">Por que a análise de arquitetura não rodou; nulo quando rodou.</param>
internal sealed record JudgeInput(
    IReadOnlyList<ReviewFinding> Findings,
    int FilesChanged,
    int FilesAnalyzed,
    bool Truncated,
    IReadOnlyList<string> Limitations,
    string? ArchitectureNotRunReason,
    // O que a pessoa pediu e não rodou: verificação do projeto sem resultado não aprova, e modelo ou requisitos sem resposta não deixam passar limpo.
    bool GatesRequestedNotRun = false,
    bool OptionalRequestedNotRun = false);

/// <summary>
/// Juízes determinísticos: cada um aplica critérios escritos e conferíveis aos achados da sua dimensão. Não há modelo aqui, e o
/// agente que fez a alteração não participa: o que ele escreveu é só o diff. Um achado confirmado e grave é bloqueante para o
/// juiz da dimensão e para o veredito final, e nada a jusante o desfaz. Os juízes que dependem de dados que esta entrega ainda
/// não tem aparecem como <c>not_run</c>, com o motivo, em vez de sumirem do relatório.
/// </summary>
internal static class ReviewJudges
{
    private sealed record Dimension(string Id, string Name, string[] Criteria);

    private static readonly Dimension[] Dimensions =
    [
        new(JudgeIds.Security, "Segurança", [
            "Nenhuma credencial de provedor nas linhas alteradas.",
            "Nenhum arquivo que guarda credenciais entra na alteração.",
            "A verificação de certificado do servidor continua ligada.",
            "Consultas e HTML não são montados com valores sem tratamento (só como hipótese: o fluxo do valor não é visto)."]),
        new(JudgeIds.Architecture, "Arquitetura", [
            "Os arquivos alterados não aparecem nos achados da análise de arquitetura (direção de camadas, fronteiras entre módulos)."]),
        new(JudgeIds.CodeQuality, "Qualidade do código", [
            "Nenhum conflito de merge não resolvido.",
            "Nenhuma falha engolida por um tratamento vazio.",
            "Nenhum método async void fora de manipulador de evento."]),
        new(JudgeIds.Regression, "Regressão", [
            "Testes não são apagados, removidos em saldo nem desligados.",
            "Dependências alteradas levam o arquivo de bloqueio junto."])
    ];

    public static IReadOnlyList<JudgeResult> Evaluate(JudgeInput input, IReadOnlyDictionary<string, JudgeResult>? overrides = null)
    {
        var results = new List<JudgeResult>();
        foreach (var dimension in Dimensions)
        {
            var owned = input.Findings.Where(finding => ReviewRules.Find(finding.RuleId).Judge == dimension.Id).ToList();
            results.Add(Judge(dimension, owned, input));
        }
        results.Add(overrides?.GetValueOrDefault(JudgeIds.Requirements) ?? NotRun(JudgeIds.Requirements, "Requisitos", "A revisão não recebeu critérios de aceite: escreva-os para este juiz entrar.",
            ["A alteração atende os critérios de aceite da atividade, e dá para apontar a linha do diff que atende cada um."]));
        results.Add(NotRun(JudgeIds.Dependency, "Dependências entre componentes", "O mapa de dependências entre módulos e repositórios ainda não existe (Q5).",
            ["Contratos, eventos e serviços que dependem do que mudou foram identificados."]));
        results.Add(overrides?.GetValueOrDefault(JudgeIds.Model) ?? NotRun(JudgeIds.Model, "Revisão por modelo de IA", "Nenhuma análise de IA foi feita nesta revisão: você não a pediu (ela envia o diff, sem segredos, ao provedor configurado), e o que está acima são só verificações determinísticas.",
            ["Um modelo procura defeitos que os padrões não alcançam; o que ele aponta só vale com linha do diff e nunca bloqueia."]));
        results.Add(overrides?.GetValueOrDefault(JudgeIds.Execution) ?? NotRun(JudgeIds.Execution, "Verificações do projeto", "Build, testes e lint do projeto não foram pedidos: eles rodam código do projeto e só com a sua autorização.",
            ["As verificações que o próprio projeto define (build, testes, lint) passam com a alteração."]));
        results.Add(Final(results, input));
        return results;
    }

    private static JudgeResult Judge(Dimension dimension, IReadOnlyList<ReviewFinding> owned, JudgeInput input)
    {
        var ids = owned.Select(finding => finding.Id).ToList();
        if (dimension.Id == JudgeIds.Architecture && input.ArchitectureNotRunReason is { } why)
            return new JudgeResult(dimension.Id, dimension.Name, ReviewVerdicts.NotRun, why, dimension.Criteria, [], true);
        if (input.FilesAnalyzed == 0 && owned.Count == 0)
            return new JudgeResult(dimension.Id, dimension.Name, ReviewVerdicts.NotRun, "Nenhum arquivo alterado pôde ser analisado linha a linha.", dimension.Criteria, [], true);

        var blocking = owned.Where(IsBlocking).ToList();
        if (blocking.Count > 0)
            return new JudgeResult(dimension.Id, dimension.Name, ReviewVerdicts.Rejected,
                $"{Plural(blocking.Count, "problema confirmado e grave")} contra os critérios.", dimension.Criteria, ids, true);
        var warnings = owned.Where(finding => finding.Severity != QualitySeverities.Informational).ToList();
        if (warnings.Count > 0)
            return new JudgeResult(dimension.Id, dimension.Name, input.Truncated ? ReviewVerdicts.Inconclusive : ReviewVerdicts.ApprovedWithWarnings,
                $"{Plural(warnings.Count, "ponto")} para conferir; nenhum é confirmado e grave.", dimension.Criteria, ids, true);
        return new JudgeResult(dimension.Id, dimension.Name, input.Truncated ? ReviewVerdicts.Inconclusive : ReviewVerdicts.Approved,
            input.Truncated ? "Nada encontrado no que foi lido, mas a alteração não foi lida inteira." : "Nada encontrado contra os critérios nas linhas analisadas.",
            dimension.Criteria, ids, true);
    }

    private static JudgeResult NotRun(string id, string name, string reason, string[] criteria) =>
        new(id, name, ReviewVerdicts.NotRun, reason, criteria, [], true);

    // A única regra de decisão do veredito: o motor a aplica, e ela nunca é uma média de notas.
    private static JudgeResult Final(IReadOnlyList<JudgeResult> judges, JudgeInput input)
    {
        var criteria = new[] { "Nenhum achado confirmado e grave.", "A alteração foi lida por inteiro.", "Nenhum juiz determinístico reprovou." };
        var ids = input.Findings.Select(finding => finding.Id).ToList();
        var (verdict, reason) = Decide(judges, input);
        return new JudgeResult(JudgeIds.Final, "Entrega", verdict, reason, criteria, ids, true);
    }

    public static (string Verdict, string Reason) Decide(IReadOnlyList<JudgeResult> judges, JudgeInput input)
    {
        if (input.FilesChanged == 0 && input.Findings.Count == 0)
            return (ReviewVerdicts.NotRun, "Não há alterações para revisar.");

        var blocking = input.Findings.Where(IsBlocking).ToList();
        if (blocking.Count > 0)
            return (ReviewVerdicts.Rejected, $"{Plural(blocking.Count, "problema confirmado e grave")}: a entrega não deve seguir sem a correção.");
        if (judges.Any(judge => judge.Verdict == ReviewVerdicts.Rejected))
            return (ReviewVerdicts.Rejected, "Um juiz reprovou a alteração.");

        if (input.Truncated || input.FilesAnalyzed == 0)
            return (ReviewVerdicts.Inconclusive, input.Truncated
                ? "A alteração é grande demais para ser lida inteira: o que ficou de fora não foi avaliado."
                : "Nenhum arquivo alterado pôde ser analisado linha a linha.");

        if (input.GatesRequestedNotRun)
            return (ReviewVerdicts.Inconclusive, "Uma verificação do projeto que você pediu não rodou ou não terminou: sem o resultado dela, a revisão não aprova.");

        var warnings = input.Findings.Where(finding => finding.Severity != QualitySeverities.Informational).ToList();
        if (warnings.Count > 0)
            return (ReviewVerdicts.ApprovedWithWarnings, $"{Plural(warnings.Count, "ponto")} para conferir, nenhum confirmado e grave.");
        if (input.OptionalRequestedNotRun)
            return (ReviewVerdicts.ApprovedWithWarnings, "A revisão por modelo ou a conferência de requisitos que você pediu não rodou: o que passou é só o que as verificações determinísticas alcançam.");
        return (ReviewVerdicts.Approved, "Nenhum problema encontrado pelas verificações determinísticas.");
    }

    // Só o que o motor provou (confirmado) e é grave bloqueia; hipótese e heurística pedem conferência, não barram.
    public static bool IsBlocking(ReviewFinding finding) =>
        finding.Confidence == FindingConfidences.Confirmed && QualitySeverities.Rank(finding.Severity) >= QualitySeverities.Rank(QualitySeverities.High);

    private static string Plural(int count, string singular) =>
        count == 1 ? $"1 {singular}" : $"{count} {Pluralize(singular)}";

    // Os textos de cima são os únicos que passam por aqui; a regra é "acrescenta s à última palavra do primeiro termo".
    private static string Pluralize(string text) => text switch
    {
        "problema confirmado e grave" => "problemas confirmados e graves",
        "ponto" => "pontos",
        _ => text + "s"
    };
}
