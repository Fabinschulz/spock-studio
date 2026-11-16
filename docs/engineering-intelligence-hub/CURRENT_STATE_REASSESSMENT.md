# Reavaliação do estado atual

> Parte do estudo [Engineering Intelligence Hub](README.md) (2026-11-16). Código lido em `master` `bea3302` (depois dos
> PRs #109 a #118). Os achados da auditoria de 15/11 ([../product-audit/](../product-audit/README.md)) foram conferidos
> de novo no código. Somente leitura; os testes não foram executados nesta rodada.

## 1. Os achados da auditoria, hoje

| Achado | Auditoria | Hoje | Evidência (conferida) | Situação |
| --- | --- | --- | --- | --- |
| P0-1 AssumeRole na IaC (R-01) | IaC usa só o perfil de origem | **Igual** | `Iac/IacOperationService.cs:492`: `AWS_PROFILE = profile ?? sourceProfile`; `roleArn` ignorado. A leitura cloud assume o papel (`Infrastructure.Cloud/Aws/AwsCredentials.cs:18-22`) | Aberto |
| P0-2 Teto de tokens (R-05) | Tela 64k/120k, Python 20k/40k | **Parcial**: o motor já envia o teto (`WorkflowRequests.cs:36`, `InvestigationService.cs:93`), mas o Python faz `min(MAX_TOKENS, pedido)` | `engineering/graph.py:80`, `engineering/state.py:18,110`, `investigation/graph.py:32,82` | Aberto: o corte continua silencioso |
| P0-3 D-02 no build | CI sem `SpockEdition` | **Igual**, e agora também no MCP | `ci.yml:145,150-151` sem edição; `McpEntry.cs:52-53` monta o runtime da IaC sem política, que cai em `Unrestricted` (`IacRuntimeService.cs:49`) | Aberto, com uma via nova (MCP-2) |
| P0-3 Ícone e nome | Associação ao personagem | Não reavaliado neste estudo | — | Decisão do dono |
| P0-4 Validação com modelo real (R-03) | Nunca rodou | **Igual** (C-17 da homologação) | `docs/homologacao-kaivor-2026-11-16/RELATORIO.md:132` | Aberto, depende das chaves do dono |
| P1-1 ADR de execução e de IA | Inexistente | Igual | `docs/architecture/adr/` só tem a 0001 | Aberto |
| P1-2 Elo de entrega | Inexistente | Igual | Sem pacote de tarefa, critérios nem pendências no motor | Aberto |
| P1-3 Git e GitHub tipados | Só `git status`; GitHub só issues e PRs como item | Igual | `Discovery/SystemProbe.cs:36,40`; `GitHubConnector.cs:57-112` | Aberto |
| P1-4 Demanda estruturada | Sem `description` do Jira | Igual | `JiraConnector.cs:16` | Aberto |
| P1-5 Arquitetura existente | Não reconhece | Igual; a homologação acrescentou detecção de comandos em subpasta | `NodeDetector.cs` (C-05) | Aberto |
| P1-6 Ambiente dos filhos (R-10) | Herdado | **Igual** | `SystemProcessRunner.cs:28` só acrescenta; o Python recebe o ambiente inteiro (`LoopbackAiRuntime.cs:244-250`) | Aberto |
| P1-6 MCP sem projeto (R-11) | Expõe todos | **Igual** | `McpOptions.cs:27`, `McpProjectResolver.cs:9` | Aberto |
| RT-50 Política por destino | Um booleano | Igual | `ContextualizationPolicy(bool FeedsKnowledge)` (`ContextualizationPolicy.cs:4`) | Aberto |
| RT-43 Recall do RAG | 0,25 medido; 0,33 documentado | Igual; a homologação somou C-08 (projeto grande indexa em 5 rodadas, limite de 2.000 arquivos) | `spock-questions.json`, `KnowledgeSources.cs:13-15` | Aberto |
| INT-22 Harness para Cursor/Codex | Bloqueado | Igual; o inventário agora **lê** `AGENTS.md`, `.agents/skills` e o MCP do Codex | `AiEnvironments.cs:10-11`, `HarnessInventoryReader.cs:17-22,67-116` | Aberto |
| P2-10 Composição duplicada | Só montagem repetida | **Piorou**: a montagem divergiu com efeito (política de edição, histórico de riscos, state sem nuvem) | `McpEntry.cs:46-73` contra `Program.cs:50,76-79` | Aberto, promovido a P0 neste estudo |
| C-09 Ligar a IDE | — (homologação) | Só `claude mcp add --scope user`; sem `.mcp.json`, sem `MARILIZ_DATA_DIR` | `ide-setup.ts:27-55` | Aberto |

**Leitura:** nenhum P0 da auditoria foi fechado desde 15/11; o trabalho do período foi IaC (entregas 7 a 10), licenças e
a homologação. O P0-2 avançou metade. A duplicação de composição deixou de ser cosmética. Por isso o roadmap deste estudo
começa por eles.

## 2. Inventário: o que existe e como entra na nova arquitetura

Legenda: **R** reutilizar como está; **A** ampliar; **N** novo; **C** corrigir antes.

### 2.1 Núcleo de inteligência do projeto

| Componente | Onde | O que faz hoje | Papel no hub | Ação |
| --- | --- | --- | --- | --- |
| Identidade do projeto | `Workspace/WorkspaceAuthorizer.cs:78-82` | `p-` + SHA-256 do caminho; pasta só pelo diálogo nativo | Chave de todo o contexto | **A**: precisa de um id estável no repositório (worktree e clone viram outro projeto hoje) |
| Perfil tecnológico | `Discovery/` | Linguagens, frameworks, comandos, CI, ambientes de IA | Seção "perfil" do contexto | **R**, com C-12 (perfil velho até "Atualizar") |
| Arquitetura do projeto | — | Só a declarada pela fase A (`ArchitectureDocuments.cs`) | Seção "arquitetura" | **N** (P1-5) |
| Inventário do Harness | `HarnessInventoryReader.cs` | Lê `.claude/`, `CLAUDE.md`, `AGENTS.md`, `.agents/skills`, `.mcp.json`, `.codex/config.toml` | Base para detectar divergência entre canônico e arquivos da IDE | **R** |
| Declaração e lock | `DeclaredConfig.cs`, `ManagedLockFile.cs` | `mariliz.yaml` e `mariliz.lock` com sha256 por arquivo | Modelo canônico do Harness e versão do contexto | **A** |
| Plano, diff, aprovação, reversão | `PlanBuilder.cs`, `WritePlanner.cs`, `ApplyExecution.cs`, `RevertService.cs` | Três vias (lock, disco, desejado), conflito, idempotência, backup | Mecanismo de sincronização dos arquivos de IDE | **R**: é o ativo mais valioso para a Parte II do pedido |
| Escopo gravável | `Core/Policy/ManagedScope.cs:12-24` | `.claude/`, `.mariliz/`, `mariliz.yaml`, `mariliz.lock` | Precisa incluir os arquivos de cada adaptador | **A** (com `CLAUDE.md`, `AGENTS.md`, `.cursor/`, `.mcp.json` em modo bloco) |
| Blocos gerenciados dentro de arquivo | — | Posse é do arquivo inteiro; `settings.json` tem mesclagem dirigida | Necessário para `CLAUDE.md`/`AGENTS.md` da pessoa | **N** |

### 2.2 Conhecimento

| Componente | Onde | Papel | Ação |
| --- | --- | --- | --- |
| Índice FTS5 + símbolos + RRF | `intelligence/knowledge/` | Busca do Context Engine | **R**, melhorar recall (P2-3) |
| Fontes do índice | `Workflows/KnowledgeSources.cs` | Seleção, bloqueio de segredo, orçamento | **A**: orçamento de indexação separado do de execução (C-08) |
| Decisões como memória | `Management/DecisionMemory.cs` | Conhecimento aprovado | **R** |
| Dados externos no índice | `Integrations/ExternalKnowledge.cs` | Conhecimento de integração rotulado | **R**, sob política por destino |
| Revisão de contexto com tokens | `Context/ContextReviewService.cs` | Base do orçamento do Context Engine | **R** |
| Proveniência por trecho | `knowledge/index.py:215-228` | Caminho, hash, linha, versão | **A**: devolver `indexed_at` e a classificação |

### 2.3 Gestão

| Componente | Onde | Papel | Ação |
| --- | --- | --- | --- |
| Indicadores de fluxo, previsão, marcos, riscos, sinais do portfólio | `Management/*Calculator.cs`, `RiskRules.cs`, `PortfolioSignals.cs` | Fonte única dos números para tela **e** IDE | **R** |
| Proveniência de métrica | `MetricSource` (`FlowMetrics.cs:35-41`) | `LastSyncedAt`, `Complete`, provedor, recurso | **R**: já é o que o pedido exige por métrica; falta catálogo com definição e fórmula |
| Assistente verificado | `ManagementAssistant.cs`, `AnswerVerifier.cs` | Padrão de resposta fato, cálculo, inferência, recomendação | **R** na tela; na IDE quem redige é o modelo da IDE, então a verificação vira contrato de dados (ver [ENTERPRISE_INTELLIGENCE.md](ENTERPRISE_INTELLIGENCE.md)) |
| Briefing gerencial no MCP | `Mcp/ManagementBriefing.cs` | Primeira versão da gestão na IDE | **A** para ferramentas estruturadas |
| Organização, equipe, papéis | — | — | Depende de [DEPLOYMENT_MODELS.md](DEPLOYMENT_MODELS.md) |

### 2.4 Integrações

| Componente | Onde | Papel | Ação |
| --- | --- | --- | --- |
| Conexão, vínculo, recorte, política | `Integrations/` | Catálogo central do Connector Hub | **R** + política por destino (**A**) |
| GitHub | `GitHubConnector.cs` | Issues e PRs | **A** (P1-3) |
| Jira Cloud | `JiraConnector.cs` | Issues, changelog, links, fixVersions | **A**: `description`, critérios; validação de ponta a ponta |
| Azure DevOps, GitLab, Linear, bancos | — | — | **N** (P2), atrás de `IIntegrationConnector` |
| Sincronização | `SyncService.cs` | Sob demanda, incremental | **A**: sincronização leve antes de responder na IDE quando estiver velha, com teto |
| Cloud | `Application/Cloud`, `Infrastructure.Cloud` | Leitura com `agentAccess` | **R** |
| Inventário de MCP de terceiros | `Engineering/McpInventory.cs` | Ponto de partida do cliente MCP | **A** (P2) |
| Cofre | `SecretVaults.cs` | Segredos fora de contexto | **R** |

### 2.5 Interface com as IDEs

| Componente | Onde | Papel | Ação |
| --- | --- | --- | --- |
| Servidor MCP | `Mcp/` | Contrato principal | **C** (escopo, composição) e **A** (bootstrap, gestão estruturada) — ver [MCP_CAPABILITY_CATALOG.md](MCP_CAPABILITY_CATALOG.md) |
| Configuração da IDE | `ide-setup.ts`, `IdeConnection.cs` | Texto para copiar | **A**: vira componente do Harness, aplicado com diff |
| Hook PreToolUse | `Assets/hooks/mariliz-policy.py`, `SettingsMerger.cs` | Política dos agentes no Claude Code | **R**; base para hooks de contexto |
| CLIs de IA em leitura | `AiToolCatalog.cs`, `AgentRunService.cs` | Execução de agente pela tela | **R**; sem relação com o hub, exceto que não recebem o MCP |

## 3. Respostas curtas às perguntas de inventário

- **O SPOCK tem os elementos fundamentais?** Sim para conhecimento, governança de escrita e gestão calculada. Não para
  identidade estável do projeto, contexto composto com orçamento, saída estruturada no MCP, adaptadores multi-IDE e
  verificação de entrega.
- **O que corrigir antes de expandir?** Escopo obrigatório do MCP, composição única do MCP, teto de tokens, ambiente dos
  filhos e AssumeRole. Os três primeiros afetam diretamente o hub.
