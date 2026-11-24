namespace Mariliz.Application.Analysis;

// O roteiro do agente da sessão para propor a modernização (W4-2, modo Sessão). As regras são as que ModernizationPlans
// confere; a pessoa aprova cada passo no Spock, e só então o agente executa.
public static class ModernizationPlanGuide
{
    public static string For(string path) => $$"""
        Proponha um plano de modernização a partir da avaliação acima: passos pequenos, em ordem, que convivem com o código
        atual. Antes de mudar, caracterize: diga que comportamento de hoje tem de continuar igual e que teste o prende.
        Formato exato do arquivo:
        {"goal": "aonde o plano leva o projeto, em uma ou duas frases",
         "characterization": [{"behavior": "o que o código faz hoje e deve continuar", "files": ["caminho/relativo"],
           "test": "o teste que prende esse comportamento: o que já existe ou o primeiro que o plano cria"}],
         "steps": [{"id": "S1", "title": "...", "kind": "incremental", "divergences": ["D1"],
           "change": "o que muda, em termos concretos", "files": ["caminho/relativo/que/já/existe"], "dependsOn": [],
           "undo": "como voltar atrás se der errado", "verification": "como saber que deu certo",
           "risk": "o que pode quebrar", "cost": "esforço estimado", "coexistence": "como o novo e o antigo convivem"}]}
        Tipos de passo: incremental (muda um pedaço e o resto continua funcionando) ou rewrite (substitui um componente
        inteiro). Em rewrite, cost e coexistence são obrigatórios; em incremental são opcionais.
        Regras:
        - Todo passo trata ao menos uma divergência da avaliação, pelo id (D1, D2...), e cita ao menos um arquivo que já
          existe; arquivos que o passo vai criar ficam na descrição da mudança.
        - Todo passo diz como se desfaz, como se verifica e o risco.
        - Prefira passos incrementais. Reescrever é exceção: declare como rewrite, com custo, risco e plano de convivência.
          O Spock recusa passo incremental que fala em reescrever.
        - dependsOn só cita passos anteriores do plano.
        - Não invente arquivos nem divergências: o Spock confere cada um.
        Passos:
        1. Leia a avaliação abaixo e os arquivos que vai citar.
        2. Grave o objeto em {{path}} e chame spock_modernization_plan_check até passar em todas as verificações.
        3. Diga à pessoa para aprovar ou recusar cada passo no Spock, página Arquitetura. Nesta tarefa você não muda o código.
        4. Mais tarde, quando pedirem para executar, chame spock_modernization_plan_check e execute só os passos aprovados, em
           ordem, rodando a verificação de cada um. Um passo que você alterar depois da aprovação precisa ser aprovado de novo.
        """;
}
