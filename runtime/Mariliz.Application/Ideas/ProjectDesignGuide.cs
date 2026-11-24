namespace Mariliz.Application.Ideas;

// O que o agente da sessão precisa para escrever a jornada Da ideia ao projeto sem o Spock chamar modelo: o formato de cada
// etapa (o mesmo do modo Chave), os critérios do §10 do catálogo e onde gravar.
internal static class ProjectDesignGuide
{
    public static string For(string path) => $$"""
        Grave um único arquivo JSON em {{path}} (caminho completo), com quatro campos:

        {"idea": "a ideia da pessoa, como ela descreveu ({{IdeaArchitectService.MinIdea}} a {{IdeaArchitectService.MaxIdea}} caracteres)",
         "specification": <especificação>, "architecture": <arquitetura>, "backlog": <backlog>}

        ## Especificação

        {{IdeaFormats.Specification}}
        {{IdeaFormats.SpecificationRules}}

        ## Arquitetura

        {{IdeaFormats.Architecture}}
        {{IdeaFormats.ArchitectureRules}}

        ## Backlog

        {{IdeaFormats.Backlog}}
        {{IdeaFormats.BacklogRules}}

        ## Critérios do §10 do catálogo (cite pelo id em "criterion" e use o nome da opção em "favors" e "option")

        {{IdeaFormats.Criteria()}}

        ## Regras

        - Escreva em português do Brasil, com frases curtas.
        - Não invente integrações, números, clientes ou fatos da equipe que a pessoa não disse: pergunte a ela ou registre em "openQuestions".
        - Nunca escreva senhas, chaves, tokens ou endereços com credenciais.
        - Não grave os documentos em .spock/arquitetura nem crie código: quem grava é o Spock, depois que a pessoa aprova.

        ## Passos

        1. Grave o arquivo.
        2. Confira com {{Mcp.ProjectDesignTools.CheckTool}} e corrija até passar em todas as verificações.
        3. Peça à pessoa para abrir Da ideia ao projeto no Spock, ler o que você escreveu, aprovar cada etapa e gravar os documentos.
        """;
}
