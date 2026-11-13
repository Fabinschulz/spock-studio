# Spock Studio: diagnóstico do estado atual

**Data:** 2026-11-12. **Branch lida:** `feat/plataforma-fase-6-versoes` (commit `5fa0ddd`).
**Método:** leitura de código e documentação. Nenhum build, teste ou execução do aplicativo foi feito para este
documento; números de suíte citados vêm de `docs/plataforma/evolucao-engineering-management.md`.

Este é o documento 2 da análise de referência. Ele registra o que o Spock Studio já faz, o que está parcial e o que
não existe, para que as propostas não recriem o que já está pronto.

Legenda de situação: **Implementado**, **Parcial** (com o que falta), **Inexistente**.

## 1. Arquitetura encontrada

| Camada | Onde | Responsabilidade | Situação |
| --- | --- | --- | --- |
| Host desktop | `desktop/host/src-tauri` (Rust, Tauri 2) | Janela, diálogo nativo de pasta, ciclo de vida do sidecar, allowlist de métodos, projetos recentes. | Implementado |
| Interface | `desktop/ui` (React, TypeScript, Vite) | Telas, validação Zod das respostas, nenhuma regra de negócio. | Implementado |
| Motor | `runtime/` (.NET 10) | Domínio, casos de uso, persistência, integrações, gestão, proxy de modelo, protocolo. | Implementado |
| Runtime de IA | `intelligence/` (Python, LangGraph) | Um grafo de engenharia, índice e recuperação de conhecimento, checkpoint, evals de IA. | Implementado |
| Contratos | `contracts/` | Schemas JSON, fixtures válidas e inválidas, goldens conferidos em mais de uma linguagem. | Implementado |

Como as camadas conversam:

- Interface chama o host por `invoke("engine_request")`; o host fala com o motor por NDJSON v1 em stdin/stdout
  (`runtime/Mariliz.Application/Protocol/EngineSession.cs`). A lista de métodos é conferida em três lugares: interface
  (`ENGINE_METHODS`), host (`FRONTEND_METHODS` em `protocol.rs`) e motor (`ProtocolMethods.cs`).
- `workspace.authorize` só nasce do diálogo nativo. A interface nunca envia um caminho.
- O motor fala com o Python por HTTP em `127.0.0.1`, porta efêmera e token por sessão. O Python só chama modelo pelo
  proxy do motor (`Models/ModelBroker.cs`): a chave nunca chega ao Python.
- A interface não fala com o Python.

Projetos .NET: `Mariliz.Core` (modelos e políticas puras), `Mariliz.Application` (casos de uso e protocolo),
`Mariliz.Infrastructure` (disco, processos, HTTP, DPAPI, runtime Python), `Mariliz.Runtime` (sidecar) e `Mariliz.Cli`
(jornada do Harness por linha de comando).

## 2. Protocolo do motor

42 métodos, 38 liberados à interface (`runtime/Mariliz.Application/Protocol/ProtocolMethods.cs`).

| Grupo | Métodos | O que cobre |
| --- | --- | --- |
| Sessão (só host) | `engine.hello`, `engine.shutdown`, `workspace.authorize`, `runtime.health` | Handshake, autorização de pasta, saúde do motor e do Python. |
| Descoberta e Harness | `project.discover`, `harness.plan`, `harness.apply`, `harness.validate`, `harness.revert`, `harness.runs` | Descoberta, plano com diff, aplicação aprovada, validação por evals, reversão, diários. |
| Execuções | `run.snapshot`, `run.cancel` | Retrato com `sinceSeq` e cancelamento. |
| Workflow | `workflow.start`, `workflow.resume` | Pedido de engenharia e retomada da pausa de aprovação. |
| Modelo | `model.status`, `model.save`, `model.clear`, `model.complete` | Chave do provedor e conclusão simples. |
| Integrações | `integration.catalog`, `connection.{list,create,test,rotate,disconnect,remove}`, `integration.{list,link,configure,unlink,sync,history}` | Conexões, vínculo com projeto, recorte, política, sincronização. |
| Gestão | `management.{portfolio,metrics,forecast,risks,evidence,ask}` | Portfólio, indicadores, previsão, riscos, assistente. |
| Engenharia | `engineering.{profile,analyze,knowledge,harness,revisions}` | Perfil tecnológico, conhecimento, estado e revisões do Harness. |

Não existem métodos `eval.*`: as evals rodam por `harness.validate` e aparecem como eventos.

## 3. Funcionalidades por área

### 3.1 Harness e configuração do projeto

| Capacidade | Situação | Evidência e observações |
| --- | --- | --- |
| Declaração `mariliz.yaml` | Implementado | `Harness/DeclaredConfig.cs`. É da pessoa; o produto só edita `components.exclude`, a pedido e pelo plano (`DeclarationEditor.cs`). |
| Lock `mariliz.lock` | Implementado | `Harness/ManagedLockFile.cs`: componentes, arquivos gerenciados, modo `file` ou `merge`, SHA-256. |
| Plano, aplicação, conflito, reversão | Implementado | `Harness/PlanBuilder.cs`, `Execution/ApplyService.cs`, `RevertService.cs`. Diff por arquivo, aprovação por mudança e por comando, manter ou substituir, backup, idempotência. Escrita limitada a `.claude/`, `.mariliz/`, `mariliz.yaml` e `mariliz.lock` (`Core/Policy/ManagedScope.cs`). |
| Estado do Harness | Implementado | `Engineering/HarnessStatusService.cs`: íntegro, mantido, modificado, ausente, fora do escopo. Só leitura. |
| Dispensar e voltar a aplicar | Implementado | `Harness/ComponentRequest.cs`, `SettingsMerger.cs`. Sempre pelo plano aprovado. |
| Revisões do Harness | Implementado na branch atual | `Engineering/HarnessRevisionService.cs`, derivadas dos diários. Sem comparação de versão por componente (decisão registrada). |
| Perfil tecnológico | Implementado | `Engineering/TechnologyProfileService.cs`; detectores .NET, Node (Next, React, Angular), Flutter e CI; histórico de 100 mudanças. |
| Conhecimento do projeto | Implementado como leitura | `Engineering/ProjectKnowledgeService.cs` sobre `Workflows/KnowledgeSources.cs`. |
| Evals do Harness | Implementado | 14 no catálogo (`Validation/EvalCatalog.cs`), 12 determinísticas obrigatórias. O juiz por modelo do Harness sempre devolve "não rodou". |
| Ambientes de IA | Parcial | Só `claude-code` recebe configuração; `cursor` e `codex` são apenas detectados (`Discovery/AiEnvironments.cs`). |

### 3.2 Agentes, Skills, Rules, Hooks, MCP

| Capacidade | Situação | O que falta |
| --- | --- | --- |
| Agentes | Parcial | Três embutidos (`harness-engineer`, `code-reviewer`, `verifier`) em `Assets/agents/`. Não há criar, editar, versionar nem tela de catálogo; só aplicar ou dispensar. |
| Skills | Parcial | Uma própria (`harness-contract`) e a estrutura de especificação externa instalada por comando aprovado. Sem catálogo nem autoria. |
| Rules | Parcial | Geral, .NET, JS/TS, comandos do repositório e regras extras da proposta. Sem regra para Flutter. |
| Hooks | Parcial | Um hook PreToolUse mesclado em `.claude/settings.json`. |
| MCP servers | Parcial (só inventário) | `Discovery/HarnessInventoryReader.cs` lê `.mcp.json` e sinaliza segredo literal. Catálogo, instalação, teste e grants: Inexistente. |
| Proposta do Harness Engineer | Implementado | O agente roda no Claude Code da pessoa e grava `.mariliz/proposal.json`; o motor trata como dado não confiável (`Harness/ProposalReader.cs`). |

Ponto central: o Spock Studio **não executa agentes com ferramentas**. O trabalho de engenharia roda no ambiente de IA
da pessoa (Claude Code), com os ativos que o produto instala. O motor planeja, valida, aplica e registra.

### 3.3 Orquestração de IA, RAG e evals

| Capacidade | Situação | Evidência e observações |
| --- | --- | --- |
| Workflow LangGraph | Implementado, um grafo | `intelligence/workflows/engineering/graph.py`: `analyze_request`, `build_context`, `retrieve`, `plan`, `validate_plan`, `architecture` e `check_impact` (quando o pedido marca impacto), `evaluate`, `approval`, `synthesize`. |
| Checkpoint e interrupção humana | Implementado | `SqliteSaver` por projeto; `interrupt()` no nó `approval`; `approvalId` ligado ao hash das ações; retoma após reinício (`Workflows/WorkflowService.cs`, `ApprovalGate.cs`). |
| Ações com efeito | Parcial | A única ação é gravar um registro de decisão em `.mariliz/decisions/`, com conteúdo escrito pelo motor e aprovação obrigatória (`Workflows/ProjectActionExecutor.cs`). Nenhum código é gerado ou alterado. |
| `ModelGateway` | Parcial | Só OpenAI, `gpt-4o-mini`, endpoint em allowlist; teto de 8.000 tokens e 60 s por execução; redação de segredos. Sem escolha de provedor nem de modelo. |
| RAG | Implementado sem vetores | Seleção de fontes no .NET; índice SQLite FTS por projeto; chunking estrutural; recuperação híbrida BM25 mais símbolos com RRF. |
| Laço agentic do RAG | Parcial | Uma busca por execução, sem reformulação nem múltiplas rodadas. Embeddings adiados (decisão D3). Só fontes do próprio repositório. |
| Evals de IA | Implementado | Recall@k e MRR sobre dataset próprio; baselines do grafo; juiz por modelo conferido pelo motor (`Workflows/JudgmentVerifier.cs`). O juiz nunca decide o veredito. |
| Validação com modelo real | Inexistente | Nenhuma suíte chamou modelo real (`docs/validacao-fase-10.md`). |

### 3.4 Gestão

| Capacidade | Situação | Evidência e observações |
| --- | --- | --- |
| Portfólio | Implementado | `Management/Portfolio.cs`, `PortfolioSignals.cs`. Sinais por regra fixa. |
| Indicadores de fluxo | Implementado | `FlowCalculator.cs`, `DeliveryRules.cs`: WIP, entregues e novos por semana, lead time (mediana e p85), prazo vencido. Por tipo de dado, nunca por pessoa. |
| Cycle time, bloqueios, mudança de escopo, previsibilidade | Inexistente | Saem em `notCalculated`, com o motivo. |
| Previsão Monte Carlo | Implementado | `ForecastCalculator.cs`: 10.000 rodadas, semente derivada dos dados, percentis 15, 50 e 85, confiança com motivos, recusa quando falta histórico. |
| Riscos por regra | Implementado | `RiskRules.cs`: `overdue`, `stalled-wip`, `review-queue`, `scope-growth`, `throughput-drop`. Sem histórico de acompanhamento. |
| Assistente gerencial | Implementado, não validado com modelo real | `Assistant/EvidenceBuilder.cs`, `ManagementAssistant.cs`, `AnswerVerifier.cs`. Uma chamada, sem LangGraph, sem histórico de perguntas, um projeto por vez. |
| Marcos, dependências, responsáveis, decisões gerenciais, relatórios salvos | Inexistente | Gestão lê só o que foi sincronizado e não cria nada. |

Portfólio, indicadores, previsão e riscos são determinísticos no .NET. Só `management.ask` usa modelo.

### 3.5 Central de integrações

| Capacidade | Situação | Evidência e observações |
| --- | --- | --- |
| Conexões GitHub e Jira Cloud | Implementado | `Integrations/ConnectionService.cs`: criar, testar, trocar credencial, desconectar, remover. Só grava depois que o provedor aceita. |
| Estados de conexão | Implementado | `Connected`, `Error`, `Disconnected`. Sincronização: `Running`, `Succeeded`, `Partial`, `Failed`. |
| Cofre | Parcial | DPAPI no Windows (`DpapiSecretVault.cs`). Linux e macOS: a central recusa criar conexões. |
| Vínculo com projeto e recorte de dados | Implementado | `ProjectIntegration.cs`, `DataScope.cs`; isolamento por projeto em serviço, arquivo e leitura. |
| Política de contextualização | Parcial | Nasce fechada e é gravada. O único consumidor é o assistente gerencial; nenhum agente nem o índice lê dado sincronizado. |
| Transporte | Implementado | Só GET, só HTTPS, sem seguir redirecionamento; Jira restrito a `*.atlassian.net`. |
| Sincronização | Implementado | Sob demanda, incremental, 50 páginas por execução, retomada por página, sem eventos (a interface relê a cada 1,5 s). |
| Auditoria de integrações | Implementado | `FileAuditLog.cs`, `{dataRoot}/audit.ndjson`. |
| Provedor de IA como conexão | Inexistente | A chave de modelo fica fora da central. Desde a fase A ela vai para o cofre (DPAPI no Windows) e `model.json` só existe onde não há cofre. |
| MCP, bancos, APIs HTTP, Azure DevOps, GitLab como conexão | Inexistente | Ponto de extensão: `IIntegrationConnector`. |
| Escrita em provedor | Inexistente | Decisão registrada: só leitura. |
| Validação com serviço real | Inexistente | Sincronização real do GitHub e todo o conector do Jira nunca foram validados. |

### 3.6 Execuções e observabilidade

| Capacidade | Situação | Evidência e observações |
| --- | --- | --- |
| Modelo de execução | Implementado | `Execution/RunJournal.cs`: tipos `apply`, `revert`, `validate`, `workflow`; oito estados (`RunStates.cs`); uma execução por projeto de cada vez. |
| Diário e eventos | Implementado | Operações, etapas, evals, ações, aprovação, juízo, uso de modelo. Eventos com `seq` em `events.jsonl`. |
| Histórico | Parcial | `harness.runs` e `run.snapshot` existem, mas não há tela de lista de execuções com filtro, nem reabertura de uma execução antiga pela interface. |
| Evidências | Implementado | Trechos recuperados com arquivo, chunk, hash, pontuação e recuperador. |
| Tokens e custo | Parcial | Tokens e chamadas contados pelo proxy; custo em dinheiro sempre zero (decisão D4). Sem visão agregada. |
| Auditoria única | Parcial | `audit.ndjson` cobre integrações e respostas do assistente; Harness e workflows ficam só no diário. Sem "quem" (não há identidade). |
| Logs | Parcial | Só stderr do motor. Sem log estruturado, métricas ou modo de diagnóstico. |
| Sincronização como execução | Inexistente | Sincronizações têm histórico próprio, fora do modelo de execução. |

### 3.7 Identidade e permissões

Inexistente: usuário, login, organização, equipe e papéis (decisão registrada na seção 6 do documento da plataforma).
O que existe como controle: pasta autorizada pelo diálogo nativo, allowlist de métodos em três camadas, política de
caminhos graváveis, aprovação humana por mudança, comando e ação, tokens por sessão em loopback, instância única.

### 3.8 Persistência

Local-first, sem banco central e sem nuvem.

- No projeto: `mariliz.yaml`, `mariliz.lock`, `.claude/`, `.mariliz/`, `.agents/`.
- No diretório de dados do motor (`%LOCALAPPDATA%\Mariliz`): `workspaces.json`, `model.json`, `audit.ndjson`,
  `projects/<id>/runs/<runId>/` (diário, eventos, backup), `projects/<id>/evals/`, índice e checkpoint em SQLite,
  `profiles/<projectId>.json`, `integrations/` (conexões, segredos cifrados, dados sincronizados).
- No host: `recent-projects.json`.

## 4. Interface atual

### 4.1 Rotas e telas

| Rota | Tela | Acabamento |
| --- | --- | --- |
| `/` | Início: cinco atividades, seletor de pasta, recentes | Completa, mas só lançador |
| `/projeto` | Projeto: perfil, Harness aplicado, revisões, inventário, conhecimento, execuções | Completa em conteúdo; relatório longo em coluna única |
| `/configurar` | Jornada Configurar (6 etapas, painel de contexto) | A parte mais acabada do produto |
| `/pedido` | Pedido de engenharia | Completa, sem item no menu |
| `/execucao/:runId` | Execução de workflow | Funcional, visualmente crua, sem volta nem lista |
| `/portfolio` | Portfólio | Parcial: linhas não abrem o projeto |
| `/gestao` | Acompanhamento: atividades, riscos, previsão, fluxo, fontes | Completa em conteúdo, pobre em hierarquia visual |
| `/gestao/assistente` | Assistente | Completa no escopo, sem histórico |
| `/integracoes` | Conexões e vínculos do projeto | Completa |
| `/configuracoes` | Chave de modelo | Parcial como "Configurações" |

### 4.2 Shell

Grid de duas colunas (`desktop/ui/src/app/layouts/app-shell/`): rail de 15rem que colapsa para 3,5rem abaixo de 864 px,
faixa superior de 36 px com tagline, estado do motor e versão, e a área principal. Menu em quatro grupos: Início,
Engenharia (Projeto, Configurar, Catálogo indisponível), Gestão (Portfólio, Acompanhamento, Assistente), Plataforma
(Integrações, Configurações).

Não existem: indicação do projeto ativo no shell, seletor de projeto fora do Início, alternador de ambiente, paleta de
comandos, atalhos de teclado, tooltips, toasts, menus de contexto. O único `Modal` é a confirmação de reversão; o
`Drawer` do ui-kit não é usado.

### 4.3 Design system em uso

- Tema escuro único, tokens semânticos em `desktop/ui/src/shared/styles/_tokens.scss`, acento `#33C1BA`, contraste
  conferido por teste (`tokens.contrast.test.ts`).
- Tipografia Inter variável; escala 28, 22, 16, 14, 13, 12, 11. Espaço em base 4 px; raios 6 e 10; controle de 36 px.
- `@mariliz/ui-kit` 0.6.0. Usados: `Button`, `Alert`, `TextField`, `Checkbox`, `Modal`, `Breadcrumb`, `Typography`,
  `FormProvider`, `TextFormField`, `CheckboxFormField`. Exportados e não usados: `Drawer`, `Menu`, `MenuItem`,
  `TabBar`, `TabItem`, `DataTable`, `TableComponent`, `LoadingBar`, `Divider`, `SelectField`, `Switch`, `Autocomplete`,
  `Avatar`, `CardData`, e cerca de 410 dos 426 ícones.
- Componentes locais: `StatusChip`, `StatusGlyph`, `Glyph`, `InlineNotice`, `ActionCard`, `SectionCard`, `PageHeader`,
  `WizardSteps`, `EvidenceDisclosure`, `DiffView`, `JourneyFrame`, `ContextPanel`.

### 4.4 Problemas de experiência observados no código

1. O projeto ativo não aparece no shell; trocar exige voltar ao Início; o Portfólio lista projetos e não deixa abrir
   nenhum; a seleção não sobrevive a reinício.
2. Engenharia e Gestão são apenas rótulos de grupo do mesmo menu, sem página de entrada nem identidade por ambiente.
3. Projeto e Acompanhamento empilham seis seções de texto em 46rem, sem resumo, abas, âncoras nem visualização de dados.
   Em janelas largas, metade da tela fica vazia.
4. Não há esqueleto de página compartilhado: cabeçalho, seção, linha, vazio, carregando e erro estão copiados em quatro
   módulos SCSS, com quatro padrões de leitura assíncrona.
5. A faixa superior repete o estado do motor e não carrega título, trilha, projeto nem ações.
6. Pedido de engenharia e Execução não têm item de menu; execuções listadas em Projeto não têm link; "Configurar" fica
   ao lado de "Configurações"; Projeto e Configurar usam o mesmo ícone.
7. Feedback fraco: carregamento é uma frase, sucesso não é confirmado, erro de ação aparece longe da linha, "Remover"
   conexão não pede confirmação, confirmação destrutiva ora é modal, ora inline.
8. A jornada perde o estado ao sair da rota; o stepper não é navegável; foco e rolagem não são tratados na troca de etapa.
9. Vocabulário técnico (`mariliz.yaml`, `components.exclude`, "hook", "percentil 85", "Evals") em telas que o gestor vê;
   mistura de português do Brasil com português europeu ("Guardar", "A carregar").
10. Quatro fontes de ícone, `StatusChip` e `StatusGlyph` para a mesma ideia, dois seletores de pasta, duas listas de
    recentes, componentes sem consumidor.

## 5. Pontos que não devem ser alterados

1. **A fronteira "a IA propõe, o motor decide e executa".** Agente sem ferramenta, ação como intenção, conteúdo escrito
   pelo motor, aprovação ligada a hash.
2. **O ciclo do Harness:** plano com diff, aprovação granular, conflito explícito, reversão, idempotência, e
   `mariliz.yaml` como fonte da verdade da pessoa.
3. **Gestão determinística e auditável:** fórmulas explícitas, "sem dado" em vez de zero, previsão reprodutível,
   assistente com verificação de números e citações.
4. **Segredos nunca saem do motor:** proxy de modelo com orçamento, redação de segredos, credenciais em cofre.
5. **Local-first real:** nada de nuvem, telemetria desligada, isolamento por projeto.
6. **Separação Connection, Project Integration, Data Scope e Contextualization Policy**, já modelada.
7. **Contratos versionados com fixtures** e allowlist de métodos em três camadas.
8. **Catálogo de conectores vindo do motor**, que deixa a interface livre de mudar a cada conector novo.
9. **Tokens semânticos, tema escuro único, teal `#33C1BA` e contraste testado.**
10. **A jornada Configurar** (linha do tempo, painel de contexto, aprovação explícita) como referência de qualidade
    para as outras telas.
11. **Regra de texto:** sem travessão em código e interface, "sem dado" para valor ausente (`TypographyTests`).

## 6. Lacunas funcionais

| Área | Lacuna |
| --- | --- |
| Navegação | Sem contexto de projeto no shell, sem alternância de ambiente, sem busca global, sem central de execuções. |
| Engenharia | Um único workflow que não executa trabalho; sem catálogo de agentes, skills, regras, hooks e MCP; só Claude Code. |
| IA | Um provedor, um modelo, chave em texto puro, sem custo, sem memória de conversa, RAG sem laço agentic. |
| Integrações | Dois conectores só de leitura, nunca validados com dado real; cofre só no Windows; dado sincronizado não alimenta agentes; sem MCP, banco ou API como conexão. |
| Gestão | Sem cycle time, bloqueios, marcos, dependências, histórico de riscos, decisões e relatórios salvos. |
| Execuções | Sem lista unificada, sem filtro, sem reabrir execução antiga, sem custo, auditoria repartida. |
| Identidade | Sem usuário, papel ou organização. |
| Distribuição | Sem assinatura, sem atualização automática, sem macOS. |

## 7. Decisões em aberto já registradas

Da seção 13 de `evolucao-engineering-management.md`: credencial de teste para GitHub e Jira; cofre em Linux e macOS;
migrar a chave de modelo para o cofre; identidade e papéis; nome nos textos gravados nos projetos; identificadores
internos; travessões na documentação.

Do ADR-0001 e da auditoria de orquestração: embeddings (D3), Harness Engineer dentro do produto (D5), custo em dinheiro
(D4), `workspaceId` reservado.

Ordem de entregas aprovada pelo dono do produto em 2026-11-11 (ainda não escrita nos documentos): revisões do Harness
(feita), execuções ligadas à entrega, cycle time e bloqueios, decisões gerenciais com evidência técnica, agentes
técnicos com prioridades e objetivos, histórico consolidado e investigação com LangGraph e RAG.

## 8. Oportunidades identificadas

Vistas só pelo lado do Spock, antes da comparação:

1. Dar ao shell o contexto que falta (projeto ativo, ambiente, estado do motor num só lugar).
2. Transformar o que já existe em diários numa central de execuções navegável.
3. Tornar visível e gerenciável o que hoje é só aplicado ou dispensado (agentes, skills, regras, hooks, MCP).
4. Ligar o dado sincronizado ao conhecimento dos agentes, respeitando a política que já existe.
5. Levar a chave de modelo para a central de integrações e o cofre, abrindo espaço para mais de um provedor.
6. Extrair os padrões de página repetidos para componentes compartilhados antes de criar telas novas.
7. Validar com dado real o que já foi construído (GitHub, Jira, modelo), antes de ampliar o catálogo.

## 9. O que não foi verificado

- Nenhuma tela foi renderizada para este documento; aparência e quebras de layout vêm do código e das capturas em
  `docs/redesign/screenshots/`, que são anteriores a Gestão, Integrações e à marca Spock.
- Comportamento com serviços reais (GitHub, Jira, OpenAI) e no aplicativo Tauri empacotado.
- Peças grandes do motor (`PlanBuilder.cs`, `ApplyExecution.cs`, `SyncService.cs`, conectores) foram lidas em parte.
- Props e comportamento dos componentes do ui-kit que o aplicativo não usa: só os nomes exportados foram conferidos.
