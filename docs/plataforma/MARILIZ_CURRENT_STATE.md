# Spock Studio: diagnóstico do estado atual

**Data:** 2026-11-17 (segunda rodada). **Código lido:** `origin/master` em `09d7e4d` (depois dos PRs #102 a #132).
**Retrato anterior:** 2026-11-15, `master` em `3f898c3` (#101), com os gates da CI rodados na máquina: motor .NET com
1389 testes aprovados e 18 ignorados, 5 de integração do executável, inteligência com 192, interface com 823, host com
45. **Nesta atualização os gates não foram rodados de novo**: as contagens acima são as de 15/11. O que mudou de #102 a
#132 foi lido no código, nos commits e nos documentos de cada entrega. O que foi provado com serviço real vem de
[MARILIZ_REAL_WORLD_VALIDATION.md](../MARILIZ_REAL_WORLD_VALIDATION.md), [REMEDIATION_REPORT.md](REMEDIATION_REPORT.md)
e [INFRASTRUCTURE_MANAGEMENT.md](INFRASTRUCTURE_MANAGEMENT.md).

Registra o que o Spock Studio já faz, o que está parcial e o que não existe, para que as propostas não recriem o que já
está pronto. A homologação do cenário 1 do hub (branch `docs/c2-homologacao-cenario-1`, sem merge) não entra.

Legenda de situação: **Implementado**, **Parcial** (com o que falta), **Inexistente**. "Validado" quer dizer exercitado
com o serviço real; sem essa palavra, a prova é a suíte de testes.

## 1. Arquitetura encontrada

| Camada | Onde | Responsabilidade | Situação |
| --- | --- | --- | --- |
| Host desktop | `desktop/host/src-tauri` (Rust, Tauri 2) | Janela, diálogo nativo de pasta, ciclo de vida do sidecar, allowlist de métodos, projetos recentes, log de diagnóstico em memória, terminal integrado. | Implementado |
| Interface | `desktop/ui` (React, TypeScript, Vite) | Telas, validação Zod das respostas, nenhuma regra de negócio. | Implementado |
| Motor | `runtime/` (.NET 10) | Domínio, casos de uso, persistência, integrações, cloud, infraestrutura como código, gestão, proxy de modelo, protocolo, servidor MCP. | Implementado |
| Runtime de IA | `intelligence/` (Python, LangGraph) | Grafos de engenharia e de investigação, índice e recuperação de conhecimento, checkpoint, evals de IA e de gestão. | Implementado |
| Contratos | `contracts/` | Schemas JSON, fixtures válidas e inválidas, goldens conferidos em mais de uma linguagem. | Implementado |
| Lançador `spock` | `desktop/launcher` (Rust) | Comando no PATH que repassa `version`, `context session` e `mcp` ao motor; é o que o Harness grava no `.mcp.json` e no hook da IDE. | Implementado; no Linux, sem verificação num pacote real |

Como as camadas conversam:

- Interface chama o host por `invoke("engine_request")`; o host fala com o motor por NDJSON v1 em stdin/stdout
  (`runtime/Mariliz.Application/Protocol/EngineSession.cs`). A lista de métodos é conferida em três lugares: interface
  (`ENGINE_METHODS` em `shared/api/engine-client.ts`), host (`FRONTEND_METHODS` em `protocol.rs`) e motor
  (`ProtocolMethods.cs`).
- `workspace.authorize` só nasce do diálogo nativo. A interface nunca envia um caminho.
- O motor fala com o Python por HTTP em `127.0.0.1`, porta efêmera e token por sessão (`LoopbackAiRuntime.cs`; rotas
  `/v1/knowledge/*`, `/v1/context/preview`, `/v1/workflows`, `/v1/investigations`). O Python só chama modelo pelo proxy
  do motor (`Models/ModelBroker.cs`): a chave nunca chega ao Python. O stderr do Python vai para o diagnóstico do host.
- A interface não fala com o Python.
- **Modo MCP:** o mesmo executável, com o argumento `mcp`, vira um servidor MCP por stdio iniciado pela IDE
  (`Mariliz.Runtime/McpEntry.cs`), hoje pelo SDK oficial C# `ModelContextProtocol.Core` 2.4.0 (ADR-0009). Só lê o que o
  aplicativo gravou, anota cada leitura na auditoria e nunca autoriza pasta nova. Recusa iniciar sem projeto (H0-1) e
  usa a mesma composição de serviços do sidecar (H0-2). Sobe um Python próprio quando precisa buscar no índice.
- **Processos filhos** do motor não herdam variáveis com cara de segredo, com exceção nomeada por ferramenta (H0-5).

Projetos .NET: `Mariliz.Core` (modelos e políticas puras), `Mariliz.Application` (casos de uso, protocolo e MCP),
`Mariliz.Infrastructure` (disco, processos, HTTP, cofres, runtime Python), `Mariliz.Infrastructure.Cloud` (o único com
SDK de AWS, Azure e Google, atrás da porta `ICloudProvider`), `Mariliz.Runtime` (sidecar e modo MCP) e `Mariliz.Cli`
(jornada do Harness por linha de comando).

## 2. Protocolo do motor

113 nomes em `ProtocolMethods.cs` (eram 99 em 15/11; o crescimento é quase todo da infraestrutura como código). Os
quatro só do host são `engine.hello`, `engine.shutdown`, `workspace.authorize` e `runtime.health`.

| Grupo | Métodos | O que cobre |
| --- | --- | --- |
| Sessão (só host) | `engine.hello`, `engine.shutdown`, `workspace.authorize`, `runtime.health` | Handshake, autorização de pasta, saúde do motor e do Python. |
| Descoberta e Harness | `project.discover`, `harness.{plan,apply,validate,revert,runs}` | Descoberta, plano com diff, aplicação aprovada, validação por evals, reversão, diários. |
| Execuções | `run.{snapshot,cancel,list,detail,link,unlink,linked}` | Retrato com `sinceSeq`, cancelamento, lista paginada com filtro, detalhe, vínculo com item de trabalho. |
| Workflow | `workflow.{start,resume}` | Pedido de engenharia e retomada da pausa de aprovação. |
| Modelo | `model.{status,save,remove,default,connection,test,purpose,budget}` | Chave por provedor, provedor padrão, conferência da conexão, modelo e teto de tokens por finalidade. |
| Integrações | `integration.catalog`, `connection.{list,create,test,rotate,disconnect,remove,reach}`, `integration.{list,link,configure,unlink,sync,history}` | Conexões, alcance de cada conexão, vínculo com projeto, recorte, política, sincronização. |
| Gestão | `management.{portfolio,metrics,forecast,risks,riskhistory,milestones,report,evidence,ask,investigate,portfolioevidence,portfolioask}` | Portfólio, indicadores, previsão, riscos e histórico, marcos, relatório, assistente do projeto e do portfólio, investigação. |
| Decisões | `decision.{record,list,get}` | Decisões gerenciais com o retrato dos fatos. |
| Engenharia | `engineering.{profile,analyze,knowledge,harness,revisions,catalog,mcp,policy}` | Perfil, conhecimento, estado e revisões do Harness, catálogo, inventário de MCP, política dos agentes. |
| Linha do tempo e auditoria | `timeline.read`, `audit.read` | Projeção das fontes do projeto e trilha unificada. |
| Contexto | `context.review` | O que entraria no contexto de um pedido, com contagem de tokens. |
| Agentes | `aitool.list`, `agent.run`, `ide.connection` | CLIs de agente instaladas, execução só de leitura, configuração do servidor MCP na IDE. |
| Segurança | `security.{scan,plan,fix}` | Varredura, plano de correção, correção aprovada. |
| Ideia (fase A) | `idea.{draft,plan,save}` | Especificação, arquitetura e backlog validados, gravados pelo plano. |
| Cloud | `cloud.{catalog,list,create,validate,permissions,remove,links,link,configure,unlink,read}` | Conexões AWS, Azure e Google, vínculo por projeto, leituras. |
| Infraestrutura como código | `iac.runtime.{status,offer,install,decline,remove,adopt,pin}`, `iac.{layout,validate,plan,apply,operations,operation}`, `iac.state{,.resources,.unlock}`, `iac.bootstrap.{plan,save}`, `iac.architect.{draft,proposal}`, `iac.generate.{plan,save}`, `iac.drift{,.schedule,.schedule.set}`, `iac.cost.{estimate,plan}` | OpenTofu ou Terraform: instalação, validação, plano, aplicação aprovada, state, bootstrap do backend, desenho e geração de `.tf`, drift e custo. |

Não existem métodos `eval.*`: as evals rodam por `harness.validate` e pelo workflow, e aparecem como eventos.

## 3. Funcionalidades por área

### 3.1 Harness e configuração do projeto

| Capacidade | Situação | Evidência e observações |
| --- | --- | --- |
| Declaração, lock, plano, aplicação, conflito, reversão, estado, dispensa, revisões | Implementado | Sem mudança de desenho: `Harness/DeclaredConfig.cs`, `ManagedLockFile.cs`, `PlanBuilder.cs`, `Execution/ApplyService.cs`, `RevertService.cs`, `Engineering/HarnessStatusService.cs`, `HarnessRevisionService.cs`. Escrita limitada a `.claude/`, `.mariliz/`, `mariliz.yaml` e `mariliz.lock` (`Core/Policy/ManagedScope.cs`). |
| Perfil tecnológico | Implementado | .NET, Node, Flutter e CI, e agora Python, Rust, Go e Java pelos manifestos (`Discovery/LanguageDetectors.cs`), com frameworks por catálogo (`Discovery/Frameworks.cs`: Django, FastAPI, LangGraph, Axum, Gin, Spring Boot, Vue, NestJS e outros) e comandos sugeridos. Validado no próprio Spock e num projeto TypeScript. |
| Regras de stack | Parcial | Geral, .NET, JS/TS, comandos do repositório e arquitetura. Python, Rust, Go, Java e Flutter são detectados, mas geram só a limitação `NoRuleProfile` (`Harness/HarnessComposition.cs`). |
| Nome do projeto | Implementado | Vem do manifesto (`package.json`, `pyproject.toml`, `Cargo.toml`, `go.mod` ou a única `.sln`), com o da pasta como reserva; o id continua vindo do caminho (`Workspace/ProjectNameReader.cs`). Não lê `pom.xml` nem Gradle. |
| Da ideia ao projeto (fase A) | Implementado, não validado com modelo real | `Ideas/IdeaArchitectService.cs`: especificação, arquitetura e backlog escritos pelo modelo e conferidos pelo motor (até 2 tentativas). A gravação passa por plano, aprovação e diário. A seção "Stack" aprovada vira regra de arquitetura e entra na eval de requisitos (`Harness/ArchitectureDocuments.cs`). |
| Segurança | Implementado | `Security/`: credenciais (13 regras), arquivos sensíveis fora do `.gitignore` e permissões perigosas de agentes. Corrige só duas coisas (entradas no `.gitignore` e o settings do Claude), com plano, aprovação, diário, nova varredura e reversão. Validado no Spock (1443 arquivos, achados só em fixtures). |
| Evals do Harness | Implementado | 14 no catálogo, 12 obrigatórias (`Validation/EvalCatalog.cs`). O juiz por modelo do Harness continua devolvendo "não rodou", mesmo com modelo configurado. |
| Ambientes de IA | Parcial | Só `claude-code` recebe Harness: desde o C2 (#130), bloco gerenciado no `CLAUDE.md`, servidor `spock` no `.mcp.json` (`spock mcp --project-dir .`) e hook `SessionStart` chamando `spock context session`. `cursor` e `codex` são detectados e recebem só a configuração de MCP pela tela de Agentes (`ide.connection`, também para Gemini); os adaptadores deles (C3) não existem. O hook por prompt (`context prompt`) não existe. |
| Identidade do projeto | Implementado | `project.id: spk-<ULID>` no arquivo de declaração (F1, #124). O host ainda guarda os recentes por caminho; aviso de cópia (fork) e id na tela: Inexistente. |
| Arquivos compartilhados com a pessoa | Implementado | Modo bloco (C1, #128, `ManagedRegion`): o Spock gerencia só o trecho dele em `CLAUDE.md`, `AGENTS.md`, `.mcp.json`, `.cursor/*` e `.codex/*`; o resto do arquivo fica intacto e edição dentro do bloco vira conflito. |
| Nomes dos arquivos | Duas convenções vivas | Projeto novo recebe `spock.yaml`, `spock.lock`, `.spock/` e agentes `spock-*`; projeto configurado antes mantém `mariliz.yaml`, `mariliz.lock`, `.mariliz/` (#132). Pacotes, pasta de dados e variáveis seguem `Mariliz`/`MARILIZ_*`; a marca depende da decisão D-12. |

### 3.2 Agentes, Skills, Rules, Hooks, MCP

| Capacidade | Situação | Evidência e observações |
| --- | --- | --- |
| Catálogo do Harness | Implementado, só leitura | `engineering.catalog` (`Engineering/HarnessCatalogService.cs`, fichas em `Assets/catalog/components.json`): cada componente aparece como aplicado, excluído ou não aplicado, com as ferramentas lidas do próprio arquivo do agente. Tela `/projeto/catalogo`. |
| Agentes | Parcial | Três embutidos (`harness-engineer`, `code-reviewer`, `verifier`). Sem criar, editar nem versionar. |
| Skills | Parcial | `harness-contract` e a estrutura de especificação instalada por comando aprovado. Sem autoria. |
| Rules | Parcial | Ver 3.1. Regras propostas pelo Harness Engineer aparecem no catálogo. |
| Hooks | Parcial | Um hook PreToolUse mesclado em `.claude/settings.json` (`Harness/SettingsMerger.cs`). |
| Política dos agentes | Implementado, só leitura | `engineering.policy` (`Engineering/AgentPolicy.cs`): as regras deny, ask e allow lidas do próprio hook gerenciado. Não é editável pela tela. |
| Inventário de MCP | Parcial | `engineering.mcp` (`Engineering/McpInventory.cs`): transporte, programa ou host, variáveis citadas e segredo literal, sem devolver valores. Catálogo, instalação, teste e grants: Inexistente. |
| Execução de agentes por CLI | Implementado, só leitura, não validado | `aitool.list` e `agent.run` (`AiTools/AiToolCatalog.cs`, `AgentRunService.cs`): Claude Code em modo plano, Cursor Agent e Codex em sandbox de leitura, Gemini CLI sem `--yolo`. Pedido de até 8.000 caracteres, 10 minutos, saída ao vivo com segredos redigidos, registro no diário. Nenhuma CLI estava instalada na validação real. Tela `/agentes`. |
| Servidor MCP do Spock | Implementado, validado por stdio | `Mcp/`: `spock_context_bootstrap` (texto e `structuredContent`, com contrato em `contracts/schemas/mcp/`), `list_projects`, `project_briefing` (tecnologias, comandos, Harness, integrações, indicadores, riscos, marcos, decisões), `decisions` e `search_project` (trechos com caminho e hash; trechos de integração ficam de fora), mais `cloud_connections`, uma ferramenta por leitura cloud liberada e `iac_status`, `iac_validate`, `iac_plan_summary`, `iac_history`, `iac_state`, `iac_drift_summary` e `iac_blueprint_check`. Escopo obrigatório por `--project` ou `--project-dir`; cada leitura vira `ide.context_read` na auditoria, com caminhos e sem conteúdo. Faltam erros tipados, `tools/list_changed` e contrato das demais ferramentas (F3). |
| Context Engine | Parcial | `Context/ContextEngine.cs` (F2, #125): cada item com origem, classificação, data e frescor, orçamento com `omitted[]` e `contextVersion`. Só a intenção `bootstrap` (143 a 153 tokens medidos em três pastas reais); as outras devolvem `CONTEXT_INTENT_UNKNOWN`. Consumido pelo MCP, pelo hook `SessionStart` e por `mariliz context` na CLI. |
| Proposta do Harness Engineer | Implementado | O agente roda no Claude Code da pessoa e grava `.mariliz/proposal.json`; o motor trata como dado não confiável (`Harness/ProposalReader.cs`). |

Ponto central: o Spock **executa agentes só para leitura**. Mudança no projeto continua passando pelo motor, com plano,
aprovação, diário e reversão.

### 3.3 Orquestração de IA, RAG e evals

| Capacidade | Situação | Evidência e observações |
| --- | --- | --- |
| Workflows LangGraph | Implementado, dois grafos | Engenharia (`intelligence/workflows/engineering/graph.py`), sem mudança de nós. Investigação de gestão (`workflows/investigation/graph.py`): enquadrar, reunir, levantar hipóteses, decidir, buscar e concluir, até 4 rodadas, sem checkpoint. A fase A roda no .NET, não no LangGraph. |
| Checkpoint e retomada | Implementado, validado | `SqliteSaver` e `interrupt()`; uma aprovação pendente sobrevive a fechar o aplicativo e é retomada noutro processo. Execução que estava `running` na queda vira falha `ENGINE_RESTARTED` (`Workflows/WorkflowRecovery.cs`). |
| Ações com efeito | Parcial, validado | A única ação continua sendo gravar o registro de decisão em `.mariliz/decisions/`, conferido pelo hash e sem sobrescrever (`Workflows/ProjectActionExecutor.cs`). Conta como alteração e pode ser desfeita na tela da execução. Nenhum código é gerado ou alterado. |
| Provedores de modelo | Implementado, não validado com chave real | OpenAI, Anthropic e Google Gemini, com endereços em allowlist (`Models/ModelProviders.cs`). Chave testada com o provedor antes de ir para o cofre. Validado só que as três APIs reais recusam chave inválida. |
| Modelo e teto por finalidade | Implementado | `model.purpose` (assistente, juiz, workflow) e `model.budget` (workflow 8.000 tokens, de 2.000 a 64.000; assistente 30.000, de 5.000 a 120.000). O juiz gasta do teto do workflow. |
| RAG | Implementado sem vetores, validado | Índice SQLite FTS5 por projeto, BM25 mais símbolos com RRF; um documento ruim não derruba mais o índice. Testes e repetições pesam menos. `sqlite-vec` está no `requirements.lock`, sem uso. Recall@5 com perguntas reais sobre o Spock: 0,33 (`intelligence/evaluations/project_recall.py`); o resto é vocabulário português contra identificadores em inglês. |
| Fontes do índice | Implementado | Arquivos do projeto, decisões registradas (`@decision/`, `Management/DecisionMemory.cs`) e dados de integração (`@external/`, `Integrations/ExternalKnowledge.cs`) só com `feedsKnowledge` ligado na integração. Validado com issues reais do GitHub. |
| Laço agentic do RAG | Parcial | No grafo de engenharia, uma busca por execução; o propósito `rewrite-query` existe e nenhum nó o usa. Várias rodadas só na investigação. |
| Evals | Implementado | Recall e MRR sintéticos, recall no próprio repositório, evals de gestão (enquadramento, fundamentação, cobertura e abstenção, `intelligence/evaluations/management.py`). O juiz é conferido pelo motor (`Workflows/JudgmentVerifier.cs`) e nunca decide o veredito. |
| Validação com modelo real | Inexistente | `LiveValidationTests`, `IdeaLiveTests` e `ManagementEvalLiveTests` e `scripts/live-validation.py` existem e nunca rodaram: falta chave. Tudo que escreve com modelo (assistentes, investigação com modelo, fase A, juiz) está provado só com modelo falso ou roteirizado. |

### 3.4 Gestão

| Capacidade | Situação | Evidência e observações |
| --- | --- | --- |
| Portfólio | Implementado | `Management/Portfolio.cs`, `PortfolioSignals.cs`. Sinais por regra fixa; as linhas abrem o projeto. |
| Indicadores de fluxo | Implementado, validado | `FlowCalculator.cs`: WIP, entregues e novos por semana, lead time e cycle time (mediana e p85; entregue sem início conhecido fica fora e é contado), bloqueados (rótulo `blocked`/`bloqueado` e vínculo "is blocked by", que só o Jira informa), vencidos. Por tipo de dado, nunca por pessoa. |
| Mudança de escopo e previsibilidade | Inexistente | Saem em `notCalculated`, com o motivo: falta guardar o histórico do escopo e o planejado por período. |
| Previsão Monte Carlo | Implementado, validado | `ForecastCalculator.cs`. Recusa quando falta histórico (validado: menos de 6 semanas). |
| Riscos e histórico | Implementado, validado | `RiskRules.cs` (`overdue`, `blocked`, `stalled-wip`, `review-queue`, `scope-growth`, `throughput-drop`); histórico gravado só depois de sincronização completa (`RiskHistory.cs`, `management.riskhistory`). |
| Marcos | Implementado, validado | `MilestoneCalculator.cs`: milestone do GitHub e fixVersion do Jira, com situação contra a previsão. Sem dependências nem ordem de itens. |
| Decisões gerenciais | Implementado, validado | `Decisions.cs`: imutáveis, com o retrato dos fatos e das execuções do momento; entram no índice como memória. |
| Relatório | Implementado, sem salvar | `ProjectReport.cs`: métricas, previsão, riscos e mudanças, marcos, decisões e execuções do período, todo texto do motor; exportação em Markdown na tela. Relatórios salvos: Inexistente. |
| Linha do tempo | Implementado, validado | `Timeline/ProjectTimeline.cs`: projeção de Harness, perfil, execuções, decisões, riscos e sincronizações, sem gravar nada. |
| Investigação | Implementado, não validado com modelo real | `Management/Investigation/`. Sem modelo, reúne os fatos e diz o que não fez (validado). |
| Assistentes do projeto e do portfólio | Implementado, não validado com modelo real | `ManagementAssistant.cs`, `PortfolioAssistant.cs` (até 10 projetos e 40 fatos cada), com verificação de números e citações (`AnswerVerifier.cs`). Sem histórico de perguntas. |
| Dependências, responsáveis, capacidade | Inexistente | Gestão lê só o que foi sincronizado e não cria nada no provedor. |

Portfólio, indicadores, previsão, riscos, marcos, relatório e linha do tempo são determinísticos no .NET. Usam modelo:
`management.ask`, `management.portfolioask` e, quando há chave, `management.investigate`.

### 3.5 Central de integrações

| Capacidade | Situação | Evidência e observações |
| --- | --- | --- |
| GitHub | Implementado, validado | Conexão, vínculo, sincronização completa e incremental, merge, idempotência e erros reais (token, repositório, formato, rede fora, encerramento no meio). O token só existe no cofre. |
| Jira Cloud | Implementado, validado só nos erros | Credencial inválida e site fora do padrão contra o Atlassian real. O ciclo completo nunca rodou: falta projeto de teste e token. Só e-mail e token de API; OAuth 2.0 (3LO) é decisão pendente. |
| Cofre | Implementado | DPAPI no Windows (validado), Keychain no macOS e Secret Service no Linux (`Integrations/SecretVaults.cs`), os dois últimos nunca rodados em máquina real (`UnixVaultRealityTests` pronto). |
| Provedor de IA como conexão | Parcial | A chave fica no mesmo cofre, com ids reservados (`ReservedSecretIds.cs`), mas é configurada em Configurações, fora da central. |
| Política de contextualização | Implementado | Nasce fechada. Consumidores: índice, workflows, investigação e assistente. O MCP retém os trechos externos de propósito e o resumo do MCP mostra só números do motor. |
| Transporte | Implementado | Só GET, só HTTPS, sem seguir redirecionamento; Jira restrito a `*.atlassian.net`. |
| Sincronização | Implementado | Sob demanda, incremental, 50 páginas por execução, retomada por página; evento de término que a interface escuta, com releitura de segurança a cada 15 s. Sem agendamento. |
| Auditoria | Implementado | `audit.ndjson`, inclusive leituras cloud e do MCP. |
| Conectores cloud | Parcial | AWS, Azure e Google só leitura (`Application/Cloud`, `Infrastructure.Cloud`, tela `/cloud`, ferramentas MCP liberadas por vínculo com `agentAccess`). **AWS validada** com conta real por SSO (perfil, cadeia padrão e AssumeRole; motor, MCP e tela). **Azure e Google implementados, não validados**: não havia identidade na máquina. Perfil criado por `aws login` é lido (pacote `AWSSDK.Signin`), validado com um perfil real. Os agentes LangGraph não usam cloud. Ver [CLOUD_CONNECTORS_STATUS.md](CLOUD_CONNECTORS_STATUS.md). |
| MCP, bancos, Azure DevOps, GitLab como conexão | Inexistente | Ponto de extensão: `IIntegrationConnector`. |
| Escrita em provedor | Inexistente | Decisão registrada: só leitura, nos conectores e no `ICloudProvider`. |

### 3.6 Execuções e observabilidade

| Capacidade | Situação | Evidência e observações |
| --- | --- | --- |
| Modelo de execução | Implementado | `Execution/RunJournal.cs`: tipos `apply`, `revert`, `validate`, `workflow`, `assistant`, `investigation`, `agent`, `security`, `idea` (`RunKinds.cs`); oito estados; uma execução por projeto de cada vez. |
| Diário e eventos | Implementado | `journal.json`, `events.jsonl` com `seq` e `links.json` com o vínculo a itens de trabalho. |
| Central de execuções | Implementado | `run.list` (filtro, cursor, resumo) e `run.detail` (`Execution/RunCatalog.cs`); tela `/execucoes` com detalhe em painel e selo no menu para em andamento e aguardando aprovação. A listagem lê o diário inteiro de cada execução (medição pendente). |
| Desfazer | Implementado, validado | Na tela da execução, para `apply`, `workflow`, `security` e `idea`, pelo mesmo caminho do `harness.revert`. |
| Evidências | Implementado | Trechos recuperados com arquivo, chunk, hash, pontuação e recuperador. |
| Tokens e custo | Parcial | Tokens por execução e tetos por finalidade; custo em dinheiro sempre zero (decisão D4). Sem visão agregada. |
| Auditoria única | Implementado, sem "quem" | `audit.read` junta `audit.ndjson` e os diários (`Audit/ProjectAudit.cs`), em categorias; tela `/auditoria`. Não há identidade de pessoa. |
| Logs | Parcial | O host guarda as últimas 300 linhas do motor (com o stderr do Python) e do host, redigidas (`diagnostic_log.rs`), na tela Diagnóstico. Sem gravação em disco nem métricas. |
| Sincronização como execução | Inexistente | Aparece na linha do tempo e em `integration.history`, fora da lista de execuções (decisão pendente). |
| Infraestrutura na central | Parcial | Só o bootstrap do backend usa o tipo `infrastructure` (`Execution/RunKinds.cs`); validar, planejar, aplicar, drift e geração ficam no histórico próprio (`iac.operations`), fora da central. |

### 3.7 Identidade e permissões

Inexistente: usuário, login, organização, equipe e papéis. Controles que existem: pasta autorizada pelo diálogo nativo,
allowlist de métodos em três camadas, política de caminhos graváveis, aprovação humana por mudança, comando e ação,
tokens por sessão em loopback, instância única, agentes por CLI só em modo leitura (argumentos fixos por ferramenta em
`AiTools/AiToolCatalog.cs`, sem nível de autonomia configurável), MCP com escopo obrigatório por projeto e leitura
auditada, leitura cloud liberada por vínculo, projeto a projeto, e infraestrutura que só aplica o plano aprovado, com a
aprovação presa ao hash do plano e confirmações a mais para destruição.

### 3.8 Persistência

Local-first, sem banco central e sem servidor do Spock. Os conectores cloud falam direto com o provedor, com a
credencial que o SDK de cada um já encontra na máquina.

- No projeto: `mariliz.yaml`, `mariliz.lock`, `.claude/`, `.mariliz/`, `.agents/`.
- Diretório de dados do motor: `MARILIZ_DATA_DIR` se definido; senão `%LOCALAPPDATA%\Mariliz` (Windows),
  `~/Library/Application Support/Mariliz` (macOS) ou `$XDG_DATA_HOME/mariliz` (Linux). Dentro dele:
  - `workspaces.json`, `audit.ndjson`, `profiles/<projectId>.json`;
  - modelo: `model-default.json`, `model-purposes.json`, `model-budgets.json`, `model-checks.json`,
    `models/<provedor>/` (o `model.json` com a chave só existe onde não há cofre);
  - `projects/<id>/`: `runs/<runId>/` (diário, eventos, vínculos, backup), `evals/`, `risk-history.json`,
    `decisions/`, `knowledge/index.sqlite`, `execution/checkpoint.sqlite`;
  - `investigation-index/`, `integrations/` (conexões, segredos cifrados, dados sincronizados),
    `cloud/connections.json`;
  - infraestrutura: `tools/<ferramenta>/` (instalações verificadas), `iac/terraform.json` (ferramenta e versão por
    projeto), `iac/plugin-cache/`, e `projects/<id>/iac/` com `operations.json` e a pasta de cada operação (cópia da
    configuração e o `plan.tfplan`).
- No host: `recent-projects.json`.

### 3.9 Infraestrutura como código

| Capacidade | Situação | Evidência e observações |
| --- | --- | --- |
| Ferramenta | Implementado, validado no Windows | OpenTofu como padrão (gratuito, MPL 2.0) e Terraform como opção. Detecção, compatibilidade com o `required_version` (também em `.tofu`), instalação autorizada com assinatura conferida pela chave do fornecedor embutida (`Infrastructure/Iac/OpenTofuToolchain.cs`, `HashiCorpTerraformToolchain.cs`, `OpenPgpSignature.cs`), adoção de instalação existente, ferramenta e versão por projeto, atualização lado a lado e remoção. As duas ferramentas foram instaladas de verdade. |
| Validar | Implementado, validado com o OpenTofu real | `iac.validate`: `fmt -check` e `validate` em cada pasta com `.tf`, sobre uma cópia, sem backend nem nuvem; erros por arquivo e linha (`Application/Iac/IacOperationService.cs`, adaptador único `CliIacRuntime.cs`). |
| Planejar | Implementado, validado com o OpenTofu real | `iac.plan`: `init` e `plan -out` na cópia, com a conexão cloud do projeto passando só nomes; resumo de criar, alterar, substituir e destruir, achados de segurança, valores sensíveis fora da tela e da trilha. O projeto não é alterado. |
| Histórico e trilha | Implementado | `iac.operations` e `iac.operation`; a trilha recebe as operações sem valores. As operações ficam fora da central de execuções. |
| MCP | Implementado, só consulta | `iac_status`, `iac_validate`, `iac_plan_summary`, `iac_history`, `iac_state`, `iac_drift_summary`, `iac_blueprint_check`, `iac_architecture_prepare` (`Mcp/IacMcpTools.cs`). Instalar, planejar e aplicar são decisões da pessoa na interface. |
| Aplicar o plano aprovado | Implementado, validado com o OpenTofu real | Entrega 3: aprovação presa ao id e aos hashes do plano, com prazo; confirmações a mais para destruição, achados graves e produção; apply só do arquivo aprovado; verificação por plano seguinte limpo. **É o primeiro caminho de escrita em provedor do produto.** Com AssumeRole, roda com a credencial do papel (H0-4, validado em conta real). |
| State | Implementado, validado | Entrega 4: onde vive o state, riscos, backend recomendado, bootstrap do bucket (AWS, Azure, Google) por plano reversível, consulta e force-unlock com confirmação ([TERRAFORM_STATE_MANAGEMENT.md](TERRAFORM_STATE_MANAGEMENT.md)). |
| Desenho e geração de `.tf` | Implementado, validado com `fmt` e `validate` | Entregas 5 e 10: da necessidade ao desenho (IA opcional), checagens determinísticas, `.tf` por modelos fixos para AWS, Azure e Google, diff revisado. A parte com IA nunca rodou com modelo real. |
| Drift e custo | Implementado, validado na AWS | Entregas 6, 8 e 9: drift por `plan -refresh-only` na cópia, agendado de 6 h a 7 dias com o aplicativo aberto (`IacDriftScheduler.cs`), recursos da conta fora do state; custo mensal do desenho e do plano com os preços públicos da AWS, sem preço inventado. |

O layout esperado dos projetos segue [TERRAFORM_PROJECT_LAYOUT.md](TERRAFORM_PROJECT_LAYOUT.md). macOS e Linux estão
implementados e não rodados em máquina real.

## 4. Interface atual

### 4.1 Rotas e telas

Rotas em `desktop/ui/src/app/routes.tsx`. Quase todas exigem o motor; as exceções são Início, Configurar, Pedido e
Diagnóstico.

| Rota | Tela | Acabamento (pelo código) |
| --- | --- | --- |
| `/` | Início: seleção de projeto e recentes | Lançador, sem estados próprios |
| `/projeto` | Visão geral | Carregando, erro e vazio; as linhas de execução **não têm link** |
| `/projeto/harness`, `/projeto/conhecimento`, `/projeto/catalogo` | Harness, Conhecimento, Catálogo | Esqueleto compartilhado (`Page`, `ResourceView`) |
| `/configurar` | Jornada Configurar | Continua a mais acabada; guarda o estado ao trocar de rota |
| `/pedido` | Pedido de engenharia | Trata erro e runtime indisponível |
| `/agentes` | Agentes de IA | `ResourceView` |
| `/ideia` | Da ideia ao projeto | Carregando e erro |
| `/seguranca` | Segurança | Sem projeto, sucesso |
| `/portfolio` | Portfólio | Linhas abrem o projeto |
| `/gestao`, `/gestao/assistente`, `/gestao/relatorio` | Acompanhamento, Assistente, Relatório | Vazio e sem projeto; aviso quando falta chave de modelo |
| `/execucoes`, `/execucao/:runId` | Central de execuções e execução | Filtros, detalhe em `Drawer`, Desfazer, volta para a lista |
| `/linha-do-tempo`, `/auditoria` | Linha do tempo e auditoria | Carregando, erro e vazio; ligam para a execução |
| `/integracoes`, `/cloud` | Integrações e conexões cloud | Aviso de cofre indisponível; estados completos |
| `/infraestrutura` | Infraestrutura: ferramenta, versão por projeto, validação e plano | Ciclo da mudança com as etapas que ainda não existem marcadas como próxima entrega |
| `/configuracoes/ia`, `/configuracoes/diagnostico`, `/configuracoes/sobre` | Minha IA (vários provedores e CLIs detectadas), Diagnóstico, Sobre (avisos de terceiros) | Menu próprio de Configurações |

São 24 caminhos mais a rota da execução; 21 aparecem no menu lateral. O Início ainda oferece "Importar requisitos" e
"Modernizar sistema" como "Em breve" (`features/home/journey-grid/journeys.ts`).

Nenhuma tela foi conferida no navegador para este documento. A validação real de 2026-11-14 dirigiu o aplicativo
Tauri por Integrações, sincronização, pedido com aprovação e Auditoria.

### 4.2 Shell

`desktop/ui/src/app/layouts/app-shell/`:
- **Topo:** seletor de projeto (ativo e recentes), alternador Engenharia/Gestão e busca (Ctrl K).
- **Menu lateral:** mostra só o grupo do ambiente atual, mais Plataforma (Execuções, Linha do tempo, Auditoria,
  Integrações, Conexões cloud, Infraestrutura, Configurações), sempre visível.
- **Paleta de comandos:** telas e projetos.
- **Atalhos:** Ctrl/⌘ com K, B (recolher menu), J (painel inferior), ` (terminal) e / (folha de atalhos).
- **Barra de estado:** motor, inteligência local, Terminal, Saída, Atalhos e versão.
- **Painel inferior:** redimensionável, com terminal (xterm) e saída.
- **Navegação:** foco e rolagem voltam ao topo na troca de tela; trilha no cabeçalho da página. O último projeto é
  reaberto ao iniciar.

Não existem: toasts, menus de contexto, identidade visual distinta por ambiente.

### 4.3 Design system em uso

- Tema escuro único, tokens em `desktop/ui/src/shared/styles/_tokens.scss`, acento `#33C1BA`, contraste e paleta
  conferidos por teste (`tokens.contrast.test.ts`, `brand-palette.test.ts`).
- Fontes do sistema (sans e mono), sem `@font-face`; escala 28, 22, 16, 14, 13, 12, 11; espaço em base 4 px; raios 6 e
  10; controle de 34 px.
- `@mariliz/ui-kit` 0.6.0, que não recebe mais mudanças: os componentes novos ficam no aplicativo. Usados: `Button`,
  `TextField`, `Checkbox`, `Alert`, `SelectField`, `Modal`, `Drawer`, `Typography`, `Breadcrumb`, formulários e 32
  ícones. Também `@mui/material` e xterm.
- Componentes locais (`shared/components/ui`): `Page`, `PageHeader`, `PageSection`, `PageState` (carregando, erro,
  vazio, sem projeto, sucesso), `ResourceView`, `ConfirmAction`, `StatusChip`, `StatusGlyph`, `Tooltip`, `DiffView`,
  `EvidenceDisclosure`, `WizardSteps`, `ActionCard`, `SectionCard` e outros.

### 4.4 Problemas de experiência ainda abertos

Dos dez problemas do retrato anterior, estão resolvidos: projeto ativo no shell e seleção que sobrevive a reinício,
ambientes com entrada própria, topo sem repetição do estado do motor, jornada que não se perde ao trocar de rota, e
"Remover conexão" com confirmação. Continuam:

1. As linhas de execução na Visão geral não abrem a execução (`features/project/project-page.tsx`).
2. Configurar e Início não usam `Page`; Cloud, Integrações, Infraestrutura, Auditoria, Linha do tempo, Execução e Ideia
   montam carregando e erro à mão em vez de `ResourceView`; 48 módulos SCSS nas telas.
3. Sem toasts; a confirmação de reversão no Configurar é modal enquanto as outras usam `ConfirmAction` na linha. O
   `Drawer` só é usado na central de execuções.
4. Vocabulário técnico em telas de pessoa (`mariliz.yaml`, `components.exclude`, "% das simulações"); o glossário
   `Term` aparece em só 5 arquivos; "nesta fase" sobrou em três telas.
5. `StatusChip` (41 arquivos) e `StatusGlyph` (4) para a mesma ideia; Portfólio e Conexões cloud com o mesmo ícone.
6. Acompanhamento sem visualização de dados: só texto e tabelas.
7. **Menu longo e misturado** (novo): o grupo Plataforma tem 7 itens fixos e aparece também em Gestão, com
   Infraestrutura, Conexões cloud e Auditoria; conexões ficam em três lugares (Integrações, Conexões cloud, Minha IA);
   "Agentes de IA" não mostra os agentes do Harness, que estão no Catálogo; Configurações é item da Plataforma e modo
   próprio do shell.

## 5. Pontos que não devem ser alterados

1. **A fronteira "a IA propõe, o motor decide e executa".** Agente por CLI só em modo leitura, ação como intenção,
   conteúdo escrito pelo motor, aprovação ligada a hash.
2. **O ciclo do Harness:** plano com diff, aprovação granular, conflito explícito, reversão, idempotência, e
   `mariliz.yaml` como fonte da verdade da pessoa.
3. **Gestão determinística e auditável:** fórmulas explícitas, "sem dado" em vez de zero, previsão reprodutível,
   assistentes com verificação de números e citações.
4. **Segredos nunca saem do motor:** proxy de modelo com orçamento, redação de segredos, credenciais em cofre, cloud só
   com a credencial que o SDK encontra.
5. **Local-first real:** sem servidor do Spock, telemetria desligada, isolamento por projeto.
6. **Separação Connection, Project Integration, Data Scope e Contextualization Policy**, e a política que nasce fechada
   também para o MCP.
7. **Contratos versionados com fixtures** e allowlist de métodos em três camadas.
8. **Catálogo de conectores vindo do motor**, que deixa a interface livre de mudar a cada conector novo.
9. **Tokens semânticos, tema escuro único, teal `#33C1BA` e contraste testado.**
10. **A jornada Configurar** como referência de qualidade para as outras telas.
11. **Regra de texto:** sem travessão em código e interface, "sem dado" para valor ausente (`TypographyTests`).

## 6. Lacunas funcionais

| Área | Lacuna |
| --- | --- |
| Validação real | Nada que escreve com modelo foi rodado com chave real (H0-7); Jira só nos erros; Azure, Google e cofres Unix nunca rodaram; IaC e `.deb` com o `spock` não rodaram em macOS e Linux. |
| Engenharia | Ações com efeito limitadas ao registro de decisão; regras de stack só para .NET e JS/TS; Harness só para Claude Code; sem autoria de agentes, skills, regras e hooks; MCP de terceiros só inventariado; nenhuma leitura do ciclo de especificação (`.specs`); o resultado de um pedido feito na IDE não é verificado pelo produto (D1 a D3 do hub). |
| Hub de contexto | ContextEngine só com `bootstrap`; política por destino (F4) inexistente, ainda um booleano por vínculo; sem hook por prompt; sem adaptadores Cursor e Codex (C3); sem diagnóstico de IDE na tela (C4). |
| IA | RAG sem busca semântica (recall@5 0,33); laço do RAG só na investigação; custo em dinheiro zero. |
| Integrações | Jira sem OAuth; só leitura; sem agendamento de sincronização; sem MCP, banco, Azure DevOps ou GitLab como conexão; agentes LangGraph não leem cloud. |
| Gestão | Sem mudança de escopo, previsibilidade, dependências, responsáveis e relatórios salvos. |
| MCP do Spock | Sem linha do tempo nem relatório como ferramenta. |
| Infraestrutura | Operações fora da central de execuções; Azure e Google só validados até `fmt` e `validate`; macOS e Linux não rodados. |
| Execuções | Sincronização fora da lista; listagem lê diários inteiros; sem visão agregada de tokens. |
| Identidade | Sem usuário, papel ou organização. |
| Distribuição | Instaladores Windows (NSIS) e Linux (deb; AppImage fora pela D-06) gerados pela CI, sem assinatura, sem atualização automática, sem workflow de release, sem macOS. |

## 7. Decisões em aberto

| Decisão | Bloqueia |
| --- | --- |
| Chave de modelo com limite de gasto para os testes ao vivo | Validar assistentes, investigação, fase A, juiz e evals de gestão |
| Projeto Jira de teste e token de API | Ciclo completo do Jira (`LiveValidationTests`) |
| OAuth 2.0 (3LO) do Atlassian e registro do app | Jira em empresas que desligam tokens de API |
| Origem dos embeddings (provedor com chave ou modelo local) | Busca semântica (D3) |
| Identidade de leitura no Azure e no Google | Validar os conectores cloud |
| Máquinas Linux e macOS | Validar os cofres Unix; macOS segue fora (ADR-0001, D2) |
| Certificados de assinatura de código | Instalador assinado |
| Sincronização como execução | Central de execuções completa |
| Marcador de bloqueio configurável por projeto | Bloqueados no GitHub além dos três rótulos fixos |
| Custo em dinheiro (D4), identidade e papéis | Visão de custo e auditoria com "quem" |
| Apagar o repositório temporário da validação real | Limpeza |
| DD-1 a DD-4 e DD-7 a DD-10 do hub ([README](../engineering-intelligence-hub/README.md)) | Modelo de implantação, identidade, sincronização antes de responder, conectores novos, níveis de banco |
| ADR-0002 a 0008 em "proposta" (a 0002, do Context Engine, já tem código no master) | H0-8 |
| Decisões de licença e marca pendentes (D-02, D-05, D-08 a D-12, `docs/compliance/LICENSE_DECISIONS.md`) | H0-6, release, nome único dos arquivos |

## 8. Oportunidades identificadas

A ordem de construção da segunda rodada está em [SPOCK_IMPLEMENTATION_ROADMAP.md](SPOCK_IMPLEMENTATION_ROADMAP.md),
seção 9.

1. Destravar a validação com modelo real: é o maior pedaço do produto que só foi provado com dublê.
2. Fechar o contrato de hub: demais intenções do ContextEngine, política por destino e erros tipados no MCP.
3. Verificar a entrega feita fora do produto (D1 a D3), que é onde "pronto" ainda é declaração.
4. Consolidar a navegação (seção 4.4, item 7) antes de acrescentar qualquer tela.
5. Levar infraestrutura e sincronização para a central de execuções.
6. Busca semântica medida pelo mesmo `project_recall.py` antes de virar padrão.
7. Regras do Harness para as stacks que a descoberta já reconhece (Python, Rust, Go, Java).
8. Assinatura e atualização automática antes de distribuir para fora da máquina do dono.

## 9. O que não foi verificado

- Nenhuma tela foi renderizada para este documento; o acabamento vem do código e da validação real de 2026-11-14.
- Os gates da CI não foram rodados para a atualização de 2026-11-17; as contagens de testes são de 15/11.
- Comportamento com OpenAI, Anthropic, Google, Jira (além dos erros), Azure, Google Cloud e cofres Unix.
- O conteúdo de `evals/` e `investigation-index/` no diretório de dados, e se a interface soma tokens em algum ponto.
- O limite de tempo por execução de workflow: o código mostra 30 s por chamada de chat ao modelo.
