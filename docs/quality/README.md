# Engineering Quality Intelligence

> Primeira entrega, de 2026-11-27: a revisão nativa das alterações locais e de uma branch. O que é futuro está no
> [ROADMAP](../ROADMAP.md), seção 8 (Q0 a Q7), sem backlog paralelo. A decisão de arquitetura está na
> [ADR-0011](../architecture/adr/0011-engineering-quality-intelligence.md).

## 1. O que é, e o que não é

O Spock configura e orquestra agentes de desenvolvimento; este domínio verifica, com evidência, o trabalho que eles (ou uma
pessoa) deixaram no projeto. Lê o que mudou no git, procura defeitos com verificações do próprio motor, deixa juízes com
critérios escritos decidirem e guarda o relatório na pasta de dados do Spock.

Não é um agente de IA, não escreve o relatório com modelo e não depende de nenhum serviço externo. **Nesta entrega nenhum
modelo participa**: a revisão por modelo (que acharia o que os padrões não alcançam) é a Q2, e o relatório diz isso em cada
revisão. Também não executa build, testes nem lint do projeto (Q2), não publica nada em GitHub ou Jira (Q6) e não altera
nenhum arquivo do projeto analisado.

## 2. O que existe, por camada

| Camada | Onde | O que faz |
| --- | --- | --- |
| Leitor de alterações | `Quality/Git/GitChangeReader.cs` | Só comandos de leitura do git, sem shell, sem checkout, reset, stash, commit nem troca de branch, sem driver de diff nem conversão de texto do repositório, sem o monitor de arquivos e sem travar o índice. Lê o diff contra `HEAD` (índice e pasta de trabalho juntos), os arquivos novos, os conflitos, e a diferença `base...head` de uma branch. A base é sempre explícita. |
| Parser e classificação | `Quality/Git/UnifiedDiffParser.cs`, `FileClassifier.cs` | Linhas por hunk com a numeração nova, o símbolo mais próximo que o git mostra, renomeados, apagados, binários. Gerado, de terceiros, arquivo de bloqueio e alteração enorme ficam na lista mas não são lidos linha a linha. |
| Regras | `Quality/ReviewRules.cs` | 15 regras `QR-*`, cada uma com categoria, gravidade, confiança, juiz, o esperado e a correção. É o embrião do modelo de regras da Q4. |
| Detectores | `Quality/ChangeDetectors.cs` | Só as linhas adicionadas e a lista de arquivos. Segredos (o mesmo detector do resto do motor), arquivo sensível, conflito de merge, erro engolido, `async void`, certificado desligado, SQL concatenado e execução dinâmica (hipóteses), teste apagado, removido em saldo ou desligado, dependência sem arquivo de bloqueio, arquivo do Harness alterado. |
| Arquitetura | `ArchitectureAnalyzer` (existente) | Os achados que tocam arquivos alterados entram como heurística: a análise lê o projeto inteiro e não separa o que existia antes. |
| Juízes | `Quality/ReviewJudges.cs` | Segurança, Arquitetura, Qualidade do código, Regressão e Entrega, todos determinísticos. Requisitos, Dependências e Revisão por modelo aparecem como `not_run`, com o motivo. |
| Serviço e histórico | `Quality/CodeReviewService.cs`, `FileReviewStore.cs` | Monta o relatório, descarta o achado cuja linha mudou durante a revisão, deduplica, grava em `projects/<id>/reviews/` (100 por projeto) e audita. |
| Protocolo | `quality.review.start`, `.list`, `.detail` | `start` sem `mode` é a revisão das alterações locais; `mode: "branch"` exige `base`. |
| Boot | `quality.review_changes` (direta), `quality.review_branch` (abre a tela) | A primeira executa no nível de observação porque só lê. A segunda abre a tela porque pede a base. |
| Interface | `/qualidade`, em Engenharia | Veredito, problemas confirmados, a conferir, recomendações, juízes, o que não foi verificado, próximos passos, arquivos e histórico. |

## 3. Contratos

**Achado** (`quality.review.v1`): `id` (estável entre revisões para o mesmo defeito no mesmo lugar), `ruleId`, `reviewId`,
`projectId`, `path`, `startLine`/`endLine` (nulos quando a evidência é o arquivo), `onChangedLines`, `category`
(`bug`, `security`, `architecture`, `reliability`, `performance`, `maintainability`, `requirement-compliance`,
`compatibility`), `severity` (`critical`, `high`, `medium`, `low`, `informational`), `confidence`, `title`, `description`,
`evidence` (trecho, relação com a alteração, origem, símbolo, contexto consultado), `expectedBehavior`, `actualBehavior`,
`suggestedCorrection`, `relatedRequirement`, `relatedArchitectureRule`, `source`, `detector`, `status`, `createdAt`.

**Confiança não é porcentagem.** Cada nível tem um critério que se confere:

- `confirmed`: regra determinística, evidência nas linhas alteradas, e o padrão sozinho já é o defeito.
- `heuristic`: regra determinística com evidência conferida, mas só uma pessoa sabe se o padrão é defeito naquele contexto.
- `hypothesis`: depende de contexto fora do diff; nunca é tratada como defeito, nunca bloqueia.

**Revisão**: veredito (`approved`, `approved_with_warnings`, `rejected`, `inconclusive`, `not_run`), resumo, contagens,
arquivos (com o motivo de não terem sido lidos), achados, juízes, regras aplicadas, contexto consultado, limitações e
próximos passos. O formato é conferido contra as fixtures `contracts/fixtures/golden/quality-*.json`, que o teste do motor
gera do que o protocolo responde de verdade, e o Zod da interface lê as mesmas.

## 4. A regra de decisão

O veredito não é média de notas, e o motor o aplica: **reprovada** quando há achado `confirmed` e grave (`high` ou
`critical`), ou quando um juiz reprova; **inconclusiva** quando a alteração foi lida pela metade ou nenhum arquivo pôde ser
analisado; **aprovada com ressalvas** quando sobra algo que não é só informativo; **aprovada** quando nada sobra;
**não executada** quando não há alteração. Um juiz que diz "aprovado" nunca desfaz um achado confirmado e grave (há teste).
Heurística e hipótese pedem conferência e não barram. Aprovar significa "nenhum problema encontrado pelas verificações
determinísticas", e o relatório diz quais verificações faltaram.

## 5. Segurança

- A pasta é a autorizada; o id do projeto nunca vira caminho fora do `WorkspaceAuthorizer`.
- `git` é chamado com argumentos separados, `core.fsmonitor=false`, `GIT_OPTIONAL_LOCKS=0`, `GIT_TERMINAL_PROMPT=0`,
  `--no-ext-diff` e `--no-textconv`. Referências que começam com `-` ou fora do conjunto de caracteres de nomes de ref são
  recusadas antes de chegar ao git, e o `rev-parse` usa `--end-of-options`.
- Um teste prova que a pasta do projeto e o índice do git ficam idênticos antes e depois da revisão.
- O valor de um segredo nunca entra no relatório: o trecho é a linha de omissão, e há teste sobre o registro gravado e o
  que o protocolo devolve. Todo trecho passa pela redação de linhas com cara de segredo.
- O conteúdo do diff é dado, não instrução: nenhum texto do projeto chega a um modelo, porque nenhum modelo participa.

## 6. O que está provado e o que não está

Provado por teste automatizado, com repositórios git de verdade em pastas temporárias: leitura de alterações locais, de
arquivos novos, de repositório sem commit, de conflito de merge e de branch; recusa de referência inválida e de pasta que
não é repositório; achados com arquivo, linha e evidência; segredo omitido; arquivo gerado, binário e de terceiros fora da
leitura; decisão do veredito e imunidade dos juízes; persistência, histórico e id estável; formato do protocolo; Boot
reconhecendo o pedido e executando a capacidade só de leitura; tela com os grupos, as limitações e o histórico.

**Uma rodada real, no próprio repositório do Spock** (45 arquivos alterados, pelo executável do motor e pelo mesmo protocolo
da interface): a primeira versão apontou 8 pontos, todos falsos positivos, porque os detectores liam como código os padrões
que aparecem dentro de textos entre aspas (os próprios detectores e os testes que escrevem código). Os padrões passaram a ser
avaliados sem o conteúdo dos textos entre aspas (só o SQL concatenado precisa deles), e a rodada seguinte deu "aprovada", com
duas recomendações informativas de arquivo grande. O `git status` do repositório ficou idêntico antes e depois, e o Boot, com o
pedido "Revise minhas alterações antes do commit" em nível 0, reconheceu e executou `quality.review_changes`.

**Limites conhecidos do que está provado:** um literal de expressão regular em JavaScript (`/eval\(/`) ainda é lido como
código (caso `lacuna-literal-de-regex-em-js` do gabarito, fora da meta e conferido para avisar quando for corrigido); um
apóstrofo de linguagem que não abre texto (como o tempo de vida do Rust) esvazia o resto da linha e pode esconder um padrão.

**Não provado:** a precisão dos detectores. Não há linha de base (a eval da seção 7 mede o conjunto atual, escrito por quem
escreveu os detectores, e por isso só serve contra regressão, não como medida de qualidade). Também não foi vista no
aplicativo aberto com um projeto real do dono.

## 7. Eval

`CodeReviewEvaluationTests` roda os detectores contra casos com gabarito em `datasets/code-review.json` (código correto,
um defeito por regra, falsos positivos conhecidos) e mede recall e falsos positivos. O gabarito foi escrito à mão; o
conjunto é pequeno e serve para impedir regressão. Antes de transformar uma meta em gate é preciso uma linha de base sobre
alterações reais, revisadas por uma pessoa.
