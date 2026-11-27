using System.Globalization;
using System.Text;

namespace Mariliz.Application.Boot;

public enum IntentResolution
{
    Resolved,
    NeedsClarification,
    NoMatch
}

/// <param name="Hint">O que a tela já sabe (o tipo de atividade que a pessoa marcou); reforça, nunca decide sozinho.</param>
/// <param name="CapabilityId">Quando a pessoa abriu uma funcionalidade direto, não há o que interpretar.</param>
public sealed record IntentRequest(string? Text, string? Hint, string? CapabilityId, bool HasProject);

/// <param name="Score">Soma dos pesos dos termos e do contexto. Ordena candidatos; não é probabilidade.</param>
public sealed record IntentCandidate(string CapabilityId, int Score, double Confidence, IReadOnlyList<string> Matched);

/// <param name="Source">"text" (léxico), "explicit" (escolha direta) ou "model" (segundo estágio).</param>
/// <param name="ModelStage">O que houve com o estágio do modelo; ver <see cref="ModelStages"/>.</param>
public sealed record IntentResult(
    IntentResolution Resolution,
    string? CapabilityId,
    string Source,
    string? Domain,
    IReadOnlyList<IntentCandidate> Candidates,
    string? Question,
    string Objective,
    string ModelStage = ModelStages.Skipped);

/// <summary>
/// Reconhece a intenção a partir das fichas do catálogo: termos ponderados (palavra ou frase em ordem), reforço pela dica da tela e
/// pelo contexto (há projeto aberto?), e uma regra de desempate que prefere perguntar a adivinhar. É léxico e roda sem modelo, de
/// propósito: o resultado é reprodutível e a eval de intenção mede o que erra. O que o léxico não resolve pode ir a um modelo
/// (<see cref="ModelIntentStage"/>), e <see cref="WithModel"/> é a única regra que aceita a proposta dele.
/// </summary>
public sealed class IntentRecognizer(CapabilityRegistry registry)
{
    public const int MinScore = 4;
    public const int ClarifyGap = 2;
    public const int MemberMinScore = 3;
    public const int HintBonus = 3;
    public const int NoProjectPenalty = 3;
    public const int MaxObjective = 280;
    public const int MaxOptions = 3;

    public IntentResult Recognize(IntentRequest request)
    {
        var objective = Objective(request.Text);

        if (request.CapabilityId is { } explicitId)
        {
            var found = registry.Find(explicitId);
            return found is null
                ? new IntentResult(IntentResolution.NoMatch, null, "explicit", null, [], $"A capacidade \"{explicitId}\" não existe.", objective)
                : new IntentResult(IntentResolution.Resolved, found.Id, "explicit", found.Domain,
                    [new IntentCandidate(found.Id, MinScore, 1, ["escolha direta"])], null, objective);
        }

        var tokens = Tokens(request.Text);
        if (tokens.Count == 0)
            return new IntentResult(IntentResolution.NoMatch, null, "text", null, [], "Escreva o que você quer fazer.", objective);

        var scored = new List<IntentCandidate>();
        foreach (var capability in registry.All.Where(capability => !capability.IsComposite))
            if (Score(capability, tokens, request) is { } candidate) scored.Add(candidate);

        foreach (var composite in registry.All.Where(capability => capability.IsComposite))
        {
            var members = composite.Composes
                .Select(id => scored.FirstOrDefault(candidate => candidate.CapabilityId == id))
                .Where(candidate => candidate is not null && candidate.Score >= MemberMinScore)
                .Cast<IntentCandidate>().ToList();
            // A primeira etapa é a que dá o nome à composição: sem ela, as outras sozinhas são só pedidos soltos.
            if (members.Count >= 2 && members.Any(member => member.CapabilityId == composite.Composes[0]))
            {
                var score = members.Sum(member => member.Score) + 2;
                scored.Add(new IntentCandidate(composite.Id, score, Confidence(score), [.. members.SelectMany(member => member.Matched).Distinct()]));
            }
        }

        // Sem projeto aberto, o que precisa de projeto continua sendo reconhecido (o Boot diz que falta escolher um), mas perde
        // para o que responde sem projeto quando a diferença é pequena: "riscos dos meus projetos" vai para o portfólio.
        int Rank(IntentCandidate candidate) =>
            candidate.Score - (!request.HasProject && registry.Find(candidate.CapabilityId)!.Needs.HasFlag(CapabilityNeeds.Project) ? NoProjectPenalty : 0);
        var ranked = scored.OrderByDescending(Rank).ThenBy(candidate => candidate.CapabilityId, StringComparer.Ordinal).ToList();
        if (ranked.Count == 0 || ranked[0].Score < MinScore)
            return new IntentResult(IntentResolution.NoMatch, null, "text", null, [.. ranked.Take(3)],
                "Não encontrei uma capacidade do Spock para esse pedido. Descreva o resultado que você quer, ou escolha uma das opções da tela.", objective);

        var top = ranked[0];
        var close = ranked.Skip(1).Where(candidate => Rank(top) - Rank(candidate) < ClarifyGap && candidate.Score >= MinScore - 1).ToList();
        if (close.Count > 0 && !IsMemberOf(top, close, registry))
        {
            var options = new[] { top }.Concat(close).Take(3).ToList();
            return new IntentResult(IntentResolution.NeedsClarification, null, "text", null, options, Question(options), objective);
        }

        return new IntentResult(IntentResolution.Resolved, top.CapabilityId, "text", registry.Find(top.CapabilityId)!.Domain, [.. ranked.Take(3)], null, objective);
    }

    /// <summary>
    /// Aceita a proposta do modelo só onde o léxico não resolveu; um léxico <see cref="IntentResolution.Resolved"/> nunca é trocado.
    /// A proposta já veio validada contra o catálogo, e aqui ela só decide entre três saídas: uma capacidade (resolve), duas ou mais
    /// opções (pergunta, que é o que o léxico faria) ou nada (o resultado do léxico fica como estava). O que acontece depois da
    /// escolha, disponibilidade, permissão e plano, é o fluxo de sempre.
    /// </summary>
    public IntentResult WithModel(IntentResult lexicon, ModelIntentProposal proposal)
    {
        if (lexicon.Resolution == IntentResolution.Resolved || lexicon.Source == "explicit") return lexicon;
        if (proposal.State != ModelStages.Used) return lexicon with { ModelStage = proposal.State };

        var options = new List<string>();
        if (proposal.CapabilityId is not null) options.Add(proposal.CapabilityId);
        options.AddRange(proposal.Alternatives);
        options = [.. options.Distinct(StringComparer.Ordinal).Take(MaxOptions)];

        // Uma só opção resolve; duas ou mais viram pergunta (o modelo mesmo disse que o pedido cabe em mais de uma coisa). Alternativa
        // solta, sem capacidade principal, não é palpite que valha uma pergunta: o léxico segue.
        if (options.Count == 1 && proposal.CapabilityId is not null && proposal.Alternatives.Count == 0)
            return new IntentResult(IntentResolution.Resolved, options[0], "model", registry.Find(options[0])!.Domain, [Proposed(options[0])], null,
                lexicon.Objective, ModelStages.Used);
        if (options.Count >= 2)
        {
            var proposed = options.Select(Proposed).ToList();
            return new IntentResult(IntentResolution.NeedsClarification, null, "model", null, proposed, Question(proposed), lexicon.Objective, ModelStages.Used);
        }
        return lexicon with { ModelStage = ModelStages.Used };
    }

    // O modelo não pontua: o candidato dele não tem peso de termo, só a origem.
    private static IntentCandidate Proposed(string capabilityId) => new(capabilityId, 0, 0, ["modelo"]);

    private string Question(IReadOnlyList<IntentCandidate> options)
    {
        var names = options.Select(option => registry.Find(option.CapabilityId)!.Name).ToList();
        return $"O pedido cabe em mais de uma coisa: {string.Join(", ", names.Take(names.Count - 1))} ou {names[^1]}. Qual delas?";
    }

    // Uma composição sempre empata com os próprios membros por construção; perguntar entre elas seria ruído.
    private static bool IsMemberOf(IntentCandidate top, List<IntentCandidate> close, CapabilityRegistry registry) =>
        registry.Find(top.CapabilityId) is { IsComposite: true } composite && close.All(candidate => composite.Composes.Contains(candidate.CapabilityId));

    private static IntentCandidate? Score(Capability capability, IReadOnlyList<string> tokens, IntentRequest request)
    {
        var score = 0;
        var matched = new List<string>();
        foreach (var term in capability.Terms)
        {
            if (!Matches(term.Text, tokens)) continue;
            score += term.Weight;
            matched.Add(term.Text);
        }

        // A dica só reforça o que o texto já sugeriu: sozinha ela não escolhe nada. Quem marcou o tipo de atividade de propósito
        // manda a capacidade direto, sem interpretação.
        if (matched.Count == 0) return null;
        var hinted = request.Hint is not null && capability.Hints.Contains(request.Hint, StringComparer.Ordinal);

        if (hinted)
        {
            score += HintBonus;
            matched.Add("dica: " + request.Hint);
        }
        if (capability.Needs.HasFlag(CapabilityNeeds.Project) && request.HasProject) score += 1;
        return score <= 0 ? null : new IntentCandidate(capability.Id, score, Confidence(score), matched);
    }

    private static double Confidence(int score) => Math.Round(score / (score + 4.0), 2);

    // As palavras da frase aparecem em ordem, com até duas outras no meio; "x*" casa qualquer palavra que comece por x.
    internal static bool Matches(string phrase, IReadOnlyList<string> tokens)
    {
        var words = phrase.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        for (var start = 0; start < tokens.Count; start++)
        {
            if (!WordMatches(words[0], tokens[start])) continue;
            var at = start;
            var all = true;
            for (var next = 1; next < words.Length && all; next++)
            {
                var found = false;
                for (var look = at + 1; look <= at + 3 && look < tokens.Count; look++)
                {
                    if (!WordMatches(words[next], tokens[look])) continue;
                    at = look;
                    found = true;
                    break;
                }
                all = found;
            }
            if (all) return true;
        }
        return false;
    }

    private static bool WordMatches(string word, string token) =>
        word.EndsWith('*') ? token.StartsWith(word[..^1], StringComparison.Ordinal) : word == token;

    internal static List<string> Tokens(string? text)
    {
        var tokens = new List<string>();
        if (string.IsNullOrWhiteSpace(text)) return tokens;
        var word = new StringBuilder();
        foreach (var c in text.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsLetterOrDigit(c)) word.Append(char.ToLowerInvariant(c));
            else Flush();
        }
        Flush();
        return tokens;

        void Flush()
        {
            if (word.Length > 0) tokens.Add(word.ToString());
            word.Clear();
        }
    }

    private static string Objective(string? text)
    {
        var flat = string.Join(' ', (text ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return flat.Length <= MaxObjective ? flat : flat[..(MaxObjective - 1)] + "…";
    }
}
