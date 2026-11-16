# Auditoria de engenharia de IA

> Parte da auditoria estratégica do SPOCK (2026-11-15). Navegação e método em [README.md](README.md).
> Somente leitura: nenhum código foi alterado. Legenda de estados: A implementado e validado, B parcial, C só documentado,
> D não implementado, E substituído por melhoria válida, F substituído sem justificativa, G regressão, H conflito entre
> documento e código, I decisão pendente, J não se aplica.

Auditor: domínio IA (orquestração, modelos, skills/MCP/plugins, contexto/memória/RAG, avaliação e observabilidade).
Escopo: só leitura. Branch `feat/conformidade-licencas` (limpa). Caminhos relativos à raiz do repositório.

## 0. O que foi executado

| Verificação | Resultado |
| --- | --- |
| `python -B -m unittest discover -s intelligence/tests -p "test_*.py"` (como a CI, offline, sem gravar bytecode) | **192 testes, OK** (55 s) |
| `python -B intelligence/evaluations/project_recall.py .` (índice em pasta temporária, só lê `git ls-files`) | 1.631 arquivos, 13.502 trechos, **recall@5 = 0,25, MRR = 0,111**, testShare@5 = 0 |
| Testes .NET (`dotnet test`) | **não executados** (gravariam `bin/obj`); evidência de testes .NET citada só pelo nome do arquivo, **não verificado** |
| Chamada a modelo real | não feita (regra da auditoria). A documentação do próprio projeto admite que nada com modelo foi validado com chave válida |

Observação: `docs/plataforma/MARILIZ_CURRENT_STATE.md:116` registra recall@5 = 0,33 para o mesmo script; hoje deu 0,25 (corpus cresceu para 1.631 arquivos). É regressão de qualidade ou deriva do corpus; nos dois casos o número publicado está desatualizado.

## 1. Resposta à pergunta central

**O SPOCK não coordena um processo de engenharia. Ele produz planos e análises sob controle forte, e delega toda a execução de código ao ambiente de IA da pessoa.**

- O grafo de engenharia (`intelligence/workflows/engineering/graph.py:123-151`) é um pipeline linear com saídas antecipadas: analisar, contexto, recuperar, planejar, validar plano, (arquitetura, checar impacto), juiz, aprovação, sintetizar. Não há agente de implementação, de testes nem de revisão de código, nem paralelismo nem subagentes.
- A **única ação com efeito** que o grafo pode propor é gravar um registro de decisão em `.mariliz/decisions/<executionId>-impacto.md` (`runtime/Mariliz.Application/Workflows/ActionGovernance.cs:16-23`, `ProjectActionExecutor.cs:18-27`). Nenhum código é gerado ou alterado (confirmado em `MARILIZ_CURRENT_STATE.md:111`).
- O achado anterior **continua verdadeiro**: o motor .NET não executa o Harness Engineer. O agente é um `.md` aplicado em `.claude/agents/harness-engineer.md` (`Harness/Catalog.cs:49-51`), roda no Claude Code da pessoa e grava `.mariliz/proposal.json`, que o motor lê como dado não confiável (`Harness/ProposalReader.cs:9-22`). Nenhum código do motor dispara esse agente (grep por `harness-engineer`/`PathProposal` só encontra catálogo, composição e leitura).
- **D-08 no código** = `AgentRunService` (`AiTools/AgentRunService.cs:15-68`) + `AiToolCatalog` (`AiTools/AiToolCatalog.cs:14-26`): o motor localiza o binário instalado (Claude Code, Cursor Agent, Codex, Gemini CLI), executa-o **só para leitura** na pasta autorizada (Claude Code com `--permission-mode plan --disallowedTools Edit,Write,NotebookEdit,Bash`), transmite a saída com segredos redigidos e registra no diário. É um "pergunte à sua CLI" com prompt livre de até 8.000 caracteres e 10 minutos. Consequência importante: como `Write` está proibido, **nem o Harness Engineer pode ser rodado por esse caminho** (ele precisa de `Write` para gravar a proposta). D-08 está "Adotada; confirmação pendente" (`docs/compliance/LICENSE_DECISIONS.md:15,48-55`) e nenhuma CLI estava instalada na validação real (`MARILIZ_CURRENT_STATE.md:98`).
- Fora do LangGraph há **outros dois laços de modelo**, no .NET: Da ideia ao projeto (`Ideas/IdeaArchitectService.cs:37-40`, gerar, conferir, tentar de novo até 2 vezes) e geração de IaC (`Iac/IacGenerationService.cs:53`). Ambos chamam `ModelGateway.ChatAsync` direto, sem o `ModelBroker` e sem o teto de tokens por execução.

Em resumo: é um **planejador governado + leitor de contexto (MCP) + porta para CLIs externas**, com governança, rastreabilidade e defesa contra injeção acima da média. Não é um orquestrador multiagente de engenharia.

## 2. Tabela de capacidades

Legenda: A implementado e validado, B parcial, C só documentado, D não implementado, E substituído por melhoria válida com evidência, F substituído sem justificativa, G regressão, H conflito entre documento e código, I decisão pendente, J não se aplica.

### 2.1 Orquestração de agentes

| ID | Capacidade | Origem / intenção | Estado | Evidência | Lacuna | Recomendação |
| --- | --- | --- | --- | --- | --- | --- |
| AI-01 | Grafo LangGraph de engenharia | ADR-0001 §Decisão 1; auditoria agentic §7.1 | **A** (com modelo falso) | `workflows/engineering/graph.py:123-151`; `tests/test_graph.py` (192 testes passam) | Linear; sem laço de replanejamento (`validate_plan` inválido vai para END, `graph.py:140-144`), embora a auditoria §7.1 previsse "plan de novo (até N vezes)" | Aceitar o linear ou implementar o laço de correção com o motivo da recusa |
| AI-02 | Grafo de investigação de gestão (com ciclo) | Fase G (`SPOCK_IMPLEMENTATION_ROADMAP.md` §Fase G) | **B** | `workflows/investigation/graph.py:96-120`: ciclo decide→retrieve/gather→hypothesize→decide, teto de 4 rodadas (`investigation/nodes.py:18,133`) | Sem checkpoint (`graph.py:1-3`); nunca rodado com modelo real (`SPOCK_IMPLEMENTATION_ROADMAP.md:946`) | Validar com chave real antes de anunciar |
| AI-03 | Agentes especializados (planning, implementation, review, tests, architecture, research) | Pedido do produto; auditoria §7.2 | **B** | Só `planning`, `architecture`, `judge` (`workflows/engineering/state.py:25-27`) e `investigation` no Python; `code-reviewer` e `verifier` são `.md` para o Claude Code (`Assets/agents/*.md`), não executados pelo SPOCK | Implementação, testes e revisão inexistentes como agentes do produto | Decidir explicitamente se o SPOCK quer executar ou só orquestrar a CLI |
| AI-04 | Subagentes / delegação | Pedido do produto | **D** | Nenhum nó cria subgrafo ou agente filho; delegação só para a CLI da pessoa, só leitura (`AgentRunService.cs`) | | Se for meta, modelar a CLI como nó do grafo com contrato de saída |
| AI-05 | Execução paralela, dependências entre tarefas | Pedido do produto | **D** | Todas as arestas são sequenciais (`graph.py:137-150`); `RunTracker.TryStart` permite **uma execução ativa por projeto**, de qualquer tipo (`Execution/RunTracker.cs:25-38`) | | Manter serialização por projeto como controle de concorrência; paralelismo só com caso de uso |
| AI-06 | Ciclos controlados | | **B** | Só na investigação, com teto de rodadas, etapas e tokens (`investigation/nodes.py:122-148`) | Engenharia não tem ciclo | |
| AI-07 | Retomada após queda | ADR-0001 §6; auditoria §1.2 | **B** | Só a **pausa de aprovação** sobrevive: `SqliteSaver` + `interrupt()` (`graph.py:154-172`, `memory/execution.py:37-49`), retomada pelo diário (`WorkflowService.cs:76-99`). Execução `running` vira `ENGINE_RESTARTED` (`Workflows/WorkflowRecovery.cs:11-28`) | `app.stream(..., durability="exit")` (`graph.py:159`) só grava checkpoint na saída ou na pausa; o comentário de `server.py:66` ("checkpoint consistente a cada passo") não bate com o código (**H** menor) | Corrigir o comentário; documentar que queda no meio perde a execução |
| AI-08 | Estado persistido | | **A** | Checkpoint por projeto em `<data>/projects/<id>/execution/checkpoint.sqlite`, apagado ao terminar (`memory/execution.py:24-68`); validação do checkpoint antes de retomar (`workflows/engineering/checkpoint.py:8-29`); `tests/test_memory_isolation.py` | | |
| AI-09 | Limite de contexto por agente | auditoria §7.2 | **A** | Saída: 1.024 tokens por agente (`agents/planning.py:9`, `architecture.py:8`, `judge.py:23`), 1.200 na investigação; entrada: 1.200 caracteres por trecho, 12.000 por bloco, 4.000 no pedido (`agents/prompts.py:21-23,45-66`) | Limites em caracteres, não em tokens | |
| AI-10 | Resultado parcial e degradação | | **A** | Toda falha de modelo cai no determinístico com limitação explícita (`workflows/engineering/budget.py:42-56`); `completed_with_limitations` na investigação (`investigation/graph.py:156-159`) | | |
| AI-11 | Consolidação | | **B** | `synthesizer` só conta etapas, stacks e trechos (`engineering/nodes.py:204-210`) | Não consolida opiniões de vários agentes | |
| AI-12 | Prevenção de conflito / concorrência | | **A** | Uma execução por projeto (`RunTracker.cs:29`); lock por arquivo SQLite (`knowledge/index.py:289-312`); escrita nunca sobrescreve conteúdo diferente (`ProjectActionExecutor.cs:30-34`) | Lock só entre threads do mesmo processo (o motor é único por pasta de dados, `WorkflowRecovery.cs:5`) | |
| AI-13 | Quality gates antes de concluir | auditoria §8 | **A** (determinístico) | `plan_validator` (`nodes.py:117-131`), `impact_ok`, `intents_ok`, `checkpoint_ok`; veredito sempre determinístico (`nodes.py:213-223`); aprovação ligada ao hash das ações (`WorkflowService.cs:156-181`) | O gate não verifica nada de código (não há código produzido) | |
| AI-14 | Harness Engineer dentro do produto (D5) | ADR-0001 tabela, D5 | **I** | D5 "Pendente" (`docs/architecture/adr/0001-langgraph-e-contratos-v2.md:133`) | O caminho D-08 não serve para ele (Write proibido) | Decidir D5; se ficar fora, documentar a jornada manual |
| AI-15 | Integração com CLIs de IA (D-08) | `LICENSE_DECISIONS.md` D-08 | **B** | `AiTools/AgentRunService.cs`, `AiToolCatalog.cs`; `runtime/tests/.../AgentRunTests.cs` (não executado); UI `desktop/ui/src/features/agents/agents-page.tsx` | Só leitura; não validado com CLI instalada; confirmação jurídica pendente (R-10, R-13); o roadmap ainda trata "segundo ambiente de IA" como P2 (`SPOCK_IMPLEMENTATION_ROADMAP.md` §5) enquanto o código já tem quatro CLIs (**H**) | Atualizar o roadmap; validar com Claude Code instalado |

### 2.2 Modelos

| ID | Capacidade | Origem | Estado | Evidência | Lacuna | Recomendação |
| --- | --- | --- | --- | --- | --- | --- |
| AI-16 | Modelo por finalidade ("mais capaz para planejar, especializado para implementar") | Pedido do produto; Fase F | **B** | Só três finalidades: `assistant`, `judge`, `workflow` (`Models/ModelPurposes.cs:50-59`). `plan`, `architecture`, `classify`, `rewrite-query` caem todas em `workflow` (`ModelPurposes.cs:64-69`) | Não dá para pôr um modelo forte no plano e outro barato no resto; sem metadados de capacidade, custo, latência ou disponibilidade; sem fallback entre provedores | Separar `plan` de `workflow`; registrar latência e falha por provedor; fallback opcional |
| AI-17 | Independência de fornecedor | ADR-0001 D4 | **E** (melhoria) com **H** | Três provedores fixos: OpenAI, Anthropic, Google (`Models/ModelProviders.cs:93-103`); ADR-0001 D4 ainda diz "só OpenAI" (`adr/0001...md:118,134`) | Lista fechada no código (modelos e endereços), sem modelo local (Ollama ou equivalente) | Atualizar a ADR; se houver exigência offline, avaliar provedor local na allowlist |
| AI-18 | Teto de tokens por finalidade | D4; entrega "teto por finalidade" (#89) | **H** | .NET aceita até 64.000 no fluxo e 120.000 no assistente (`Models/ModelBudgets.cs:16-21`); o Python corta em 20.000 e 40.000 sem avisar (`workflows/engineering/state.py:17`, `graph.py:80`; `investigation/graph.py:32,82`) | A pessoa configura 64.000 e o runtime falha com `LIMIT_TOKENS` aos 20.000 | Alinhar as faixas ou devolver a limitação explícita |
| AI-19 | Prazo por execução coerente | D4 | **B** | Fluxo com modelo: 60 s no total (`WorkflowRequests.cs:14`, `state.py:16`) para até 6 chamadas (3 agentes × 2 tentativas, `agents/runner.py:10`) de até 30 s cada (`ModelGateway.cs:23`) | Risco de `LIMIT_TIME` com modelo real lento | Medir com chave real e recalibrar |
| AI-20 | Contabilidade de tokens | D4 | **A** | Broker reserva o pior caso e acerta depois (`Models/ModelBroker.cs:104,121,189-234`); uso do proxy vale mais que o do runtime (`ModelBroker.cs:14-15`) | Ideias, IaC e assistente direto não passam pelo broker (`IdeaArchitectService.cs:39`, `IacGenerationService.cs:53`, `ManagementAssistant.cs:87`) | Passar todas as chamadas pelo mesmo teto |
| AI-21 | Custo em dinheiro | D4 | **I** | `costUsd` sempre 0 (`ModelBroker.cs:131`, `workflows/engineering/ack.py` usage) | Sem visão agregada de tokens (`MARILIZ_CURRENT_STATE.md` §6) | Decidir D4-custo; ao menos somar tokens por projeto |
| AI-22 | Finalidades declaradas sem uso | auditoria §7.2 | **H** | `classify` e `rewrite-query` aceitos (`agents/model_client.py:18`, `ModelBroker.cs:40`) mas nenhum agente os usa (grep); a descrição do teto cita "classificação" e "buscas reformuladas" (`ModelBudgets.cs:17`) | Texto promete o que não existe | Remover ou implementar |
| AI-23 | Validação com modelo real | Fase A, Fase G | **D** (não validado) | Validação real só testou chave inválida nos três provedores (`docs/validacao-real-2026-11-14/s9_model.py:1-11`); `SPOCK_IMPLEMENTATION_ROADMAP.md:946` | Planning, arquitetura, juiz, investigação, Ideias e IaC só rodaram com dublê | Prioridade 1 |

### 2.3 Skills, MCP, plugins, hooks, regras

| ID | Capacidade | Origem | Estado | Evidência | Lacuna | Recomendação |
| --- | --- | --- | --- | --- | --- | --- |
| AI-24 | Skills: descoberta, instalação, versão, validação | Fase 1b (`docs/phase-1b-configure-project.md`) | **B** | Uma skill própria (`Assets/skills/harness-contract/SKILL.md`) e a TLC `tlc-spec-driven` instalada pelo instalador oficial `@tech-leads-club/agent-skills`, versões testadas 3.3.0 e 4.0.0, arquivos obrigatórios conferidos (`Specification/SpecificationManifest.cs:5-40`); `SkillsCheck.cs`, `SpecificationCheck.cs` | Sem descoberta de catálogo externo, sem escopo nem aplicação por contexto feita pelo SPOCK (quem aplica é o Claude Code); "mais nova" é uma constante local (`SpecificationManifest.cs:19`) | |
| AI-25 | Superpowers | Pedido do produto | **D** | Nenhuma ocorrência em código ou docs (grep) | | Registrar como fora de escopo ou planejar |
| AI-26 | SPOCK como servidor MCP | Roadmap §5 (P2) | **A** (por stdio) com **H** | `Mcp/McpServer.cs` (JSON-RPC por stdio, protocolos até 2025-06-18, só ferramentas, `readOnlyHint`), `Runtime/McpEntry.cs:76`; ferramentas de contexto, cloud e IaC; auditoria por chamada com nome do cliente declarado (`Mcp/ProjectContextTools.cs:147,164,228`, `Audit/ProjectAudit.cs:355-370`); testes `McpServerTests.cs`, `McpScopeTests.cs` | Roadmap ainda o lista como item "depois deste ciclo" (**H**); a identidade do cliente é declarada, não verificada (`Mcp/McpTool.cs:8`) | Atualizar o roadmap |
| AI-27 | SPOCK como cliente MCP | auditoria §7.2 "Tools & MCP Agent" | **D** | Nenhum cliente MCP no código (grep `McpClient`, `ModelContextProtocol`) | Agentes LangGraph não usam ferramentas externas | |
| AI-28 | Inventário de MCP de terceiros | Fase E | **B** | `Engineering/McpInventory.cs:27-156`: transporte, programa ou host, variáveis, segredo literal, sem valores | Sem instalação, teste, grants, escopos ou auditoria de uso de MCP de terceiros (`MARILIZ_CURRENT_STATE.md:96`) | |
| AI-29 | Plugins | Pedido do produto | **D** | Não existe sistema de plugins (grep; só cache de plugins do Terraform em `Infrastructure/Iac/CliIacRuntime.cs:248`) | | |
| AI-30 | Hooks e política dos agentes | Fase E | **B** | Um hook PreToolUse (`Assets/hooks/mariliz-policy.py`), política lida do próprio script (`Engineering/AgentPolicy.cs:31-62`), limites honestos ("proteção, não barreira", `Assets/catalog/policy.json` limits) | Comparação por regex de texto; só para Claude Code | |
| AI-31 | Regras | Fase 1b | **B** | `Assets/rules/general.md`, `stack-dotnet.md`, `stack-javascript.md`, regras propostas pelo Harness Engineer | Só .NET e JS/TS | |

### 2.4 Contexto, memória e RAG

| ID | Capacidade | Origem | Estado | Evidência | Lacuna | Recomendação |
| --- | --- | --- | --- | --- | --- | --- |
| AI-32 | Indexação de docs e código | auditoria §6 | **A** | SQLite por projeto fora do repositório (`knowledge/index.py:257-266`), fontes decididas pelo .NET (`Workflows/KnowledgeSources.cs`) | Arquivo cortado em 24.000 caracteres (`KnowledgeSources.cs:14`, `index.py:241`) e no máximo 80 trechos por arquivo (`chunking.py:14`): o fim de arquivos grandes não é indexado, sem aviso por arquivo | Indexar o arquivo inteiro em trechos ou marcar o corte |
| AI-33 | Busca lexical + símbolos (híbrida sem vetores) | auditoria §6.5 | **A** sintético / **G** real | FTS5 BM25 + tabela de símbolos + RRF k=60 (`knowledge/retrieval.py:20-53`); recall sintético 1,0 (`auditoria-orquestracao-agentic.md` Fase 4); real **0,25** hoje vs 0,33 documentado | Vocabulário português contra identificadores em inglês | Medir semântica com o mesmo `project_recall.py` |
| AI-34 | Embeddings / semântica (D3) | ADR-0001 D3 | **I** | D3 pendente (`adr/0001...md:132`); `sqlite-vec 0.1.9` no lock e no pacote, sem uso (`requirements.lock:646`, `intelligence/packages/sqlite_vec`); limitação fixa `EMBEDDINGS_NOT_READY` (`engineering/nodes.py:83`) | Dependência nativa embarcada sem uso | Decidir D3 (ver §4) ou tirar do pacote |
| AI-35 | Chunking estrutural com âncora estável | auditoria §6.3 | **A** | Markdown por título, Python por AST, C#/Java/TS por declaração, JSON/YAML por chave, ids REQ/AC/ADR como símbolos (`knowledge/chunking.py:1-84`) | Regex, não Tree-sitter (decisão consciente) | |
| AI-36 | Metadados e rastreabilidade de fonte | auditoria §6.4 | **A** | `documentId`, `chunkId`, `filePath`, `hash`, `score`, `startLine`, `language`, `module`, `documentVersion` (`index.py:205-228`); evento `context.retrieved` por trecho (`engineering/nodes.py:69-77`) | | |
| AI-37 | Atualização incremental e invalidação | | **A** | Hash por documento, só reenvia o que mudou (`index.py:43-88`, `manifest`), remoção de órfãos, `SAVEPOINT` por documento, esquema versionado (`index.py:18,315-320`) | Indexação síncrona no início de cada execução | |
| AI-38 | Isolamento por projeto | Fase 5 | **A** | `projectId` validado por regex e caminho resolvido (`index.py:257-266`, `memory/execution.py:24-34`); `tests/test_memory_isolation.py` | | |
| AI-39 | Memória (working, execution, knowledge, history, evaluation) | auditoria §6.7 | **B** | Execução: checkpoint efêmero; conhecimento: índice; histórico: diário .NET; decisões como memória (`@decision/`, `Management/DecisionMemory.cs`) e itens sincronizados (`@external/`, `Integrations/ExternalKnowledge.cs`) | "Memória aprendida" fora de escopo por decisão (`SPOCK_IMPLEMENTATION_ROADMAP.md` §5) (**J** para memória aprendida) | |
| AI-40 | Cache de referências externas | Fase F/G | **B** | Itens de Jira/GitHub sincronizados entram no índice só com `feedsKnowledge` ligado (`MARILIZ_CURRENT_STATE.md:117`) | Sem cache de documentação externa (bibliotecas, APIs) | |
| AI-41 | Revisão de contexto antes de gastar | | **A** | `Context/ContextReviewService.cs:12-28` mostra o pedido como iria ao modelo, com tokens contados | | |
| AI-42 | Chroma (Docker ou embarcado) | sessão anterior; auditoria §6.1 | **E** | Rejeitado por puxar onnxruntime/numpy acima do orçamento (`auditoria-orquestracao-agentic.md:381`); trocado por FTS5 no mesmo SQLite | | Manter a decisão (ver §4) |

### 2.5 Avaliação, observabilidade, segurança

| ID | Capacidade | Origem | Estado | Evidência | Lacuna | Recomendação |
| --- | --- | --- | --- | --- | --- | --- |
| AI-43 | Datasets de eval (recall, baseline, gestão) | auditoria §8 | **A** sintético | `evaluations/datasets/{recall,baseline,baseline-model,management,spock-questions}.json`; `test_recall_eval.py`, `test_baseline.py`, `test_management_eval.py` | Datasets escritos junto do código: medem regressão, não generalização (a própria auditoria admite) | Ampliar `spock-questions` e um segundo projeto real |
| AI-44 | Juiz por modelo independente | auditoria §8, Fase 7 | **B** | Prompt próprio, só vê o resultado (`agents/judge.py:1-3,299-313`), finalidade própria (`ModelPurposes.cs:51`), conferido pelo proxy (`Workflows/JudgmentVerifier.cs:10-25`), nunca muda o veredito (`engineering/nodes.py:213-223`) | Não validado com modelo real; sem calibração | |
| AI-45 | Linha do tempo e trilha de auditoria | Fase G | **B** | `Timeline/ProjectTimeline.cs`, `Audit/ProjectAudit.cs`, eventos com `seq` | Sem "quem" (não há identidade); telas não conferidas no navegador (`SPOCK_IMPLEMENTATION_ROADMAP.md:946`) | |
| AI-46 | Idempotência | | **A** | `interrupt_id` determinístico (`nodes.py:246-247`); escrita com mesmo conteúdo é no-op (`ProjectActionExecutor.cs:30-34`) | | |
| AI-47 | Prevenção de alucinação (grounding) | | **A** (estrutural) | Evidência obrigatória e só de trechos recuperados (`contracts/engineering.py` `plan_from_model`, `impact_from_model`); assistente: cada afirmação cita fato e todo número aparece no fato (`Management/Assistant/AnswerVerifier.cs:9-13`) | Grounding verificado só por id e número, não por semântica | |
| AI-48 | Execução controlada de ferramentas | | **A** | O agente propõe intenção; o motor escolhe alvo, conteúdo, aprova por hash e executa (`ActionGovernance.cs:11-60`); a proposta não aceita hooks, permissões, comandos, MCP (`ProposalReader.cs:12`) | Shell arbitrário só na CLI externa, e ela roda em modo leitura | |
| AI-49 | Defesa contra injeção de prompt | | **A** | Blocos delimitados com neutralização do delimitador, fontes externas e decisões rotuladas (`agents/prompts.py:1-38`), saída só JSON validado, retentativa com motivo (`agents/runner.py:36-80`) | Defesa por instrução continua probabilística; mitigada porque nenhuma saída vira ação sem validação | |
| AI-50 | Aprovações | | **A** | `ApprovalGate`, `ApprovalBinding` ligado ao hash; retomada recusa ações não propostas (`graph.py:105-109`) | | |
| AI-51 | Redação de segredos e telemetria | | **A** | `SecretScanner.RedactLines` em CLI, MCP e gateway (`AgentRunService.cs:100`, `McpServer.cs:118`, `ModelGateway.cs:231-236`); LangSmith desligado (`server.py:17-20`) | | |

## 3. Crítica narrativa

**Coerência.** Dentro do recorte "planejar com evidência, sem efeito colateral", a arquitetura é coerente e bem defendida: o motor .NET é a autoridade (chave, teto, aprovação, diário, escrita), o Python só propõe, o proxy é a fonte da verdade do uso, e toda saída de modelo passa por contrato fechado com fallback determinístico. Os contratos v2 e o juiz verificado pelo proxy são exemplos de engenharia cuidadosa.

O problema é que essa coerência cobre **uma fatia pequena do que o produto promete**. A camada de IA está espalhada em quatro caminhos que não compartilham orquestração nem orçamento:

1. LangGraph no Python (engenharia e investigação), com broker, teto e diário;
2. laços de gerar-conferir-tentar no .NET (Ideias, IaC), com `ModelGateway` direto e sem teto por execução;
3. assistente gerencial e do portfólio, chamada direta;
4. CLIs externas em modo leitura (D-08), sem contrato de saída.

Mais o Harness Engineer, que roda fora de tudo isso, no Claude Code da pessoa. Não há um modelo único de "execução de agente" nem um lugar único que diga quanto um projeto gastou.

**Orquestração.** O grafo de engenharia ganhou LangGraph, checkpoint e streaming, mas continua uma sequência fixa de dez nós em que só três chamam modelo, e o que sai é um plano de três a cinco passos genéricos mais um registro de decisão. O ciclo, o paralelismo, a delegação e os agentes de implementação, teste e revisão pedidos não existem. Se a visão é "SPOCK coordena o processo", falta o elo principal: transformar o plano em tarefas executadas e verificadas, seja por agentes próprios, seja dirigindo a CLI da pessoa com um contrato de resultado (diff proposto, testes rodados, verificação). Hoje a CLI entra como texto livre e sai como texto livre.

**Modelos.** A seleção por finalidade é real, mas grossa (três finalidades). O pedido "modelo mais capaz para planejar" não é atendido: plano, arquitetura e classificação compartilham `workflow`. Os tetos configuráveis na tela vão além do que o runtime Python aceita (64k contra 20k, 120k contra 40k), o que vira falha silenciosa. Duas finalidades do contrato (`classify`, `rewrite-query`) não têm agente. Nada disso foi exercitado com chave válida: é o maior risco de produto.

**RAG.** A escolha de FTS5 + símbolos + RRF dentro do SQLite é tecnicamente certa para um desktop: zero processo novo, zero dependência pesada, incremental, isolado, rastreável. A fraqueza é de qualidade no mundo real: recall@5 de 0,25 nas perguntas reais sobre o próprio Spock, piorando com o corpus. O laço agentic (reformular a busca) existe só na investigação, não no fluxo de engenharia. O `sqlite-vec` viaja no pacote sem uso há duas semanas de decisão pendente.

**Documentação.** O `MARILIZ_CURRENT_STATE.md` é honesto e bate com o código na maioria dos pontos. Os conflitos estão nos documentos de decisão mais antigos (ADR-0001 D4 "só OpenAI", roadmap com MCP do Spock e segundo ambiente de IA como itens futuros) e em textos de código (descrição do teto, comentário de checkpoint).

## 4. RAG: comparação de tecnologias para um produto desktop

Contexto do SPOCK: Tauri + motor .NET + CPython embarcado com orçamento de 30 MB para dependências (ADR-0001 §Decisão 3), um índice por projeto, corpus de 1 a 15 mil trechos, sem servidor, Windows e Linux.

| Opção | Execução | Peso na distribuição | Lexical | Vetorial | Incremental / isolamento | Riscos | Veredito |
| --- | --- | --- | --- | --- | --- | --- | --- |
| **SQLite FTS5 / BM25 (atual)** | No processo, `sqlite3` da stdlib | Zero (já vem no CPython) | Sim, BM25 com pesos por coluna | Não | Um arquivo por projeto, transações, já testado | Sem semântica: recall 0,25 em perguntas reais | **Manter como base** |
| **sqlite-vec** | Extensão carregável no mesmo arquivo | Pequena; **já está no pacote** (0.1.9, via `langgraph-checkpoint-sqlite`) | Combina com FTS5 na mesma consulta | Força bruta (KNN exato), adequada a dezenas de milhares de vetores | Mesmo arquivo, mesmo isolamento | Projeto de um mantenedor; última estável 0.1.9 em 2026-03-31, só alphas depois ([ecosyste.ms](https://packages.ecosyste.ms/api/v1/registries/npmjs.org/packages/sqlite-vec), [piwheels](https://www.piwheels.org/project/sqlite-vec/)); o `sqlite3` do CPython embarcado precisa permitir `enable_load_extension` (**não verificado** no embed do Windows) | **Recomendada para D3**, atrás de uma interface e medida por `project_recall.py` |
| Chroma embarcado | No processo | Pesado: onnxruntime e numpy obrigatórios ([depscope](https://depscope.dev/pkg/pypi/chromadb), [pypistats](https://pypistats.org/packages/chromadb)) | Limitado | Sim (HNSW) | Diretório próprio, migrações próprias | Passa do orçamento (rejeitado antes, `auditoria-orquestracao-agentic.md:381`); aviso de CVE crítico em 2026 citado pelo depscope (**não verificado** na fonte primária) | Não |
| Chroma servidor / Docker | Processo ou contêiner separado | Exige Docker ou binário extra por SO | Limitado | Sim | Ciclo de vida a mais para o Tauri | Usuário de desktop não tem Docker por padrão; mais uma superfície de rede | Não |
| LanceDB | No processo | pyarrow (≥16) e numpy obrigatórios ([CERN mirror](https://simple-repository.app.cern.ch/project/lancedb), [AUR](https://aur.archlinux.org/packages/python-lancedb)) | FTS próprio | Sim (IVF-PQ, colunar) | Diretório por tabela | pyarrow sozinho estoura o orçamento; relatos de falha sem AVX2 (fonte única, **não verificado**) | Não, salvo se o motor .NET usasse o SDK nativo |
| pgvector | Requer PostgreSQL | Servidor inteiro | `tsvector` | Sim | Bom, mas servidor | Inviável num desktop local | Não |

**Origem dos vetores (a parte difícil da D3).** O banco é o problema menor; o custo está em gerar embeddings:

- via provedor (proxy do motor): manda código para fora e exige consentimento por projeto (`auditoria-orquestracao-agentic.md:570`);
- modelo local ONNX: traz de volta o onnxruntime que derrubou o Chroma.

Uma alternativa barata, que ataca a causa medida (pergunta em português contra identificadores em inglês) sem vetores, é um **dicionário de sinônimos ou tradução de termos** e a **reformulação da consulta pelo modelo** (o laço que já existe na investigação) também no fluxo de engenharia. Recomendação: medir primeiro essas duas no `project_recall.py`; só então decidir D3 com sqlite-vec mais embeddings do provedor sob consentimento.

## 5. Principais riscos

1. **Nada com modelo foi validado com chave real** (AI-23). Planning, arquitetura, juiz, investigação, Ideias e IaC só rodaram com dublê. Prompts, prazos (60 s para até 6 chamadas de 30 s, AI-19) e parsers podem falhar no primeiro uso real.
2. **Expectativa contra entrega**: o produto se apresenta como plataforma de engenharia assistida por agentes, mas não implementa, não testa, não revisa; a execução real fica com a CLI externa em modo leitura ou com a pessoa (seção 1).
3. **Tetos inconsistentes** (AI-18): a tela aceita até 64k/120k tokens; o Python corta em 20k/40k e falha com `LIMIT_TOKENS`. Ideias, IaC e assistente gastam fora do broker (AI-20).
4. **Qualidade do RAG no mundo real** (AI-33): recall@5 de 0,25 e em queda; o documento ainda diz 0,33. Arquivos acima de 24.000 caracteres são indexados só no começo (AI-32).
5. **Dependência e decisões penduradas**: D3 (embeddings), D5 (Harness Engineer no produto), confirmação jurídica da D-08 e custo em dinheiro (D4). O `sqlite-vec` é embarcado sem uso.
6. **Fragmentação**: quatro caminhos de execução de IA sem orçamento, diário e contrato comuns (seção 3). Cada caminho novo repete a governança à mão.
7. **Documentos de decisão desatualizados** (AI-07, AI-15, AI-17, AI-22, AI-26): ADR-0001 D4, roadmap §5, descrição do teto e comentário do checkpoint.

## 6. Não verificado

- Testes .NET citados (`AgentRunTests`, `ModelBrokerTests`, `WorkflowRuntimeTests`, `McpServerTests` e outros): só pelos nomes dos arquivos.
- Se o `sqlite3` do CPython embarcado no Windows permite carregar extensões (pré-requisito do sqlite-vec).
- Telas `/agentes`, catálogo, linha do tempo e auditoria: não renderizadas.
- Tamanhos atuais das rodas de onnxruntime, pyarrow e chromadb: as fontes consultadas não davam o número.
