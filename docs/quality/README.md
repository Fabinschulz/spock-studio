# Engineering Quality Intelligence

> Primeira entrega, de 2026-11-27 (revisão nativa das alterações locais e de uma branch), e a jornada de decisão de 2026-11-28 (modelo, critérios, verificações do projeto, decisões da pessoa e pedido de correção). O que é futuro está no
> [ROADMAP](../ROADMAP.md), seção 8 (Q0 a Q7), sem backlog paralelo. A decisão de arquitetura está na
> [ADR-0011](../architecture/adr/0011-engineering-quality-intelligence.md).

## 1. O que é, e o que não é

O Spock configura e orquestra agentes de desenvolvimento; este domínio verifica, com evidência, o trabalho que eles (ou uma
pessoa) deixaram no projeto. Lê o que mudou no git, procura defeitos com verificações do próprio motor, deixa juízes com
critérios escritos decidirem e guarda o relatório na pasta de dados do Spock.

Não é um agente de IA e não depende de nenhum serviço externo. A revisão simples (a única que o Boot e o MCP chamam) não usa
modelo, não roda comando do projeto e não altera arquivo nenhum. **O resto só entra por escolha da pessoa, na tela**: a
revisão por modelo (envia o diff, sem as linhas com cara de segredo, ao provedor configurado; o que ele aponta é hipótese e
nunca bloqueia), a conferência de critérios de aceite (cada um só vale como atendido com uma linha do diff como evidência) e
as verificações do projeto (build, testes e lint que o próprio projeto define, rodados como processo filho, sem sandbox e só
com a autorização dela). Nada é publicado em GitHub ou Jira (Q6).

## 2. O que existe, por camada

| Camada | Onde | O que faz |
| --- | --- | --- |
| Leitor de alterações | `Quality/Git/GitChangeReader.cs` | Só comandos de leitura do git, sem shell, sem checkout, reset, stash, commit nem troca de branch, sem driver de diff nem conversão de texto do repositório, sem o monitor de arquivos e sem travar o índice. Lê o diff contra `HEAD` (índice e pasta de trabalho juntos), os arquivos novos, os conflitos, e a diferença `base...head` de uma branch. A base é sempre explícita. |
| Parser e classificação | `Quality/Git/UnifiedDiffParser.cs`, `FileClassifier.cs` | Linhas por hunk com a numeração nova, o símbolo que o git mostra (o achado só o guarda quando parece a assinatura de uma função, porque o palpite do git erra com frequência), renomeados, apagados, binários. Gerado, de terceiros, arquivo de bloqueio e alteração enorme ficam na lista mas não são lidos linha a linha. |
| Regras | `Quality/ReviewRules.cs` | 15 regras `QR-*`, cada uma com categoria, gravidade, confiança, juiz, o esperado e a correção. É o embrião do modelo de regras da Q4. |
| Detectores | `Quality/ChangeDetectors.cs` | Só as linhas adicionadas e a lista de arquivos. Segredos (o mesmo detector do resto do motor), arquivo sensível, conflito de merge, erro engolido, `async void`, certificado desligado, SQL concatenado e execução dinâmica (hipóteses), teste apagado, removido em saldo ou desligado, dependência sem arquivo de bloqueio, arquivo do Harness alterado. |
| Arquitetura | `ArchitectureAnalyzer` (existente) | Os achados que tocam arquivos alterados entram como heurística: a análise lê o projeto inteiro e não separa o que existia antes. |
| Juízes | `Quality/ReviewJudges.cs` | Segurança, Arquitetura, Qualidade do código, Regressão e Entrega, determinísticos. Revisão por modelo, Requisitos e Verificações do projeto entram quando a pessoa os pede; Dependências aparece como `not_run`. Sem o que foi pedido, o juiz mostra o motivo e a revisão não passa limpa. |
| Modelo | `Quality/ModelReviewer.cs` | Duas perguntas ao provedor configurado (finalidade `judge`): defeitos no diff e critérios de aceite. O diff vai redigido, dentro de um bloco que o prompt manda tratar como dado; a resposta é JSON no contrato e só vale com arquivo, linha adicionada e trecho que existem no diff. O resto é descartado e contado. Registra modelo, versão, hash do que foi enviado e tokens. |
| Verificações | `Quality/QualityGates.cs` | Catálogo só de leitura (scripts `build`, `test`, `lint` e `typecheck` do package.json com o gerenciador do projeto; build e testes de uma solução .NET) e executor. Roda só o que o catálogo achou e a pessoa marcou e autorizou, só nas alterações locais. Passou, falhou, bloqueada (ferramenta ausente, nunca aprovação), estourou o prazo ou cancelada. Falha é achado confirmado e reprova. |
| Decisões | `Quality/ReviewDecisions.cs` | A pessoa confirma, descarta (falso positivo) ou aceita o risco de cada achado, e assina a entrega. Ver a seção 8. |
| Serviço e histórico | `Quality/CodeReviewService.cs`, `FileReviewStore.cs` | Monta o relatório, descarta o achado cuja linha mudou durante a revisão, deduplica, grava em `projects/<id>/reviews/` (100 por projeto) e audita. |
| Protocolo | `quality.review.start`, `.list`, `.detail`, `quality.gates.list`, `quality.finding.decide`, `quality.delivery.decide`, `quality.review.brief` | `start` sem `mode` é a revisão das alterações locais; `mode: "branch"` exige `base`. `useModel`, `requirements`, `gates` e `gatesApproved` são opcionais. |
| Boot | `quality.review_changes` (direta); `quality.review_branch`, `quality.run_quality_gates`, `quality.resolve_findings` e `quality.evaluate_delivery` (abrem a tela) | A primeira executa no nível de observação porque só lê. As outras abrem a tela porque pedem a base, rodam comandos do projeto ou registram a decisão da pessoa. |
| Interface | `/qualidade`, em Engenharia | Opções da revisão, veredito, achados com as decisões, a assinatura da entrega, o pedido de correção, verificações, critérios, uso do modelo, juízes, o que não foi verificado, arquivos e histórico. |

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

**Linha de base em alterações reais (2026-11-27).** A revisão rodou, uma alteração por vez (cada uma contra o commit pai),
sobre os 80 commits mais recentes de dois repositórios reais (um de desktop em JS/TS e este mesmo), sem erro em nenhuma, e de
novo sobre outros 80, reservados e lidos só depois dos ajustes. Os achados foram classificados primeiro por quem escreveu os
detectores e depois conferidos pelo dono; a lista com os arquivos ficou fora do repositório.

| | Antes da calibração | Depois da calibração, mesmas 80 | Reservadas, outras 80 |
| --- | --- | --- | --- |
| Reprovações | 5 de 80 (todas falsas) | 0 | 0 |
| Achados que pesam no veredito | 65 | 2 | 4 |
| Alterações com algum desses achados | 15 | 2 | 4 |

O que a medição mostrou, e o que mudou por causa dela: credencial de exemplo em arquivo de teste ou de dados de exemplo
(`fixtures/`, `datasets/`) reprovava, e agora é só recomendação (a credencial continua no relatório, sem pesar no veredito);
`t.skip(...)` dentro do corpo de um teste, que é pular por falta de recurso do ambiente, contava como teste desligado (7 falsos
positivos; agora só `it.skip`, `[Ignore]` e semelhantes); e achados de arquitetura do arquivo inteiro voltavam em toda alteração que o
tocasse (50 de 65), então só valem como problema da alteração quando ela acrescentou a importação, e o resto virou recomendação.
Uma segunda passada achou mais um caso, nas minhas próprias alterações: o gabarito de avaliação guarda uma chave de exemplo, e
isso reprovava a alteração (a regra de pasta de dados de exemplo veio daí).

**Conferência do dono** dos 9 achados distintos que sobravam antes de a credencial de teste virar recomendação: 5 eram
credenciais de exemplo em teste (ruído), 2 são importações reais entre módulos (o achado está certo; no repositório de desktop a
arquitetura estava sendo reorganizada) e 2 (saldo de casos de teste removidos) ainda não foram conferidos. Depois da
calibração, o que pesa no veredito nas duas rodadas são essas importações novas e os saldos de testes removidos; dos primeiros,
os 2 conferidos são úteis, e os saldos de testes seguem sem conferência.

**Não provado:** o recall. Nenhum dos 160 commits tinha um defeito conhecido, então não há como dizer quanto a revisão deixa
passar. A eval da seção 7 mede o conjunto escrito à mão e só serve contra regressão. O gate de aprovação continua sem meta até
haver alterações com defeito comprovado e uma conferência da pessoa.

## 7. Eval

`CodeReviewEvaluationTests` roda os detectores contra casos com gabarito em `datasets/code-review.json` (código correto,
um defeito por regra, falsos positivos conhecidos) e mede recall e falsos positivos. O gabarito foi escrito à mão; o
conjunto é pequeno e serve para impedir regressão. Antes de transformar uma meta em gate é preciso uma linha de base sobre
alterações reais, revisadas por uma pessoa.

## 8. A jornada de decisão

Demanda, alteração do agente, **revisão** (simples, com modelo, critérios e verificações do projeto), **decisão da pessoa**,
**pedido de correção**, nova revisão, **assinatura da entrega**. O motor decide o que prova; o modelo só propõe; a pessoa decide
o que fazer e assina.

- **Decisão por achado** (`quality.finding.decide`): confirmar, descartar como falso positivo ou aceitar o risco. Descartar e
  aceitar exigem justificativa (10 a 500 caracteres, redigida como o resto). Um achado crítico não se aceita como risco: corrige-se,
  ou se descarta dizendo por que o motor errou. Nada some: o relatório do motor não muda e a decisão aparece ao lado. A decisão vale
  para o mesmo achado (mesmo id) em revisões seguintes.
- **Estado da entrega**: o veredito do motor nunca muda. O que muda é o que ainda bloqueia (achado confirmado e grave sem
  descarte nem aceite) e se a revisão está pronta (sem bloqueio, nem inconclusiva, nem vazia).
- **Assinatura** (`quality.delivery.decide`): aprovar só abre quando está pronta, e exige justificativa. Um juiz que aprova
  nunca substitui essa assinatura. Aprovar e recusar ficam na trilha de auditoria, sem o texto da justificativa.
- **Pedido de correção** (`quality.review.brief`): devolve ao agente só o que está aberto, com as regras que ele não pode quebrar
  (não remover, desligar nem enfraquecer testes, verificações, regras ou segurança; não sair do escopo; não mexer no relatório;
  conferir hipóteses antes). Cada pedido registra "correção pedida" nos achados, e depois de três tentativas sobre o mesmo
  achado o motor para e devolve a decisão a uma pessoa.
- **Veredito com o que foi pedido**: verificação do projeto que não rodou deixa a revisão inconclusiva; modelo ou critérios que
  não rodaram a deixam, no máximo, aprovada com ressalvas. Falha de verificação é achado confirmado e reprova.

**Jornada de ponta a ponta** (`QualityJourneyTests`): git de verdade, o npm do projeto rodando de verdade, o protocolo que a
interface usa. Um agente entrega um desconto errado, com uma instrução injetada no código; a revisão (modelo, critérios e o teste
do projeto) reprova pela falha do teste; a aprovação é recusada; a justificativa curta é recusada; o pedido de correção sai;
o agente corrige; a revisão passa; a pessoa assina. A versão com modelo roteirizado roda sempre e confere os valores; as versões
ao vivo (OpenAI e Anthropic, por um gateway de teste) só rodam com a chave no terminal e conferem o que o modelo real não pode
mudar: a verificação reprova por si, o que o modelo aponta tem linha do diff, nenhum critério vira "atendido" sem evidência, e a
hipótese de modelo não bloqueia a aprovação.
