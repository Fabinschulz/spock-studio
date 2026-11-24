using System.Text;
using Mariliz.Application.Workflows;

namespace Mariliz.Application.Ideas;

// O formato de cada etapa é um só: o modelo (modo Chave) e o agente da sessão (modo Sessão) escrevem o mesmo JSON, e as
// mesmas verificações conferem os dois.
internal static class IdeaFormats
{
    public const string Specification = """
        {"title": "nome curto", "problem": "o problema em duas frases", "users": ["quem usa"], "goals": ["resultado esperado"],
         "requirements": [{"id": "REQ-01", "text": "Quando ..., o sistema deve ...", "priority": "must|should|could"}],
         "nonFunctional": [{"id": "NFR-01", "text": "O sistema deve ..."}], "outOfScope": ["..."], "openQuestions": ["..."]}
        """;

    public const string SpecificationRules = """
        De 3 a 30 requisitos, ids únicos em sequência. Cada requisito diz o que o sistema deve fazer (use a palavra "deve"),
        no estilo EARS ("Quando <gatilho>, o sistema deve <resposta>"). Ao menos um requisito é must. Ao menos um requisito
        não funcional (desempenho, segurança, disponibilidade, custo, operação): a escolha da stack depende deles.
        """;

    public const string Architecture = """
        {"style": "estilo (ex.: monólito modular)", "summary": "visão geral em até quatro frases",
         "components": [{"id": "CMP-01", "name": "...", "responsibility": "...", "requirements": ["REQ-01"]}],
         "factors": [{"id": "F1", "criterion": "10.4-conhecimento-da-equipe", "fact": "o fato do projeto", "favors": ".NET"}],
         "stack": [{"area": "backend|frontend|dados|infra|outro", "choice": "tecnologia", "reason": "por que ela",
                    "question": "10.4", "option": ".NET", "factors": ["F1"], "requirements": ["NFR-01"],
                    "alternatives": [{"choice": "o que foi descartado", "whyNot": "por quê"}], "tradeoffs": "o que se aceita perder",
                    "exception": "só quando a opção vai contra os fatores ou desempata"}],
         "data": ["entidade e onde fica"], "integrations": ["sistema externo e para quê"],
         "risks": [{"text": "risco", "mitigation": "como reduzir"}], "decisions": [{"title": "...", "choice": "...", "reason": "..."}]}
        """;

    public const string ArchitectureRules = """
        Cite em cada componente só ids de requisitos da especificação. Todo requisito must precisa de um componente responsável.
        Prefira a solução mais simples que atende aos requisitos.
        Escolha a stack pelos critérios do §10 do catálogo da Organização, listados abaixo: cada fator (F1, F2...) é um fato
        do projeto, tirado da ideia ou da especificação, que cita um critério e diz a opção que ele favorece. O backend responde
        à pergunta 10.4 e o frontend à 10.1, com "question", "option" e os fatores; outras áreas citam a pergunta do §10 que
        se aplica ou os requisitos (REQ, NFR) que a escolha atende. A opção é a que os fatores citados favorecem, e cada critério
        conta uma vez, mesmo citado em dois fatores; se a opção for outra, ou se houver empate, escreva a exceção. Não recomende .NET por padrão: liste os fatores e mostre qual opção eles
        favorecem. Toda escolha diz o que descartou, por quê, e o trade-off que aceita. Não invente fato que a ideia não traz:
        o que faltar vira pergunta em aberto.
        """;

    public const string Backlog = """
        {"epics": [{"id": "EPIC-01", "title": "...", "stories": [{"id": "US-01", "title": "Como <usuário>, quero <ação> para <benefício>",
          "requirements": ["REQ-01"], "acceptance": ["Dado ..., quando ..., então ..."], "estimate": "P|M|G"}]}]}
        """;

    public const string BacklogRules = """
        Ids únicos em todo o backlog. Todo requisito da especificação precisa de ao menos uma história. Cada história tem ao
        menos um critério de aceite e cita só requisitos que existem. Ordene do que destrava mais para o que destrava menos.
        """;

    // Um critério por linha, com as opções que ele favorece: é o que o fator cita em "criterion" e "favors".
    public static string Criteria()
    {
        var text = new StringBuilder();
        foreach (var question in OrganizationReferences.DecisionCriteria.GroupBy(criterion => criterion.Question))
        {
            var first = question.First();
            text.Append("Pergunta ").Append(question.Key).Append(' ').Append(first.QuestionTitle).Append(" (análise de ")
                .Append(first.AnalyzedAt.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)).AppendLine("):");
            foreach (var criterion in question)
                text.Append("- ").Append(criterion.Id).Append(": ")
                    .AppendLine(string.Join("; ", criterion.Options.Select(option => $"favorece \"{option.Name}\" quando {option.Text}")));
        }
        return text.ToString().TrimEnd();
    }
}
