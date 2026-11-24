namespace Mariliz.Application.Analysis;

// O roteiro do agente da sessão para avaliar a arquitetura (W4-1, modo Sessão). As regras são as que
// ArchitectureAssessments confere; o que não dá para conferir sem modelo (a qualidade do julgamento) fica com a pessoa.
public static class ArchitectureAssessmentGuide
{
    public static string For(string path) => $$"""
        Avalie a arquitetura deste projeto: compare o que o código faz hoje (a análise acima e o próprio código) com as
        referências da pasta Organização (REF-*), e separe o que é convenção do projeto do que é divergência.
        Formato exato do arquivo:
        {"summary": "duas ou três frases sobre o estado da arquitetura",
         "conventions": [{"title": "o que o projeto faz de propósito e deve continuar", "files": ["caminho/relativo"]}],
         "divergences": [{"title": "...", "classification": "risk", "description": "o que diverge e por que importa",
           "files": ["caminho/relativo"], "references": ["REF-..."], "findings": ["dependency-direction"],
           "recommendation": "o próximo passo, pequeno e reversível"}]}
        Classificação: risk (pode quebrar, vazar dados ou travar a evolução), cost (encarece manter ou rodar), style (diverge
        da referência sem risco nem custo).
        Regras:
        - Toda divergência cita ao menos um arquivo do projeto, com caminho relativo à raiz, e se apoia numa REF-* ou num
          achado da análise (o código entre colchetes, como dependency-direction).
        - O que o projeto faz de propósito e de forma consistente é convenção, não divergência: as convenções do projeto
          valem antes das referências.
        - Não proponha reescrita: a recomendação é um passo pequeno, que convive com o código atual.
        - Não invente arquivos, REF-* nem achados: o Spock confere cada um.
        Passos:
        1. Leia a análise e as referências abaixo; o padrão inteiro de uma REF-* vem de spock_architecture_reference com o id.
        2. Leia os arquivos que vai citar antes de afirmar algo sobre eles.
        3. Grave o objeto em {{path}} e chame spock_architecture_assessment_check até passar em todas as verificações.
        4. Diga à pessoa para abrir a página do projeto no Spock, seção Arquitetura. Nesta tarefa você não muda o código.
        """;
}
