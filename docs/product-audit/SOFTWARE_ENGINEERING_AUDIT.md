# Auditoria de engenharia de software e ciclo do Harness

> Parte da auditoria estratégica do SPOCK (2026-11-15). Navegação e método em [README.md](README.md).
> Somente leitura: nenhum código foi alterado. Legenda de estados: A implementado e validado, B parcial, C só documentado,
> D não implementado, E substituído por melhoria válida, F substituído sem justificativa, G regressão, H conflito entre
> documento e código, I decisão pendente, J não se aplica.

**Data:** 2026-11-15. **Branch lida:** `feat/conformidade-licencas` (limpa, `06443a5`). **Modo:** somente leitura (código,
testes e documentos versionados). Nenhum teste foi executado nesta auditoria; "testes" abaixo cita as suítes que existem,
não o resultado delas. O que não foi conferido está marcado como **não verificado**.

Legenda: **A** implementado e validado · **B** parcial · **C** só documentado (ou só instalado como texto para um agente
externo) · **D** não implementado · **E** substituído por melhoria válida, com evidência · **F** substituído sem
justificativa · **G** regressão · **H** conflito documento x código · **I** decisão pendente · **J** não se aplica.

Coluna "Quem faz": **SPOCK** (o motor .NET/Python faz) · **Agente** (delegado ao agente de IA externo que o Harness
configura, hoje só Claude Code) · **Doc** (só descrito em documento ou regra-texto) · **Ninguém**.

---

## 1. Resumo executivo

1. O SPOCK é forte em **descobrir** (stack, versões, comandos, Harness existente), **configurar o Harness** com plano, diff,
   aprovação, diário e reversão, e em **ler** Jira/GitHub para gestão determinística. Esses pontos têm testes e, em boa
   parte, validação real.
2. O ciclo de entrega pedido (demanda → … → homologação) **para em "planejar"**. Depois disso, implementação, testes,
   revisão, atualização de status e entrega ficam fora do SPOCK por decisão de produto ("a IA propõe, o motor decide",
   `docs/plataforma/MARILIZ_CURRENT_STATE.md:285`). A delegação ao agente externo é **passiva**: o SPOCK instala regras e
   agentes no `.claude/`, mas não entrega uma tarefa estruturada ao agente nem recebe nada de volta.
3. **Não existe detecção de pendências de entrega** (escopo x diff, critérios de aceite x evidência). O SPOCK não lê
   `git diff`/commits/PRs com conteúdo nem `.specs/features/*/validation.md`, que o próprio agente `verifier` instalado
   grava.
4. As integrações são **só leitura** e **rasas para engenharia**: Jira sem descrição, critérios de aceite, comentários e
   sprints; GitHub sem branches, commits, reviews, comentários e checks. O ciclo completo do Jira nunca rodou contra um
   site real.
5. A separação **conexão / vínculo com projeto / recorte / política de contextualização** está bem implementada e testada.
   A granularidade de permissão por agente, porém, é um booleano único por vínculo.
6. Stacks: detecção boa (.NET, Node/React/Angular/Next, Flutter, Python, Rust, Go, Java). Regras de Harness só para .NET e
   um JS/TS genérico; React e Angular dividem a mesma regra; não há skills por stack; arquitetura do código existente não
   é reconhecida.
7. Projetos de referência externos (EmpregaNet, `harness-engineering`) entraram como **inspiração e templates embutidos**,
   não como conhecimento consultável. Não há mecanismo de base de referência local em Markdown.

---

## 2. Capacidades de Engenharia de Software (SE)

| ID | Capacidade | Class. | Quem faz | Evidência (arquivo:linha) | Testes |
| --- | --- | --- | --- | --- | --- |
| SE-01 | Criar novos projetos | **B** | SPOCK (documentos) | "Da ideia ao projeto": `idea.draft/plan/save` gera especificação (REQ-xx/NFR-xx), arquitetura (CMP-xx) e backlog (EPIC/US com critérios) em `.mariliz/arquitetura/` (`Ideas/IdeaDocuments.cs:10-13`, `Ideas/IdeaPrompts.cs:22,30,37-42`); a seção "Stack" aprovada vira regra do Harness (`Harness/ArchitectureDocuments.cs:19-31,50-58`). **Não gera código nem scaffold**: a escrita é limitada a `.claude/`, `.mariliz/`, `mariliz.yaml`, `mariliz.lock` (`Mariliz.Core/Policy/ManagedScope.cs:12-13,20-25`). Nunca rodou com modelo real (`MARILIZ_CURRENT_STATE.md:82,118`). | `IdeaArchitectTests`, `HarnessArchitectureTests`; `IdeaLiveTests` nunca rodado |
| SE-02 | Analisar projetos existentes | **A** | SPOCK | `Discovery/ProjectDiscoverer.cs:13-58` (stacks, ferramentas, ambientes de IA, inventário do Harness, especificação, git, ferramentas da máquina), perfil persistido com histórico (`Engineering/TechnologyProfileService.cs`). Validado no próprio Spock e num projeto TS (`MARILIZ_CURRENT_STATE.md:79`). | `DiscoveryTests`, `LanguageDiscoveryTests`, `TechnologyProfileTests` |
| SE-03 | Reconhecer linguagens, frameworks e versões | **A** (linguagens/versões) / **B** (frameworks) | SPOCK | Versão com evidência e confiança (instalada > faixa declarada, `Discovery/NodeDetector.cs:44-60`). Frameworks por catálogo para Node, Python, Rust, Go, Java (`Discovery/Frameworks.cs:21-78`), **sem catálogo .NET** (EF Core, MediatR etc. não aparecem; o .NET só anota ASP.NET Core e projetos de teste, `Discovery/DotnetDetector.cs:37-53`). Um `package.json` vira um único id (Next > Angular > React > Node, `NodeDetector.cs:31-35`). Condição morta em `NodeDetector.cs:69` (`id is "nextjs" or "angular" && … && id == "nextjs"`). | idem |
| SE-04 | Identificar a arquitetura do código existente (camadas, Clean, DDD, hexagonal) | **D** | Ninguém (Agente, por instrução) | Nenhum detector de arquitetura no `Discovery/`. A única "arquitetura" lida é a seção `## Stack` de `.mariliz/arquitetura/arquitetura.md` (`ArchitectureDocuments.cs:60-83`). | n/a |
| SE-05 | Mapear responsabilidades de módulos | **D** (existente) / **B** (novo) | SPOCK só no projeto novo | Componentes com `responsibility` só na arquitetura gerada pela fase A (`IdeaPrompts.cs:30`). Nada para o código existente. | `IdeaArchitectTests` |
| SE-06 | Detectar code smells | **D** | Ninguém | `Security/` varre credenciais, arquivos sensíveis fora do `.gitignore` e permissões de agentes (`MARILIZ_CURRENT_STATE.md:83`); não há análise de qualidade de código. | n/a |
| SE-07 | Refatoração por critérios | **D** | Ninguém | O motor não escreve fora do escopo gerenciado (`ManagedScope.cs:20-25`); não há fluxo de refatoração nem para o agente. | n/a |
| SE-08 | Aplicar Clean Architecture/DDD/SOLID | **C** | Doc → Agente | Só como regra-texto instalada no projeto: "one reason to change… dependencies point inward" (`Assets/rules/general.md:16`), direção de dependência no .NET (`Assets/rules/stack-dotnet.md:12`). Ninguém confere se foi seguida. | `HarnessRulesTests` (conteúdo da regra) |
| SE-09 | Planejar features | **B** | SPOCK | Workflow `engineering.request`: agente de planejamento com 2 a 8 etapas citando trechos (`intelligence/agents/planning.py:11-18`); sem modelo, plano determinístico genérico ("Descrever o pedido", "Considerar a stack"…, `intelligence/workflows/engineering/nodes.py:96-104`). Com modelo: só dublê. | `test_graph.py`, `test_agents.py`, `WorkflowModelTests` |
| SE-10 | Executar (implementar) features | **D** (por decisão) / **I** | Agente, sem orquestração | Escrita do motor limitada ao Harness; `agent.run` executa CLIs **só leitura** (`AiTools/AiToolCatalog.cs:7-8,17-23`). A fronteira é decisão registrada (`MARILIZ_CURRENT_STATE.md:102,285`). Se o SPOCK deve orquestrar agentes com escrita é decisão pendente. | `AgentRunTests`, `AiToolTests` |
| SE-11 | Corrigir bugs | **D** | Agente, sem orquestração | Sem fluxo de bug; o agente `debug-specialist` citado como núcleo no discovery (`docs/product-discovery.md:130`) não foi entregue (o MVP limitou o núcleo a `verifier`, `code-reviewer` e contrato, `product-discovery.md:305`). | n/a |
| SE-12 | Análise de impacto | **B** | SPOCK | Agente de arquitetura com áreas, riscos e ids de evidência (`intelligence/agents/architecture.py`, `nodes.py:134-168`); contrato validado (`nodes.py:171-177`). Fallback determinístico usa `AREA_BY_STACK` com chaves `javascript`/`typescript` que a descoberta nunca emite (emite `react`, `angular`, `nextjs`, `python`…), então React/Angular/Next/Python caem em "projeto" (`workflows/engineering/state.py:18-24` x `WorkflowContextBuilder.cs:73`); o registro gravado já esconde áreas sem modelo (`Workflows/DecisionRecordRenderer.cs:19`, correção do M4). Sem grafo de dependências/chamadas; recuperação lexical com recall@5 0,33 (`MARILIZ_CURRENT_STATE.md:114`). | `test_graph.py`, `ActionGovernanceTests` |
| SE-13 | Rodar e validar testes | **D** | Agente, por instrução | Comandos de build/teste/lint/typecheck são detectados (`NodeDetector.cs:108-110`, `LanguageDetectors.cs:247`) e escritos na regra `mariliz-repo.md` (`HarnessComposition.cs` `AddRepositoryCommands`); a regra pede ao agente que os rode (`general.md:48`). O motor nunca executa testes do projeto: as evals do Harness conferem a configuração, não o código (`Validation/Checks/RequirementsCheck.cs:29-40`, `RegressionCheck.cs:15-21`). O terminal integrado permite rodar à mão. | n/a |
| SE-14 | Code review | **C** | Agente | Agente `code-reviewer` só leitura instalado (`Assets/agents/code-reviewer.md:4`). O SPOCK não invoca revisão de diff; o juiz por modelo avalia o **plano** (`plan_quality`, `context_fit`, `request_fit`, `intelligence/agents/judge.py:33-34`), não código. | `HarnessCatalogTests` (instalação) |
| SE-15 | Git | **B** (mínimo) | SPOCK | Só `rev-parse` e `status --porcelain` (`Discovery/SystemProbe.cs:36,40`) e `check-ignore` na IaC (`Iac/IacStateService.cs:201`). Sem branch, commit, diff, log ou blame. | `DiscoveryTests` |
| SE-16 | PR review | **D** | Ninguém | PR entra só como item de trabalho (título, estado, datas, rótulos, marco), sem diff, reviews, comentários ou checks (`Integrations/Connectors/GitHubConnector.cs:57,71-95`). | n/a |
| SE-17 | Evidência de implementação | **B** | SPOCK | Evidências de recuperação (chunk, hash, pontuação), diário/eventos, ligação manual execução↔item (`Execution/RunLinkService.cs:31-66`), registro de decisão em `.mariliz/decisions/` (`Workflows/ActionGovernance.cs:23,29`). Não há evidência de **implementação** (commit, diff, resultado de teste) ligada à demanda. | `RunLinkTests`, `ActionGovernanceEndToEndTests` |
| SE-18 | Documentação técnica | **B** | SPOCK | Documentos da fase A; registro de impacto; relatório de gestão exportado em Markdown pela UI. Não documenta o código existente (README, ADR, diagramas). | `IdeaArchitectTests`, `ReportTests` |
| SE-19 | Modernização de legado | **C** | Doc | Cartão "Modernizar sistema" marcado "Em breve" (`desktop/ui/src/features/home/journey-grid/journeys.ts:45-50`); fora do MVP (`product-discovery.md:303`). | n/a |
| SE-20 | Respeitar convenções existentes antes de propor mudanças | **B** | SPOCK (Harness) + Agente (regra) | Harness preserva arquivos da pessoa: conflito explícito, `mariliz.yaml` dela, `CLAUDE.md` inventariado (`Discovery/HarnessInventoryReader.cs:21`), gerenciador de pacotes conflitante vira aviso. Regras mandam a decisão registrada vencer (`general.md:4`) e respeitar as camadas existentes (`stack-javascript.md:13`). Convenções de código (lint, nomes, estrutura de pastas, `.editorconfig`) não são detectadas. | `ConfigureJourneyTests`, `DiscoveryTests` |

### 2.1 O que o SPOCK faz x delega x só documenta

| SPOCK faz | Delega ao agente externo (passivamente, via `.claude/`) | Só documenta / ninguém |
| --- | --- | --- |
| Descoberta e perfil tecnológico; plano/aplicação/reversão do Harness; evals do Harness; segurança de segredos; plano e impacto do pedido (com ou sem modelo); especificação/arquitetura/backlog de projeto novo; registro de decisão; leitura e gestão determinística de Jira/GitHub; MCP de contexto para a IDE; execução **só leitura** de CLIs de IA | Implementar, corrigir bug, rodar testes, revisar código (`code-reviewer`), verificar critérios de aceite (`verifier`, que grava `.specs/features/<f>/validation.md`, `Assets/agents/verifier.md:3-4`), seguir o ciclo TLC (`general.md:9`) | Arquitetura do código existente, code smells, refatoração, revisão de PR, modernização de legado, atualização de status no Jira/GitHub, homologação |

---

## 3. Ciclo do Harness / entrega (HC)

| ID | Etapa | Class. | Onde está (ou falta) |
| --- | --- | --- | --- |
| HC-01 | Entrada da demanda | **B** | Texto livre até 4.000 caracteres no Pedido (`Workflows/WorkflowInput.cs:9-31`); `input` só aceita `goal`, `impact`, `requiresApproval`, `useModel` (`WorkflowInput.cs:10,27`). Não aceita issue do Jira/GitHub como entrada; a ligação com o item é manual e posterior (`run.link`, `RunLinkService.cs:31-66`). |
| HC-02 | Contexto | **B** | RAG lexical + símbolos, decisões e dados externos sob política (`nodes.py:50-79`, `Workflows/KnowledgeSources.cs:56-71`); revisão do contexto com tokens (`Context/ContextReviewService.cs`). Recall@5 0,33; laço agentic só na investigação (`MARILIZ_CURRENT_STATE.md:114-116`). |
| HC-03 | Requisitos | **B** | Só na fase A (REQ/NFR com ids, `Ideas/IdeaChecks.cs:33-34`). O pedido de engenharia não extrai requisitos. |
| HC-04 | Classificação (bug/fix/feature) | **D** | `request_analyzer` só confere se o objetivo está vazio (`nodes.py:39-42`). O tipo do item (`issuetype` do Jira) é gravado (`JiraConnector.cs:107`) e nunca usado: nenhum uso de `Type` em `Management/` (busca sem resultado). |
| HC-05 | Planejamento | **B** | `planning_agent` (`nodes.py:96-114`), validado por contrato (`nodes.py:117-131`). |
| HC-06 | Decomposição | **B** | Fase A: épicos e histórias com estimativa P/M/G, cobertura de requisitos (`IdeaChecks.cs:64-86`). Pedido: etapas sem dependência, responsável ou estimativa. |
| HC-07 | Critérios de aceite | **B** | Fase A exige um ou mais por história e grava como checklist (`IdeaChecks.cs:72,83`, `IdeaDocuments.cs:67`). Pedido de engenharia não tem. Critérios do Jira não são lidos (INT-07). |
| HC-08 | Delegação | **C** | O Harness instala agentes/regras (`HarnessComposition.cs` `AddCoreComponents`); `agent.run` manda um texto livre a uma CLI só leitura (`AiTools/AgentRunService.cs:29-49`). Não há handoff estruturado (id da demanda, plano aprovado, critérios, escopo de arquivos) nem retorno do agente para o SPOCK. |
| HC-09 | Implementação | **D** (decisão) | Bloqueada por `ManagedScope.IsWritable` (`ManagedScope.cs:20-25`); única ação com efeito é o registro de decisão (`Workflows/ProjectActionExecutor.cs:18,25-27`). |
| HC-10 | Testes | **D** | Ver SE-13. |
| HC-11 | Revisão | **C** | Ver SE-14. |
| HC-12 | Consolidação de evidências | **B** | Diário, eventos, evidências de recuperação, auditoria unificada (`Audit/ProjectAudit.cs`), execuções ligadas a itens. Nada do que o agente externo entregou entra aqui. |
| HC-13 | Documentação | **B** | `.mariliz/decisions/ex-*-impacto.md` (`ActionGovernance.cs:23`), documentos da fase A. |
| HC-14 | Atualização de status | **D** | Transporte só GET (`Integrations/Interfaces/IConnectorTransport.cs:6`, `Connectors/ConnectorClient.cs:14`); escrita em provedor é decisão "só leitura" (`MARILIZ_CURRENT_STATE.md:154`). |
| HC-15 | Entrega / homologação | **D** | Sem PR, release ou aceite. "Entregue" na gestão é deduzido do provedor (`Management/DeliveryRules.cs`). |
| HC-16 | **Detectar pendências deixadas pelo agente** (escopo x diff; critérios x evidência) | **D** | Nenhum código compara backlog/critérios com o que mudou. O SPOCK não lê `.specs/` (única referência: `Engineering/HarnessCatalog.cs:69`, rótulo de catálogo) nem diffs/commits. O único "drift" detectado é o dos arquivos do próprio Harness (`Engineering/HarnessStatusService.cs`), não da entrega do agente. Peças reaproveitáveis: `UnifiedDiff`, hash por arquivo, ids REQ/US com critérios, `run.link`. |

**Leitura crítica do ciclo.** As etapas 1 a 7 existem em dois fluxos que não conversam: a fase A (projeto novo: requisitos,
decomposição e critérios bem estruturados) e o Pedido de engenharia (projeto existente: só objetivo, plano curto e
impacto). O backlog da fase A não alimenta pedidos, não vira issues (o SPOCK não escreve no Jira) e não é conferido
depois. Da etapa 8 em diante o ciclo é inteiramente do agente externo, guiado por regras-texto, sem retorno ao SPOCK.

---

## 4. Integrações

Movido para [INTEGRATIONS_AUDIT.md](INTEGRATIONS_AUDIT.md).

## 5. Stacks, skills, regras e referências externas (ST)

| ID | Item | Class. | Evidência |
| --- | --- | --- | --- |
| ST-01 | React | **B** | Detectado (`NodeDetector.cs:34`); regra JS/TS genérica compartilhada (`HarnessComposition.cs:14,193`); sem regra ou skill React. |
| ST-02 | Angular | **B** | Detectado (`NodeDetector.cs:33`); mesma regra JS/TS, com globs só `ts/tsx/js/jsx` (templates `.html` e estilos fora, `HarnessComposition.cs:193`); sem regra Angular. |
| ST-03 | .NET | **B+** | Detectado com TFM e solução (`DotnetDetector.cs:30-58`); regra própria (`Assets/rules/stack-dotnet.md`). Uma única entrada para o repositório inteiro, enraizada na primeira solução (`DotnetDetector.cs:57-58`); sem catálogo de frameworks .NET. |
| ST-04 | Monorepo | **B** | Vários `package.json` até profundidade 3 viram stacks com raiz própria (`NodeDetector.cs:14`); varredura até profundidade 6 (`ProjectDiscoverer.cs:16`). Sem detecção de `pnpm-workspace`, Nx, Turborepo, Lerna (busca sem resultado em `Discovery/`); regras com globs globais, não por pacote; nenhum teste de monorepo em `DiscoveryTests`/`LanguageDiscoveryTests`. |
| ST-05 | Python, Rust, Go, Java, Flutter | **A** (detecção) / **D** (regras) | Detectados com frameworks (`LanguageDetectors.cs`, teste `O_proprio_spock_mostra_python_langgraph_rust_e_tauri`); só limitação `NoRuleProfile` (`HarnessComposition.cs:197-204`). |
| ST-06 | Skills por stack | **D** | Só `harness-contract` e a estrutura de especificação de terceiros (`Assets/skills/`, `Core/Policy/ManagedScope.cs:6`). Sem autoria de skills (`MARILIZ_CURRENT_STATE.md:93`). |
| ST-07 | Projetos de referência externos (EmpregaNet, `harness-engineering`) | **E** (como inspiração) | Usados como referência de desenho e templates adaptados e embutidos: camadas do backend seguem o EmpregaNet (`docs/architecture/backend-architecture.md:4`); `code-reviewer` e contrato adaptados de `harness-engineering`, MIT (`Assets/agents/code-reviewer.md:10`, `Assets/catalog/components.json:18,34`, `Assets/skills/harness-contract/SKILL.md:8`). A titularidade do EmpregaNet aparece como questão aberta (`docs/implementation-roadmap.md:286`). Não são conhecimento consultável em tempo de execução. |
| ST-08 | Base local de conhecimento de referência (Markdown) para evitar consultas repetidas ao GitHub | **D** | O índice só recebe arquivos do projeto, decisões e itens sincronizados (`KnowledgeSources.cs:56-71`); o SPOCK não lê conteúdo de repositórios GitHub. Contorno possível: pôr Markdown de referência dentro do projeto e usar `knowledge.include` (`Harness/DeclaredConfig.cs:30-33`), sem proveniência, versão ou atualização. |

---

## 6. Conflitos documento x código e documentação (H)

| ID | Conflito | Evidência |
| --- | --- | --- |
| H-01 | `MARILIZ_CURRENT_STATE.md` §3.9 diz que aplicar o plano, state, geração de `.tf`, drift e custo são "Inexistente", mas o código tem `Iac/IacStateService.cs`, `RunKinds.Infrastructure` reversível (`RunKinds.cs:15,18`), `ManagedScope.IsInfrastructureWritable` "geração no modelo" (`ManagedScope.cs:33-40`) e testes `IacDriftAndCostTests`, `IacGenerationTests`, `IacStateTests`. O documento foi escrito em `3f898c3` (antes de #97-#107). Fora do meu domínio: **não verificado em detalhe**. |
| H-02 | `docs/validacao-real.md` mantém "GitHub: não rodado" na tabela de resultados, enquanto `MARILIZ_REAL_WORLD_VALIDATION.md` e `MARILIZ_CURRENT_STATE.md:144` dão o GitHub como validado com repositório real. |
| H-03 | `evolucao-engineering-management.md` ainda diz que a política de contextualização "não tem consumidor" (`:86-87`) e que não há cofre em Linux/macOS (`:100-101,191`); o código já tem consumidores (`ExternalKnowledge.cs:56`) e `KeychainSecretVault`/`SecretServiceVault`. O documento não marca essas seções como superadas. |
| H-04 | `MARILIZ_REAL_WORLD_VALIDATION.md` A4 ("só .NET, Node e Flutter") está superado por `LanguageDetectors.cs`; o relatório é datado, mas não aponta a correção. |
| H-05 | `docs/product-discovery.md` repete a mesma nota de 2026-10-30 cerca de 15 vezes (linhas 3, 7, 11, 20, 24, 28, 89, 93, 102…): defeito de edição. |
| H-06 | Regras instaladas ainda dizem "Managed by Mariliz Studio" (`Assets/rules/general.md`): mantido de propósito para não mudar o hash do Harness (`evolucao…md:475-477`) → **E/I**, não defeito. |
| H-07 | `general.md:9` manda o agente seguir o ciclo TLC em `.specs/`, e o `verifier` grava `validation.md` lá, mas o SPOCK nunca lê `.specs/`: o produto instala um ciclo cujo resultado não consome. Lacuna mais que conflito. |

---

## 7. Crítica

**Engenharia.** O SPOCK é hoje um configurador e observador de engenharia, não um executor. Isso está documentado e é
coerente com a fronteira de segurança, mas tem uma consequência que os documentos não dizem com clareza: quase todas as
capacidades de SE que um usuário espera (refatorar, corrigir bug, revisar PR, rodar testes, aplicar Clean Architecture)
viram **texto em `.claude/rules`** para um único ambiente (Claude Code). O SPOCK não confere se esse texto foi seguido. O
valor de engenharia próprio está na descoberta (sólida, com evidência e confiança) e no Harness (o melhor trecho do
produto). A análise de impacto existe, mas sem grafo de dependências e com recuperação fraca; sem modelo, o resultado é
quase decorativo, como o próprio M4 admitiu.

**Ciclo.** Faltam as três costuras que transformariam o SPOCK num "Harness de entrega": (1) entrada a partir de um item
de trabalho, com classificação e critérios; (2) handoff estruturado para o agente (o plano aprovado vira tarefa com id,
escopo e critérios); (3) retorno: ler o que mudou (git diff/commits/PR) e o `validation.md`, e cruzar com critérios e
escopo para apontar pendências. As peças existem isoladas (ids REQ/US com critérios, `UnifiedDiff`, hash, `run.link`,
`verifier`); falta o fio.

**Integrações.** O desenho de segurança é bom (GET-only, HTTPS, sem seguir redirecionamento, recorte monta a chamada,
política fechada, cofre). O que falta é conteúdo útil para engenharia: descrição e critérios da issue, comentários,
sprints, épicos de verdade; no GitHub, branches, commits, reviews e checks. A ligação demanda↔implementação é manual e
pára na execução do SPOCK, que nunca é a implementação real.

**Permissões.** A separação de conceitos está certa. A granularidade é o ponto fraco: um booleano libera o dado externo
para todos os consumidores de IA de uma vez, e o MCP sem argumento enxerga todos os projetos autorizados.

**Stacks.** A detecção cresceu (fase A4) mais rápido que o resto: Python, Rust, Go e Java são reconhecidos e não recebem
nada; React e Angular são tratados como "JS genérico"; .NET, a stack principal do dono, não tem catálogo de frameworks.
Monorepo funciona por acidente da varredura, sem teste.

---

## 8. Principais lacunas e riscos (priorizados)

1. **Sem detecção de pendências da entrega do agente (HC-16, D).** Risco de dar como "entregue" o que o agente deixou
   pela metade. Caminho: ler `git diff`/log do intervalo da execução e `.specs/features/*/validation.md`; cruzar US/REQ e
   critérios com arquivos tocados e evidências.
2. **Ciclo para em "planejar" (HC-08 a HC-15).** Sem handoff estruturado nem retorno; o backlog da fase A não alimenta
   pedidos nem é conferido. Decisão pendente: o SPOCK passa a orquestrar agentes com escrita (sob plano, aprovação,
   diário e reversão) ou continua só observador?
3. **Integrações rasas para engenharia (INT-07, INT-08, INT-14 a INT-16).** Sem critérios de aceite, comentários,
   branches, commits, reviews e checks não há como fechar a ligação demanda↔implementação.
4. **Nada que usa modelo foi validado com modelo real** (plano, impacto, juiz, fase A, assistentes). O Jira completo
   nunca rodou; OAuth 3LO pendente (empresas que desligam tokens de API ficam de fora).
5. **Classificação de demanda inexistente (HC-04)** apesar de o `issuetype` já estar sincronizado.
6. **Arquitetura e convenções do código existente não reconhecidas (SE-04, SE-20)**; regras de stack só .NET e JS
   genérico; sem skills por stack; .NET sem catálogo de frameworks; monorepo sem teste.
7. **Granularidade de permissão (INT-25) e MCP aberto por padrão no motor (INT-20).** Uma configuração escrita à mão sem
   `--project` expõe todos os projetos autorizados.
8. **Inconsistência latente**: `AREA_BY_STACK` não conhece os ids reais da descoberta (`state.py:18-24`); condição morta em
   `NodeDetector.cs:69`.
9. **Referências externas só como templates (ST-07, ST-08)**: não há base local versionada e com proveniência para
   conhecimento de referência; a titularidade do EmpregaNet segue em aberto no roadmap.
10. **Documentação desatualizada (H-01 a H-05)**: o retrato oficial (`MARILIZ_CURRENT_STATE.md`) já está atrás do código
    em IaC; o documento de evolução ainda descreve limitações resolvidas.

## 9. Não verificado

- Nenhuma suíte foi executada; contagens de teste vêm dos documentos.
- Telas não foram renderizadas; o comportamento da UI vem do código.
- Comportamento real de Jira (além dos erros), CLIs de IA e provedores de modelo.
- Detalhes de IaC (H-01), fora do domínio desta auditoria.
