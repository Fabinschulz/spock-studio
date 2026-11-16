# Matriz de rastreabilidade: visão, decisões e implementação

> Parte da auditoria estratégica do SPOCK (2026-11-15). Navegação e método em [README.md](README.md).
> Estados: A implementado e validado, B parcial, C só documentado, D não implementado, E substituído por melhoria válida,
> F substituído sem justificativa, G regressão, H conflito entre documento e código, I decisão pendente, J não se aplica.

Esta matriz consolida os requisitos e decisões de maior peso. Cada linha aponta para o ID detalhado no documento de
domínio, onde estão as tabelas completas (AI-01 a AI-51, SE-01 a SE-20, HC-01 a HC-16, INT-01 a INT-25, ST-01 a ST-08,
INF-01 a INF-31, LIC-01 a LIC-22, UX-01 a UX-33) e o catálogo histórico (H-01 a H-118).

Origem: **Disc** = `docs/product-discovery.md` e blueprints de 25/10; **Pedido** = pedido do dono com data (histórico de
conversas, fora do repositório); **Doc** = documento do repositório; **Prompt** = prompt desta auditoria. "Validado" quer
dizer exercitado contra o serviço ou ambiente real, não só por teste com dublê.

## 1. Software Engineering

| ID | Requisito ou decisão | Origem | Estado | Evidência | Desvio encontrado | Recomendação |
|---|---|---|---|---|---|---|
| RT-01 | Analisar projeto existente antes de propor mudanças | Disc §4; H-38 | A | `runtime/Mariliz.Application/Discovery/ProjectDiscoverer.cs:13-58`; perfil persistido com histórico (SE-02) | Nenhum | Manter |
| RT-02 | Reconhecer linguagens, frameworks e versões | Disc; A4 (#90) | A / B | `Discovery/NodeDetector.cs:44-60` (versão com confiança); frameworks só por catálogo (SE-03) | Frameworks .NET (EF Core, MediatR) sem catálogo | Catálogo de frameworks .NET |
| RT-03 | Identificar a arquitetura do código existente | Prompt §5.1, §11 | D | Nenhum detector em `Discovery/` (SE-04) | Capacidade central da visão ausente | P1: detector de arquitetura com evidência |
| RT-04 | Detectar code smells e refatorar por critério | Prompt §5.1 | D | `ManagedScope.cs:20-25` limita a escrita ao Harness (SE-06, SE-07) | Ausente | P2, depois de RT-03 |
| RT-05 | Aplicar Clean Architecture, DDD, SOLID quando pertinente | Prompt §5.1 | C | Só texto em `Assets/rules/general.md:16`; nada confere (SE-08) | Regra sem verificação | P2: verificador de direção de dependência por stack |
| RT-06 | Planejar funcionalidades | Disc; ADR-0001 | B | `intelligence/agents/planning.py:11-18`; sem modelo vira plano genérico (SE-09) | Nunca rodado com modelo real (AI-23) | P0: validação com chave real |
| RT-07 | Executar funcionalidades e corrigir bugs | Prompt §5.1 | D (por decisão) / I | `AiTools/AiToolCatalog.cs:17-23` (CLIs só leitura); D5 pendente (SE-10, SE-11) | Promessa de plataforma de engenharia sem elo de execução | P1: decidir D5 e contrato de entrega |
| RT-08 | Análise de impacto | Disc | B | `intelligence/agents/architecture.py`; `nodes.py:134-177` (SE-12) | Fallback usa `AREA_BY_STACK` que não conhece os ids reais (`state.py:18-24`) | P2: corrigir o mapa |
| RT-09 | Rodar e validar testes | Prompt §5.1 | D | Comandos detectados e escritos em regra (`NodeDetector.cs:108-110`), nunca executados (SE-13) | Ausente | P1 com RT-07 |
| RT-10 | Revisão de código e de PR | Prompt §5.1 | C / D | Agente `code-reviewer` instalado (`Assets/agents/code-reviewer.md:4`); PR sem diff (`GitHubConnector.cs:57,71-95`) (SE-14, SE-16) | Ausente como capacidade do SPOCK | P1 |
| RT-11 | Integração com Git | Prompt §5.1 | B | Só `rev-parse` e `status` (`Discovery/SystemProbe.cs:36,40`) (SE-15) | Sem diff, log, branch | P1: leitura de diff e log |
| RT-12 | Criar projetos novos ("da ideia ao projeto") | Pedido 14/11; H-110 | B | `Ideas/IdeaDocuments.cs:10-13` gera documentos (SE-01) | Só documentos; nunca rodado com modelo real | P0 validação real |
| RT-13 | Modernizar legado (jornada original) | Disc `experience-blueprint.md:193`; H-111 | C / H | Cartão "Em breve" (`desktop/ui/src/features/home/journey-grid/journeys.ts:45-50`) (SE-19, UX-32) | Prometido na UI, sem plano | P3, ou retirar o cartão até haver plano |
| RT-14 | Importar requisitos de documentos (jornada original) | Disc; H-112 | B | Fase A gera backlog, não importa documento | Parcial | P3 |
| RT-15 | Respeitar convenções e arquivos da pessoa | Disc §7 | B | `Discovery/HarnessInventoryReader.cs:21`; conflito explícito no apply (SE-20) | Bom no Harness, inexistente no código | Manter e estender com RT-03 |
| RT-16 | Suporte a React, Angular, .NET e monorepo | Prompt §11 | B | Regras só .NET e JS genérico; globs sem `.html` do Angular; sem detecção de workspace pnpm/Nx (ST-01 a ST-04) | Cobertura rasa | P2: regras por stack e detecção de workspace |
| RT-17 | Referências externas como conhecimento, não template | Prompt §11; H-63, H-64 | B / D | EmpregaNet e `harness-engineering` como inspiração com atribuição MIT (`code-reviewer.md:10`); sem base local de referências no índice (ST-07, ST-08) | Só um layout Terraform anonimizado em Markdown (`docs/plataforma/TERRAFORM_PROJECT_LAYOUT.md`) | P2: fonte `@reference/` no índice |

## 2. Ciclo de Harness Engineering

| ID | Requisito ou decisão | Origem | Estado | Evidência | Desvio encontrado | Recomendação |
|---|---|---|---|---|---|---|
| RT-20 | Plano com diff, aprovação granular, apply idempotente e reversível, Evals | Disc; H-36 | A | Jornada Configurar; invariante em `MARILIZ_CURRENT_STATE.md:286-287` | Nenhum | Manter |
| RT-21 | Demanda entra ligada a Jira ou GitHub | Prompt §7 | B | Texto livre de até 4.000 caracteres (`WorkflowInput.cs:9-31`); ligação manual depois (`Execution/RunLinkService.cs:31-66`) (HC-01) | Sem origem estruturada | P1 |
| RT-22 | Classificar a atividade (bug, correção, funcionalidade) | Prompt §7 | D | `nodes.py:39-42` só confere objetivo vazio; `issuetype` sincronizado e não usado (HC-04, INT-05) | Ausente | P1 |
| RT-23 | Critérios de aceite | Prompt §7 | B | Só na fase A (backlog); Jira sem `description` (HC-07, INT-07) | Critérios do Jira não lidos | P1 |
| RT-24 | Delegação estruturada ao agente e retorno | Prompt §6, §7 | C | Harness instala agentes; nada envia tarefa nem recebe resultado (HC-08) | Elo central ausente | P1 |
| RT-25 | Consolidar evidências | Prompt §7 | B | Diário, eventos, `.mariliz/decisions/` (HC-12) | Sem evidência de implementação | P1 com RT-24 |
| RT-26 | Atualizar status e homologar | Prompt §7 | D | Sem escrita externa nem estado de homologação (HC-14, HC-15) | Ausente | P2, exige contrato de escrita |
| RT-27 | **Detectar pendências deixadas pelo agente** | Prompt §7 | D | SPOCK não lê `.specs/` (só citado em `HarnessCatalog.cs:69`), nem diffs, nem commits (HC-16) | Ausente, apesar do agente `verifier` gravar `validation.md` | **P1 de maior valor** |

## 3. AI Engineering

| ID | Requisito ou decisão | Origem | Estado | Evidência | Desvio encontrado | Recomendação |
|---|---|---|---|---|---|---|
| RT-30 | LangGraph para orquestração; .NET como autoridade | ADR-0001; H-01 | A | `intelligence/workflows/engineering/graph.py:123-151` (AI-01) | Nenhum na fronteira de autoridade | Manter |
| RT-31 | Agentes especializados (plano, implementação, revisão, testes, arquitetura, pesquisa) | Prompt §6; Disc (profiles) | B | Só planning, architecture e judge usam modelo (AI-03); agentes de profile do discovery abandonados (H-09) | Perda sem registro (F) | P1: decidir quais agentes existem e onde rodam |
| RT-32 | Subagentes, delegação, paralelismo, dependências | Pedido 02/11 §4.1 (H-05) | D | Arestas lineares (`graph.py:125-140`) (AI-04, AI-05) | Ausente | P2, só depois de RT-24 |
| RT-33 | Retomada após queda | ADR-0001 §6 | B / H | `durability="exit"` (`graph.py:159`); só a pausa de aprovação sobrevive; comentário `server.py:66` diz o contrário (AI-07) | Doc promete mais | P2: corrigir o texto ou mudar a durabilidade |
| RT-34 | Modelo mais capaz no planejamento, especializado na implementação, sem fornecedor fixo | Prompt §6 | B | Três finalidades (`ModelPurposes.cs:64-69`); planejamento e classificação dividem `workflow` (AI-16); três provedores (AI-17: E) | Política grossa; sem custo e latência; sem fallback | P2: finalidades por etapa e metadados de modelo |
| RT-35 | Teto de tokens por finalidade coerente | #89 | H | Tela até 64.000 e 120.000 (`ModelBudgets.cs:16-21`) contra 20.000 e 40.000 no Python (`engineering/state.py:17`, `investigation/graph.py:32`) (AI-18) | Falha silenciosa com `LIMIT_TOKENS` | **P0** |
| RT-36 | Uma governança única para todo uso de modelo | ADR-0001 (broker) | F | Ideias, IaC e assistentes chamam `ModelGateway.ChatAsync` direto (`IdeaArchitectService.cs:39`, `IacGenerationService.cs:53`, `ManagementAssistant.cs:87`, `PortfolioAssistant.cs:121`) | Quatro caminhos sem orçamento comum | P1: ADR da fronteira de IA e teto comum |
| RT-37 | Custo em dinheiro | D4 | I | `costUsd` sempre 0 (AI-21) | Pendente | P3 |
| RT-38 | Validação com modelo real | Pedido 14/11 | D | Validação de 14/11 só testou chave inválida (AI-23) | Maior risco de produto | **P0** |
| RT-39 | Skills: descoberta, instalação, versão, escopo | Disc; H-21 | B | TLC pelo instalador oficial, versões 3.3.0 e 4.0.0 conferidas; uma skill própria (AI-24) | Sem autoria, sem skills por stack (ST-06) | P2 |
| RT-40 | MCP: SPOCK como servidor | Pedido 14/11; H-28 | A | `runtime/Mariliz.Runtime/McpEntry.cs`; stdio, só leitura, auditoria por chamada (AI-26) | Sem `--project`, expõe todos os projetos autorizados (`McpProjectResolver.cs:8-9`) | P1: escopo padrão restrito |
| RT-41 | MCP: SPOCK como cliente de servidores de terceiros | Auditoria agentic §7.2 | D | Inventário só lista (AI-27, AI-28) | Ausente | P3 |
| RT-42 | Plugins | Prompt §8 | D | Nenhum conceito de plugin no código (AI-29, UX-12) | Ausente e sem decisão | I: decidir se é requisito |
| RT-43 | RAG lexical antes de embeddings, com eval de recall | `implementation-roadmap.md:130-132,260`; H-52 | A | FTS5, BM25, símbolos e RRF por projeto (AI-32 a AI-38) | Recall@5 medido 0,25 contra 0,33 publicado (`MARILIZ_CURRENT_STATE.md:116`) | P1: dicionário de termos e reformulação no fluxo de engenharia |
| RT-44 | Embeddings (D3) | ADR-0001 D3 | I | `EMBEDDINGS_NOT_READY` (`nodes.py:83`); `sqlite-vec` no pacote sem uso | Pendente há duas semanas | P2: decidir após medir RT-43 |
| RT-45 | Chroma (embarcado ou Docker) | Exploração | E | Rejeitado por peso (`docs/architecture/auditoria-orquestracao-agentic.md:381`); Docker sem registro (AI-42) | Substituição justificada | Manter a rejeição |
| RT-46 | Defesa contra injeção de prompt e saída validada por contrato | ADR-0001 | A | `intelligence/.../prompts.py`; fallback determinístico (AI-47, AI-49) | Nenhum | Manter |
| RT-47 | Execução controlada de ferramentas e aprovações | Disc | A | `ActionGovernance.cs:16-23`; aprovação ligada ao hash das ações (AI-48, AI-50) | Nenhum | Manter |
| RT-48 | Observabilidade e trilha de auditoria | Fase G | B | Linha do tempo e `ProjectAudit.cs:204-281` (AI-45) | Sem visão única de gasto por projeto | P2 |

## 4. Integrações e permissões

| ID | Requisito ou decisão | Origem | Estado | Evidência | Desvio encontrado | Recomendação |
|---|---|---|---|---|---|---|
| RT-50 | Separar autenticação, vínculo, recorte e política de contexto | Pedido 11/11; H-43 | A | `ConnectionReach.cs:29`, `ExternalKnowledge.cs:56` (INT-23, INT-24) | Política é um booleano único por vínculo (INT-25) | P2: política por destino (índice, workflow, assistente) |
| RT-51 | Jira: issues, épicos, status, dependências, critérios | Prompt §7 | B / D | Campos em `JiraConnector.cs:16`; sem descrição, comentários, sprints (INT-01 a INT-08) | Nunca rodado contra site real | P1 |
| RT-52 | GitHub: branches, commits, PRs, reviews, checks | Prompt §7 | B / D | Só issues e PRs como itens (INT-13 a INT-16) | Raso para engenharia | P1 |
| RT-53 | Escrita externa só com contrato e aprovação | Prompt §14; H-44 | J (não existe escrita) | Só leitura por decisão (INT-09, INT-18) | Coerente | Desenhar contrato antes de qualquer escrita |
| RT-54 | Segredos no cofre do SO | Disc; H-45 | A (Windows) / B | Validado no Windows; Linux e macOS sem máquina real | Validação parcial | P2 |

## 5. Infrastructure Management

| ID | Requisito ou decisão | Origem | Estado | Evidência | Desvio encontrado | Recomendação |
|---|---|---|---|---|---|---|
| RT-60 | Conectores AWS, Azure, GCP só leitura | Pedido 15/11; H-71 | A (AWS) / B | AWS validada com conta real; Azure e GCP só com testes de formato (INF-01 a INF-03) | Docs inflam a validação | P1: validar Azure e GCP |
| RT-61 | AssumeRole respeitado em plan e apply | INF-05 | B com defeito | `Iac/IacOperationService.cs:449-452` passa só `AWS_PROFILE` e ignora `roleArn` (conferido nesta auditoria) | Aplica com a identidade errada | **P0** |
| RT-62 | OpenTofu padrão (D-01) | D-01 | A | `IacRuntimeService` (INF-11) | Nenhum | Manter |
| RT-63 | Download oficial verificado (D-03) | D-03 | A com ressalvas | `OfficialReleaseToolchain.cs:91-139`; OpenPGP próprio, sem expiração ou revogação, sem cosign (INF-14) | Ressalvas de cadeia de confiança | P2 |
| RT-64 | Aprovação humana ligada ao plano | H-77 | A | id e hash do plano, 24 h, confirmações (INF-17) | Plano em texto puro por até 24 h (INF-21) | P2: limpeza e proteção do plano |
| RT-65 | Apply, verificação, state, drift | Entregas 3 a 6 | B | Validados com `terraform_data` e `local_file`, não em nuvem (INF-18 a INF-22) | Sem validação real | P1 em conta descartável |
| RT-66 | IaC integrada ao resto do produto | Prompt §5.3 | B / H | Só geração e bootstrap viram execuções; `INFRASTRUCTURE_MANAGEMENT.md:84` diz que validate, plan e apply também | Integração parcial e doc errado | P2 |

## 6. Licenças e distribuição (D-01 a D-12)

| ID | Decisão | Estado declarado pelo dono | Estado no código e docs | Classe | Recomendação |
|---|---|---|---|---|---|
| RT-70 | D-02 Terraform gerenciado | Aguarda aprovação | Build sem `SpockEdition` é `Unrestricted` (`IacDistributionPolicy.cs`, `Program.cs:75-77`); CI não passa a edição (`ci.yml:149`); `LICENSE_DECISIONS.md:60` diz "liberado, sem trava" (conferido) | H / I | **P0**: o dono decide; até lá, instalador comercial com `-p:SpockEdition=commercial` |
| RT-71 | D-05 `_dbm`/Berkeley DB | Aguarda aprovação | `LICENSE_DECISIONS.md:12,71-85` registra "cumprir a Sleepycat" como adotada (LIC-05) | H | P0: alinhar o registro com o dono |
| RT-72 | D-10 política de licenças | Aguarda aprovação | `license-policy.json` deixa `dl:terraform`, `bin:berkeley-db`, `nuget:JsonSchema.Net` como "registrados", que não bloqueiam (`generate_sbom.py:1170,1220-1231`) | H | P1: um `--enforce` futuro não pode aprovar pendências |
| RT-73 | D-11 validador JSON Schema | Aguarda | `JsonSchema.Net` nos testes e em `validate_sbom.cs:1` | I | Decidir |
| RT-74 | D-04 CPython embarcado | Adotada | EULA protetivo depende da D-09 | B | P1 com D-09 |
| RT-75 | D-06, D-07, D-09, D-12 | Aguardam | Coerentes: sem EULA, sem avisos no instalador, AppImage ainda gerado | I | Decisões do dono antes de distribuir |
| RT-76 | Instalador do Windows baixa o CPython embed sem hash | SC-1 | `scripts/build-runtime.ps1:70-76` | D | P1 |

## 7. Arquitetura, identidade e experiência

| ID | Requisito ou decisão | Origem | Estado | Evidência | Desvio encontrado | Recomendação |
|---|---|---|---|---|---|---|
| RT-80 | Tauri, host Rust mínimo | Disc §6 | A / H | Host com terminal PTY (`desktop/host/src-tauri/src/terminal/`) e 10 comandos contra 3 ou 5 no blueprint | Sem ADR nem modelo de ameaça | P1: ADR do terminal |
| RT-81 | NDJSON em stdio host-motor; HTTP só em 127.0.0.1 com token | Disc §6; ADR-0001 | A | `engine.rs`, `EngineSession.cs`; `compare_digest` no Python | Nenhum | Manter |
| RT-82 | Contratos versionados | Disc §6 | B | 6 schemas para cerca de 110 métodos; ~200 fixtures em três linguagens; lista de métodos em três lugares, só TS e Rust testados | Geração de tipos nunca feita | P2: teste .NET contra `protocol.rs` |
| RT-83 | Ambiente limpo para processos filhos | Blueprint §9 | H | `SystemProcessRunner.cs` só sobrepõe variáveis (conferido) | Segredos do ambiente vazam para CLIs | P1 |
| RT-84 | Core, Application, Infrastructure sem ciclos | Disc | A | Referências de `.csproj` corretas (ARCHITECTURE_AUDIT §2.3) | 79 arquivos montam `JsonObject` | P3 |
| RT-85 | Interface comunica plataforma de engenharia, não chat | Redesign; H-100 | A / B | Direção "conversa" descartada com justificativa (`docs/redesign/proposta-de-redesign.md:111-118`) | Herói "Como podemos ajudar?" soa como chat | P2 |
| RT-86 | Nome oficial SPOCK visível | Pedido 15/11 | I | Nenhuma tela com "SPOCK"; "Spock Studio" no instalador e na janela | Decisão de nome canônico | **P0 (decisão)** |
| RT-87 | Sem associação ao personagem | Pedido 15/11; H-93 | H | Comentários de `desktop/ui/public/favicon.svg:3-4` e `icons/source/spock-studio.svg:3-4`; `evolucao-engineering-management.md:465-466` "associada ao personagem" (conferido) | Conflito direto com a regra | **P0 (decisão)** |
| RT-88 | Nomes de metodologias de terceiros fora da UI | Pedido 30/10; H-22 | B | TLC, Superpowers, SDD com zero ocorrências; "Harness Engineer" em 7 linhas de `configure-project` | Conformidade a confirmar | P2 |
| RT-89 | Onboarding e gestão de projetos | Prompt §12 | D | Sem primeiro uso, sem renomear ou remover projeto (UX-01, UX-04) | Ausente | P1 |
| RT-90 | Atualização automática | Roadmap fase 8 | D | Sem `tauri-plugin-updater` | Ausente | P3 |
| RT-91 | Landing page e posicionamento comercial | Prompt §12 | D | Nada no repositório (UX §5); tagline definida e não exibida | Sem registro | P3 |
