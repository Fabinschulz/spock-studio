# Context Engine e modelo canônico de contexto

> Parte do estudo [Engineering Intelligence Hub](README.md) (2026-11-16). Reúne Project Intelligence (Parte I), Context
> Engine (§5 do pedido), Knowledge Engine (Parte VI) e o que o pedido chamou de CONTEXT_RETRIEVAL_STRATEGY.
> ADR: [0002](../architecture/adr/0002-context-engine.md).

## 1. O que é, e o que não é

O Context Engine é **um serviço do motor .NET** que, para um projeto, uma intenção e um orçamento, devolve um pacote de
contexto ordenado, compacto e rastreável. Não é um novo runtime, não é um segundo índice e não usa modelo para decidir o
que entra.

Ele compõe o que já existe:

| Peça existente | Papel no Context Engine |
| --- | --- |
| `ProjectProfile` (Discovery) | Seção `profile` |
| `HarnessStatusService`, `mariliz.yaml`, regras | Seções `rules`, `harness` |
| `DecisionMemory` | Seção `decisions` |
| Índice FTS5 + símbolos (Python) | Seção `evidence` (trechos) |
| `ManagementService` | Seções `management.*` |
| `CloudReadService`, `IacOperationService` | Seções `infrastructure.*` |
| `ProjectTimeline` | Seção `changes` |
| `ContextReviewService` (contagem de tokens) | Orçamento |
| `ProjectBriefing` (MCP) | Primeira versão de uma composição; passa a ser um "perfil de intenção" do engine |

Hoje cada consumidor (workflow, investigação, assistente, MCP) monta seu contexto do seu jeito. O engine unifica a
montagem; cada consumidor continua dono da sua pergunta.

## 2. Modelo canônico

```text
ProjectContext (projectId, contextVersion)
├── identity        nome, stableId, repositórios, cópias de trabalho, remoto Git
├── profile         linguagens, frameworks, versões, comandos, CI              [descoberto]
├── architecture    camadas, módulos, direção de dependências, divergências     [descoberto | aprovado]
├── rules           regras de engenharia ativas, por caminho                    [declarado | aprovado]
├── harness         componentes, agentes, skills, hooks, estado por IDE          [declarado]
├── decisions       decisões técnicas e gerenciais                               [aprovado]
├── knowledge       fontes indexadas e seu frescor                               [descoberto | integração]
├── connections     conexões vinculadas, alcance, política por destino, frescor  [integração]
├── management      métricas, riscos, marcos, itens                              [integração + calculado]
├── infrastructure  conexões cloud liberadas, estado da IaC, drift               [integração + calculado]
├── delivery        tarefas, verificações, pendências                            [calculado]
└── policies        acesso, contextualização por destino, ferramentas liberadas  [declarado]
```

Cada item carrega **classificação de origem** — exigência explícita do pedido (§4.1):

| Classificação | Quem produz | Pode virar regra oficial? |
| --- | --- | --- |
| `discovered` | Detector determinístico do motor | Não sozinho; vira `approved` por plano aprovado |
| `declared` | A pessoa, em `mariliz.yaml` ou na tela | Sim (é a declaração) |
| `approved` | Plano ou decisão aprovada | Sim |
| `integration` | Sincronização de fonte externa | Não; é dado da fonte, com `nativeId` e `syncedAt` |
| `calculated` | Motor, por regra do catálogo | É número, não regra |
| `inferred` | Modelo de IA | **Nunca** sem aprovação; aparece sempre rotulado |
| `stale` | Qualquer uma, quando a fonte passou do prazo de frescor | Mantém a classificação original e ganha o aviso |

O `contextVersion` é o hash das seções **persistentes** (identity, rules, harness, policies, nomes das conexões). Muda
quando algo aprovado muda; não muda a cada sincronização.

## 3. Reconhecimento contínuo

| Mudança | Como detectar | O que acontece |
| --- | --- | --- |
| Arquivos e módulos novos | Hash por documento no índice (existe) | Reindexação incremental (existe) |
| Dependências ou stack | Manifestos (existe na descoberta) | Perfil marcado como velho; "Atualizar perfil" proposto (resolve C-12) |
| Arquitetura | Detector de camadas (P1-5) comparado à arquitetura aprovada | Divergência listada; nunca sobrescreve a aprovada |
| Convenções contra regras | Leitura de `tsconfig`, eslint, `.editorconfig` antes de afirmar padrão (C-24) | Regra genérica não contradiz a configuração real |
| Infraestrutura | Drift agendado (existe) | Entra em `infrastructure` |
| Integrações | Sincronização | `syncedAt` e frescor por fonte |

Gatilhos: abertura do projeto no SPOCK, aplicação de plano, sincronização, e a chamada de `spock_context_bootstrap`
(compara o hash dos manifestos, sem reindexar tudo).

## 4. Composição por intenção

O engine recebe `(projectId, intent, budgetTokens, focus?)`. `focus` é opcional: caminho de arquivo, chave de item, nome de
módulo.

| Intenção | Seções, em ordem de prioridade | Orçamento padrão |
| --- | --- | --- |
| `bootstrap` | identity, policies, connections (nome e frescor), harness (divergências), avisos | 1.500 |
| `architecture` (implementada, W1-3) | identity, stacks do perfil, avisos, references (id e uma linha de cada padrão da pasta [Organização](../organizacao/README.md) que vale para as stacks; os da stack antes dos gerais). Classificação `reference` | 1.500 |
| `development` | rules (do `focus`), architecture (módulo), decisions relacionadas, evidence (código e testes), critérios do item | 6.000 |
| `management` | management (métricas, riscos, marcos), itens filtrados, changes, limitações | 3.000 |
| `infrastructure` | infrastructure (conexões liberadas, IaC, último plano, drift), políticas de aprovação | 3.000 |
| `investigation` | evidence ampla, changes, decisions, management, infrastructure, sinais | 8.000 |
| `delivery` | critérios, escopo declarado, diff resumido, testes, validações | 4.000 |

Algoritmo, sem modelo:
1. resolve o projeto (falha segura);
2. aplica a política: remove seções e itens cujo destino não está liberado para o consumidor (`ide`, `workflow`,
   `assistant`);
3. junta candidatos de cada seção com seu custo em tokens (tiktoken, como `ContextReviewService`);
4. ordena por prioridade da intenção e, dentro da seção, por relevância (RRF para trechos, recência para mudanças,
   gravidade para riscos);
5. corta no orçamento por seção e no total; registra o que ficou de fora (`omitted[]` com motivo);
6. devolve itens com proveniência e `freshness`, mais `limitations[]`.

O consumidor decide o que fazer com `omitted[]`; o MCP devolve como dica ("há mais 12 trechos: refine a busca").

## 5. Proveniência e frescor

Todo item devolvido tem:

| Campo | Exemplo |
| --- | --- |
| `source` | `repo`, `jira`, `github`, `decision`, `spock-calc`, `cloud:aws`, `iac` |
| `ref` | `src/app/orders.ts#L40-88@sha256:…`, `PROJ-123`, `dec-0042`, `flow.leadtime` |
| `classification` | §2 |
| `observedAt` | Data da leitura (sincronização, indexação, cálculo) |
| `freshness` | `fresh`, `stale`, `unknown` |
| `limitations` | "GitHub não informa prazo por item" |

Prazos de frescor (configuráveis por projeto, com padrão): dados de gestão 24 h; perfil 7 dias ou mudança de manifesto;
índice: por hash do arquivo (o que já existe); cloud: no momento da leitura; drift: o intervalo agendado.

**Sincronização sob demanda antes de responder:** se uma ferramenta de gestão for chamada com a fonte `stale`, o engine
pode disparar uma sincronização incremental com teto (uma página, 5 s). Se não der, responde com os dados velhos marcados
e a data. Nunca apresenta dado velho como atual (cenário 9). Decisão do dono (DD-7): permitir essa sincronização
disparada pela IDE, ou só pela pessoa no desktop como hoje.

## 6. Recuperação (RAG)

Manter FTS5 + BM25 + símbolos + RRF. Nada justifica trocar de tecnologia antes de medir alternativas baratas.

| Problema medido | Proposta | Como provar |
| --- | --- | --- |
| Recall@5 0,25 em perguntas reais em português contra código em inglês | Dicionário pt↔en de termos de domínio gerado do próprio projeto (identificadores, títulos de docs, glossário) aplicado na consulta; reformulação (`rewrite-query`, propósito já existe) também no fluxo de engenharia | Recall@5, MRR e NDCG@10 no `spock-questions.json` ampliado |
| Arquivos acima de 24.000 caracteres indexados só no começo | Indexar o arquivo inteiro em trechos com âncora; marcar corte quando houver | Pergunta cuja resposta está no fim de arquivo longo |
| Projeto grande indexa em várias rodadas (C-08) | Orçamento de indexação separado do de execução; "Indexar tudo" em segundo plano; limite de arquivos em `mariliz.yaml` | Projeto de 2.400 arquivos pesquisável depois de uma ação |
| `indexed_at` não volta na busca | Devolver `indexedAt` e `classification` por trecho | Teste de contrato |
| Busca conceitual fraca | Embeddings (`sqlite-vec` já empacotado) **só** depois de as medidas acima estabilizarem e se o ganho medido justificar o tamanho e a latência (D3) | Mesmo dataset, mesma métrica, antes e depois |

Dataset de avaliação: ampliar `spock-questions.json` (12 perguntas) para três projetos reais de stacks diferentes, com
perguntas de arquitetura, de "onde fica" e de regra, escritas por quem não escreveu o código do índice. Métricas: Recall@5,
Precision@5, MRR, NDCG@10. Meta inicial: Recall@5 ≥ 0,6 no projeto do próprio SPOCK.

## 7. Conhecimento interno e externo

| Tipo | Prefixo no índice | Entra por padrão? | Vira regra? |
| --- | --- | --- | --- |
| Código e docs do projeto | caminho | Sim (com `include`/`exclude`) | Não |
| Decisões aprovadas | `@decision/` | Sim | É decisão |
| Dados de integração | `@external/` | Só com política `knowledge` liberada | Não |
| Referências externas (docs de bibliotecas, padrões da empresa) | `@reference/` (P2-7 da auditoria) | Só se declarada, com URL e data de captura | Não, sem decisão |
| Conteúdo produzido por IA | `@inferred/` | Não | Não, sem aprovação |
| Memória aprendida | — | Fora (decisão vigente) | — |

## 8. Onde o engine mora

No .NET (`Mariliz.Application/Context/`), ao lado de `ContextReviewService`. A busca continua no Python; o engine chama a
rota existente. Motivo: política, orçamento e proveniência são regras, e regras ficam no .NET (princípio 3 do discovery).
O Python recebe o contexto já composto quando o workflow precisa dele, como hoje.
