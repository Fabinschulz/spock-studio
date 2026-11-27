# ADR-0011 — Engineering Quality Intelligence: revisão nativa, com o motor decidindo e o modelo, quando vier, só propondo

**Status:** implementada em 2026-11-27 na primeira entrega (revisão das alterações locais e de uma branch); as demais ondas
(Q0 a Q7) estão no [ROADMAP](../../ROADMAP.md), seção 8. Aguarda o dono nas decisões QD-1 a QD-3 abaixo.

## Contexto

O Spock configura o Harness e orquestra agentes, mas não tinha como verificar o trabalho que eles deixam: o juiz com modelo
só avalia planos e impacto de uma execução de engenharia, os juízes de gestão conferem respostas, e os agentes
`code-reviewer` e `verifier` são texto para a IDE. O motor não lia diff nem tinha um relatório de revisão.

## Decisão

1. **Um módulo, `Mariliz.Application/Quality`, e não um serviço por responsabilidade.** Leitor de alterações, regras,
   detectores, juízes e serviço moram juntos porque mudam juntos; o que já existe (Context Engine, Harness, análise de
   arquitetura, detector de segredos, auditoria, trilha de execução de processos) é chamado, não copiado.
2. **O leitor de alterações é só leitura e mora na aplicação**, sobre o `IProcessRunner` que já existe: sem shell, sem
   comando que escreva, sem driver externo do repositório, sem travar o índice. A base de uma comparação é sempre explícita.
3. **O motor decide; o modelo propõe.** A regra de decisão do veredito é código, conferida por teste, e um achado confirmado e
   grave nunca é desfeito por um juiz. Quando a revisão por modelo existir (Q2), o que ela escrever entra como hipótese
   verificável (arquivo, linha e trecho que existem no diff) e nunca altera política, permissão nem critério.
4. **Confiança por critério, não por porcentagem** (`confirmed`, `heuristic`, `hypothesis`), e só o confirmado e grave
   bloqueia. A confiança de um achado é a da regra que o produziu, escrita por quem mantém o motor.
5. **Juízes determinísticos agora, e honestos sobre o que faltou.** Os juízes que dependem de requisitos, do mapa de
   dependências ou de um modelo aparecem como `not_run` com o motivo. "Independência" aqui é por construção: nenhum modelo
   nem o agente que fez a alteração participa; o relatório diz isso, e não chama de independente o que for o mesmo modelo.
6. **O Boot ganha duas capacidades**, no domínio de engenharia: `quality.review_changes` (direta, só leitura, classe
   `Analysis`) e `quality.review_branch` (abre a tela, porque pede a base). A revisão grava o relatório só na pasta de dados
   do Spock, como o Boot grava os registros dele. Não há "corrigir" no Boot nesta entrega.
7. **O histórico é um arquivo por revisão** (`projects/<id>/reviews/`), com id estável por achado, para comparar uma revisão
   com a anterior sem copiar dados.

## Decisões abertas

- **QD-1**: o contrato de regras da Q4 (organização, projeto, diretório, exceção) é uma nova camada sobre o Harness ou um
  campo estruturado nos arquivos que ele já projeta? A Q4 não começa sem esta resposta.
- **QD-2**: a revisão por modelo (Q2) roda sempre num provedor diferente do que implementou, quando houver mais de um, ou
  registra apenas a limitação? Hoje há um provedor por vez.
- **QD-3**: executar build e testes do projeto (Q2) só com o comando descoberto, e com aprovação por execução ou por
  projeto? É a decisão 3 do ciclo 2, ainda aberta.

## Alternativas descartadas

- **Adotar uma ferramenta externa de revisão como núcleo.** Cria dependência de produto, de licença e de envio de código para
  fora; o isolamento por projeto e a redação de segredos deixariam de ser do motor. Um adaptador opcional, isolado, continua
  possível na Q6, depois de avaliar licença, manutenção e segurança.
- **Um agente de revisão em Markdown.** Já existe para a IDE; não produz registro, não tem veredito conferível e entrega a
  decisão ao mesmo modelo que escreveu o código.
- **Revisar só quando um Pull Request abre.** A qualidade fica para o fim; ler o diff local pega o defeito antes do commit.
- **Uma nota de qualidade de 0 a 100.** Média esconde o achado grave; o veredito é regra, não soma.
