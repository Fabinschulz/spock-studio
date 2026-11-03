# Auditoria de qualidade de código — Mariliz AI Engineering

**Data:** 2026-11-02. **Base:** `master` em `230f8e5` (PRs #1 e #2 mergeados). **Escopo:** host Tauri/Rust, UI React/TypeScript
(sem `src/prototype`, que é só referência visual de desenvolvimento), motor .NET 10, contratos e testes.

**Estado:** Onda 1 concluída (seção 5). Ondas 2 a 4 pendentes.

---

## 1. Resumo executivo

O código está em estado **bom para o estágio do produto**:

- as camadas estão respeitadas (o host não interpreta mensagens, a UI não tem regra de negócio, o .NET é a autoridade);
- os testes são densos (.NET 334, Python 33, UI 115, Rust 18);
- os nomes expressam intenção;
- há pouco código especulativo.

Os problemas reais não estão no tamanho dos arquivos. Estão em quatro pontos:

1. **Fronteiras de erro das execuções em segundo plano.** Algumas exceções escapam dos filtros, a execução nunca chega a um
   estado terminal e o projeto fica bloqueado até o motor reiniciar (P0).
2. **Três regressões que introduzi na Fase 3 e na D8.** O prazo de 60 s cobre a espera da aprovação humana. A aprovação é
   publicada antes de registrada. Na UI, o listener de eventos é ligado depois da primeira leitura (P0).
3. **Duas classes com responsabilidades demais:**
   - `WorkflowService`: 567 linhas e sete motivos de mudança;
   - `shared/schema/engine.ts`: contratos de quatro features num arquivo.

   Junto com elas, há conhecimento duplicado: ids, leitura de JSON, estados terminais, rótulos e ícones (P1/P2).
4. **Gates incompletos.** O Rust nunca passou pelo `rustfmt` (35 diferenças), o Clippy não roda na CI, e o `prettier --check`
   falha em 9 arquivos e também não roda na CI (P2).

Áreas de maior risco de manutenção:

- **`WorkflowService` (.NET):** concentra o fluxo de IA inteiro.
- **`configure-project` (UI):** 50 arquivos e nenhum teste.
- **`HarnessComposition`/`LockBuilder` (.NET):** duas formas de montar o `mariliz.lock` que já divergem.

### Resultado dos gates (executados nesta auditoria)

| Gate | Resultado |
| --- | --- |
| `dotnet build` | 0 erros; 4 avisos, todos em testes (`xUnit2029` ×2, `xUnit2031`, `CS0114` em `ProtocolJourneyTests.QueueReader.Close`) |
| `dotnet test` | 332 + 2 aprovados (rodado na Fase 4) |
| `cargo test` | 18 aprovados |
| `cargo fmt --check` | **35 diferenças** (o código nunca foi formatado; `rustfmt` e `clippy` foram instalados agora no toolchain local) |
| `cargo clippy --all-targets --all-features` | 1 aviso (`recents.rs:60`, `sort_by` → `sort_by_key`) |
| `pnpm ui:typecheck` / `ui:lint` / `ui:test` / `ui:build` | limpos / limpos / 115 / ok |
| `prettier --check` (UI) | 9 arquivos fora do padrão, todos anteriores a esta auditoria; a CI não roda |

---

## 2. Inventário

Severidade: **P0** é risco ao funcionamento ou à integridade; **P1** é problema arquitetural relevante; **P2** é
manutenibilidade; **P3** é legibilidade e padronização. Cada item foi conferido no código. Os marcados "(minha regressão)"
vieram das fases 3/D8 deste trabalho.

### 2.1 P0

| ID | Tec. | Local | Smell / princípio | Impacto | Refatoração |
| --- | --- | --- | --- | --- | --- |
| NET-P0-1 | .NET | `Workflows/WorkflowService.cs` (início de `ExecuteAsync`, prazo armado antes da aprovação) | Acoplamento temporal oculto (minha regressão) | Aprovação humana mais lenta que 60 s faz a retomada falhar como `AI_RUNTIME_TIMEOUT` | Prazos separados por trecho (início e retomada); teste com runtime falso e espera longa |
| NET-P0-2 | .NET | `WorkflowService.OpenApproval` e registro do gate | Ordem de efeitos (minha regressão) | `workflow.resume` rápido recebe `WORKFLOW_NOT_INTERRUPTED` e a execução fica parada | Registrar o gate antes de publicar `approval.required` |
| NET-P0-3 | .NET | `WorkflowService` (filtros de `catch`), `ConfigureProjectService` validate/revert, `RevertService.UndoOperations`, `CliRunner.RunAsync`, `LoopbackAiRuntime` (`JsonNode.Parse`) | Tratamento de exceção inconsistente | `JsonException`/IO/`ArgumentException` escapam: a execução não termina e `RunTracker` recusa novas execuções no projeto | Um único `RunGuard` em `Execution/` que sempre conclui a execução como `failed`; remove as escadas de `catch` duplicadas |
| NET-P0-4 | .NET | `Protocol/EngineSession.cs:93-101`, `RequestParams.Flag`, `ConfigureRequestHandler.ReadResolutions` | Fail fast fora do `try` | `id` numérico ou `v` em texto derruba o processo do motor; parâmetro mal tipado vira `INTERNAL_ERROR` | `TryGetValue` na leitura do envelope; envelope dentro do `try`; ids validados na borda |
| NET-P0-5 | .NET | `Harness/PlanSessionStore.cs:18` | Suposição falsa sobre ordem do `Dictionary` | A partir do 21º plano, o plano recém-salvo é descartado e `harness.apply` dá `PLAN_UNKNOWN` | LRU explícito (`OrderedDictionary<,>` do .NET 9+) e teste com 25 planos |
| UI-P0-1 | UI | `configure-project/steps/run-step/run-step.tsx:21-24`, `hooks/use-configure-journey.ts` (`cancelRun`) | Promise rejeitada sem tratamento | Se `run.cancel` falha, o botão fica em "Cancelando…" para sempre e a pessoa não consegue cancelar uma aplicação | `try/finally` no cancelamento, mensagem de erro e teste |
| UI-P0-2 | UI | `execution/hooks/use-run-follow.ts` | Corrida e rejeição sem tratamento (minha regressão) | O listener liga depois da primeira leitura. Se o evento perdido for o terminal, a página fica parada; falha do `listen` é silenciosa | Aguardar o listener antes do primeiro `refresh()` e tratar a falha |

### 2.2 P1

| ID | Tec. | Local | Smell / princípio | Refatoração |
| --- | --- | --- | --- | --- |
| NET-P1-1 | .NET | `Workflows/WorkflowService.cs` (567 linhas) | God class (SRP), sete motivos de mudança: validação do pedido, requisição v2, aprovação, tradução de eventos, diário, contexto e conhecimento, saneamento do resultado | Extrair `WorkflowInput`, `WorkflowRequests`, `AiEventTranslator`, `WorkflowResultSanitizer` e `WorkflowContext` em `Workflows/`; o serviço fica com o fluxo |
| NET-P1-2 | .NET | Construtor de `WorkflowService` (8 parâmetros, 4 opcionais nulos) | Dependências ocultas; uso de `store!` depois de `store?.` | Dependências obrigatórias; a ausência de descoberta e de índice vira configuração explícita |
| NET-P1-3 | .NET | `Discovery/DotnetDetector.cs:51` | Ordenação textual de TFM | `netstandard2.0` ou `net9.0` "vencem" `net10.0` e o valor errado vai para a regra gerada no projeto. Corrigir comparando família + versão, com teste |
| NET-P1-4 | .NET | `Harness/LockBuilder.cs` `Build` × `BuildFromOutcomes` | Código duplicado que já divergiu (versão da especificação, entradas anteriores) | Um montador só, alimentado pelas duas origens; teste de que o lock previsto é igual ao gravado |
| NET-P1-5 | .NET | `Infrastructure/LoopbackAiRuntime.EnsureReadyAsync` | Condição de corrida | Duas execuções podem derrubar o processo Python uma da outra. Serializar a subida com `SemaphoreSlim` |
| NET-P1-6 | .NET | `Runtime/Program.cs` × `Cli/Program.cs` | Raiz de composição duplicada que diverge (a CLI não recebe `IModelSettings`) | Uma fábrica de composição no Infrastructure usada pelos dois e pelo `Journey` dos testes |
| NET-P1-7 | .NET | `run.snapshot` e `run.cancel` dentro de `ConfigureProjectService` | Responsabilidade na camada errada (são da execução, não da jornada de configuração) | `RunQueries` próprio, usado pelos dois handlers |
| UI-P1-1 | UI | `shared/schema/engine.ts` (279 linhas) | SRP: contratos de configuração, workflow, lista de execuções e modelo no mesmo módulo | Dividir em `shared/schema/{run,harness,workflow,model}.ts` |
| UI-P1-2 | UI | `run.snapshot` com dois schemas; estados terminais em três lugares; rótulos de estado em dois | Fonte única violada (DRY) | `shared/schema/run.ts` com o snapshot base e `TERMINAL_STATES`; rótulos num só lugar |
| UI-P1-3 | UI | `configure-project/service/harness-api.ts` (`waitForRun`) e `shared/api/engine-client.ts` | Laço sem fim se o retrato terminal vier sem resultado; cancelamento só checado entre esperas; `invoke` sem timeout | Falhar no retrato terminal sem resultado, espera cancelável e timeout opcional no cliente |
| UI-P1-4 | UI | `project-selection/domain/tauri-project-picker.ts:27` | Cast sem validação (`pick` não usa o schema que `openRecent` usa) | `selectedSchema.nullable().parse` |
| UI-P1-5 | UI | `execution-page.tsx` e `settings/model-settings-page.tsx` | Acesso a dados dentro do componente; três blocos `try/catch` copiados | `service/` por feature, como em `configure-project` e `engineering-request` |
| UI-P1-6 | UI | `proposal-guide.tsx:39` | "Gerar o plano de novo" liberado com reversão ou validação em andamento | Bloquear enquanto houver execução |
| UI-P1-7 | UI | `configure-journey` → `ProjectStep` (14 props) → `FolderPickerCard` (11) | Prop drilling de um estado que já está num contexto | `FolderPickerCard` lê o contexto; `ProjectChoice` some |
| RS-P1-1 | Rust | `recents.rs` `project_id` | Dependência de algoritmo instável (`DefaultHasher` pode mudar entre versões do Rust) | Depois de atualizar o toolchain, os ids gravados não batem: recentes duplicados e `debug_assert` falhando. Usar um hash estável (FNV-1a) |

### 2.3 P2

| ID | Tec. | Local | Problema | Refatoração |
| --- | --- | --- | --- | --- |
| NET-P2-1 | .NET | `ApplyExecution` (13 parâmetros), `ValidationService.RunAsync` (8), `SpecificationPlanner.Plan` (listas de saída mutáveis) | Long Parameter List | `ApplyContext`, `ValidationRequest` e resultado tipado do planner |
| NET-P2-2 | .NET | `IModelSettings` atravessa cinco classes só para o juiz que devolve `NotRun` | Tramp data e generalidade especulativa | Injetar só no `ModelJudgeCheck` |
| NET-P2-3 | .NET | `WorkspaceAuthorizer._authorized` (`Dictionary` lido de threads de execução) | Thread safety | `ConcurrentDictionary` |
| NET-P2-4 | .NET | `RunTracker._runs` nunca podado (500 eventos e um CTS por execução) | Vazamento de memória lento | Descartar execuções terminais antigas; o snapshot já recorre ao disco |
| NET-P2-5 | .NET | Validação de id em `WorkspaceAuthorizer`, `WorkflowService.SafeProjectId`, `FileRunStore.Safe`, `LoopbackAiRuntime.IsInterruptId` | Conhecimento duplicado | `Core/Text/Ids` com as regras de `p-`, `run-`, `ex-`, `int-`, `apr-` |
| NET-P2-6 | .NET | `Text(JsonObject,string)` em três arquivos, mais variantes em `EngineSession` e `CliRender` | Duplicação | Uma extensão `JsonObject.Text(name)` |
| NET-P2-7 | .NET | Prefixos gerenciados repetidos em `ManagedScope`, `EnvironmentCheck`, `LocalProjectFiles` e `SpecificationPlanner`; pastas ignoradas em `LocalProjectFiles` e `KnowledgeSources` | Shotgun surgery | `ManagedScope` e `Core/Policy` como fonte única |
| NET-P2-8 | .NET | Resolução do Python em `SystemProbe`, `PythonRuntimeLayout` e testes, em ordens diferentes | Duplicação com comportamento divergente | O hook e o runtime podem usar interpretadores diferentes. Um localizador só |
| NET-P2-9 | .NET | Códigos de erro literais (`PROJECT_NOT_AUTHORIZED` 3×, `AI_RUNTIME_INVALID_RESPONSE` 9×); mapeamento de `WorkspaceError` duplicado entre motor e CLI | Primitive obsession | Catálogo `EngineErrors` com código e mensagem |
| NET-P2-10 | .NET | `RunJournal` com campos de harness e de workflow e dois vocabulários em `Evals.Status` | Classe com dois papéis | Por ora, tipar o status; separar só se crescer |
| NET-P2-11 | .NET | `knowledgeRoot = dataRoot` (o índice e o checkpoint recebem a pasta que também guarda `model.json`) | Menor privilégio por caminho. O processo é do mesmo usuário, então não é uma fronteira real; por isso P2 | Passar `<dados>/projects` como raiz da inteligência |
| UI-P2-1 | UI | `use-configure-journey.ts` | Hook com três papéis (casos de uso, acompanhamento da execução, navegação); `latest` sincronizado por `useEffect` | Extrair `useRunTracker`; testes do reducer antes |
| UI-P2-2 | UI | Copiar para a área de transferência em três lugares | DRY | Usar o hook existente com `resetAfterMs` |
| UI-P2-3 | UI | Quatro conjuntos de ícones (glyphs da feature, SVG inline, `Glyph`, `FolderIcon`/`CheckGlyph`) | Duplicação e sobreposição com o Synthra | Ícones do Synthra mais um registro único |
| UI-P2-4 | UI | `InlineNotice` × Synthra `Alert` usados para o mesmo fim | Duplicação de componente | Padronizar no `Alert` |
| UI-P2-5 | UI | `RecentProjects` × `RecentShortcuts` | Quase duplicados; marcam "atual" pelo nome de exibição | Botão compartilhado comparando ids |
| UI-P2-6 | UI | `use-engine-status.ts` | Parser manual com `as`; listener vaza se desmontar antes do `listen` | Zod e o mesmo padrão de cancelamento do `use-run-follow` |
| UI-P2-7 | UI | `app/routes.tsx` repete os segmentos de `shared/routing/paths.ts` | Fonte única violada | Rotas montadas a partir de `paths` |
| UI-P2-8 | UI | Execução e Configurações sem guarda de host | Erro genérico no navegador | Guarda de rota `RequiresHost` |
| UI-P2-9 | UI | Props sem uso (`ActionCard.highlight`, `InlineNotice.onDismiss`, `SectionCard.tone`, `PageHeader.actions/lead`, `CommandDisplay.description`) | YAGNI | Remover |
| UI-P2-10 | UI | `configure-project`: nenhum teste | Área sem rede de segurança | Testes de caracterização do reducer, approvals, run-events, run-outcome e cancelamento antes de mexer |
| RS-P2-1 | Rust | `.lock().unwrap()` em 16 lugares | `unwrap` em fluxo normal (mutex envenenado derruba o host) | Função `lock()` que recupera do envenenamento |
| RS-P2-2 | Rust | `engine.rs` mistura ciclo de vida do processo, correlação de pedidos, localização dos recursos e composição do status para a UI | SRP leve | `resources.rs` (recursos) e `status.rs` (status); `engine.rs` fica com processo e pedidos |
| RS-P2-3 | Rust | `lib.rs` define `Recents` (estado + gravação) junto dos comandos | Camada de comandos com estado | Mover para `recents.rs`; `lib.rs` fica com composição e comandos finos |
| GATE-P2-1 | Rust/UI | `rustfmt` nunca aplicado; Clippy e `prettier --check` fora da CI | Gates incompletos | Formatar, corrigir o aviso do Clippy e incluir `cargo fmt --check`, `cargo clippy -D warnings` e `prettier --check` na CI |

### 2.4 P3

- **Código morto (.NET):**
  - `RunStates.Analyzing`;
  - `OpStatus.RolledBack/Skipped/Failed`;
  - `LimitationCodes.JudgeNotConfigured`;
  - `RunTracker.WaitForAllAsync`;
  - `ManagedLockFile.TryParse` (duplica `Read`);
  - sobrecarga de `PlanBuilder.New` e o parâmetro `bool _`;
  - `_ = normalized` em `LocalProjectFiles.WriteAtomic`.

  Também há um comentário incorreto em `PlanBuilder.Normalize`.
- **Código morto (UI):**
  - `HarnessApi.runs`, `runsListSchema` e `RunSummary`;
  - `labels.runStateLabel` e `stepStatusTone`;
  - `MAIN_CONTENT_ID`;
  - exportações de barril sem consumidor;
  - cerca de 16 tipos exportados sem importador;
  - o rótulo `analyzing`.
- **Duplicações menores:**
  - configuração do YamlDotNet em `ManagedLockFile` e `DeclaredConfig`;
  - `ConfigureProjectService.ToJson` × `ResultJson.Node`;
  - validações repetidas em `ProposalReader`;
  - `HasNothingToApply` × `ConfigurationPlan.IsEmpty`.
- **Strings no lugar de tipos:**
  - modos de arquivo `file`/`merge`/`kept` em cinco arquivos;
  - status do workflow como literais;
  - `RunView.state` e `status` como `string` na UI.
- **Organização (.NET):**
  - `Interfaces/` e `Enums/` usados de forma inconsistente;
  - `Models/ModelContracts.cs` com cinco tipos;
  - `Ports/SystemClock.cs` (implementação concreta numa pasta de portas);
  - `WorkspaceAuthorizer` usa `Directory`/`Path` em vez da sua porta.
- **Testes .NET:** os 4 avisos do analisador e o `TestSupport.Journey`, que é uma terceira cópia da composição.
- **UI:**
  - ternário aninhado em `stepStatusLabel`;
  - casts `as never` e `!`;
  - chaves de lista por texto em `EvidenceDisclosure`;
  - `panels.module.scss` (790 linhas, importado por 30 arquivos);
  - controles nativos onde o Synthra tem equivalente. Este item precisa de checagem visual antes de mudar.
- **Rust:**
  - `compose_status` recebe `&str` em vez do enum `Status`;
  - `shutdown` faz espera ativa (aceitável no encerramento).

### 2.5 Avaliado e mantido (não é smell)

- `HarnessComposition` (235 linhas), `ProposalReader` (209) e `journey-reducer.ts`: longos, mas coesos. Cada um tem um motivo de mudança.
- `catch (Exception)` em `EvalRunner` (converte em `Blocked` de propósito) e no `Program.cs` (topo do processo).
- `WorkflowRuntimeTests.cs` (733 linhas): é a rede de segurança do fluxo de IA. Dividir só depois do NET-P1-1.
- Fronteiras entre camadas: nada de regra de negócio no Rust nem na UI, e nenhuma dependência circular entre projetos .NET.

---

## 3. Arquitetura preservada

Nenhuma refatoração deste plano move responsabilidade entre Tauri, React, .NET e Python. Também não muda contratos do
protocolo nem schemas em `contracts/`. Os goldens precisam continuar idênticos.

---

## 4. Plano de refatoração

| Onda | Escopo | Itens | Testes novos ou de caracterização | Validação |
| --- | --- | --- | --- | --- |
| **1. Correções e gates** | .NET, UI e Rust | Todos os P0, mais NET-P1-3 (TFM), NET-P1-5 (subida do Python), UI-P1-3 (laço sem fim), UI-P1-4, RS-P1-1 e GATE-P2-1 | Um teste por P0 que falha antes e passa depois: aprovação longa, retomada imediata, `JsonException` no stream, `id` numérico no protocolo, 25 planos, cancelamento rejeitado, evento antes do listener, TFM misto, hash estável | Todos os gates da seção 1, agora incluindo `cargo fmt --check`, Clippy sem avisos e Prettier |
| **2. SRP nas peças centrais** | .NET e UI | NET-P1-1, P1-2, P1-4, P1-6, P1-7; UI-P1-1, P1-2, P1-5, P1-7; UI-P2-10 (testes do configure-project antes) | Caracterização do `configure-project`; teste de lock previsto = lock gravado | `WorkflowRuntimeTests` e goldens intactos |
| **3. Duplicação e organização** | todas | NET-P2-1…P2-11, UI-P2-1…P2-9, RS-P2-1…P2-3 | Os existentes, mais os que cada item pedir | Gates completos |
| **4. Limpeza** | todas | P3 | — | Gates completos |

Cada onda vira commits pequenos (um tema por commit) e um PR próprio.

---

## 5. Resultado da Onda 1 (2026-11-02)

Cada correção ganhou um teste que reproduz o defeito. Para NET-P0-1 e NET-P0-2, reintroduzi o código antigo temporariamente
para confirmar que os testes novos falham com ele.

| Item | O que mudou | Teste |
| --- | --- | --- |
| NET-P0-1 | Cada conversa com o runtime tem o próprio prazo (`WithDeadlineAsync`); a espera humana não tem prazo, só o cancelamento a encerra. O prazo é configurável para teste | `WorkflowRobustnessTests.Aprovacao_mais_lenta_que_o_prazo_do_runtime_ainda_conclui` |
| NET-P0-2 | A aprovação é registrada antes de `approval.required` ser publicado (`WaitForApprovalAsync`) | `Retomada_disparada_pelo_proprio_evento_de_aprovacao_encontra_a_aprovacao` |
| NET-P0-3 | `RunHandle.Start` garante que toda execução termina (exceção não prevista vira `failed` com o tipo, sem a mensagem). Apply, validate, revert e workflow passam por ela. No workflow, uma única `GuardedAsync` substitui os dois blocos de `catch` duplicados e passa a cobrir `JsonException` e exceções imprevistas | `Erro_imprevisto_do_runtime_termina_a_execucao_e_libera_o_projeto` (2 casos), `Guarda_da_execucao_conclui_como_falha_o_que_escapar_do_servico` |
| NET-P0-4 | Envelope lido com `TryGetValue`; erro de qualquer pedido vira resposta de erro e nunca encerra a sessão; `Flag` e `resolutions` com tipo errado viram `INVALID_PARAMS` | `ProtocolRobustnessTests` (6 casos) |
| NET-P0-5 | `PlanSessionStore` usa `OrderedDictionary` com remoção explícita do mais antigo | `PlanSessionStoreTests` (25 planos; plano salvo de novo) |
| NET-P1-3 | `DotnetDetector.Newest` compara família (.NET > .NET Core > .NET Standard > .NET Framework) e versão; ignora sufixo de plataforma | 7 casos, entre eles `net472` × `net48` (o teste pegou um erro meu na primeira versão) |
| NET-P1-5 | Subida do Python serializada com `SemaphoreSlim` e nova checagem de saúde dentro da seção | coberto pelos testes de workflow existentes (sem teste dedicado: a corrida não é observável sem instrumentar o processo) |
| UI-P0-1 | Cancelamento que falha devolve o botão e mostra o motivo | `run-step.test.tsx` (2) |
| UI-P0-2 | A escuta é ligada e aguardada antes do primeiro retrato; falha da escuta aparece para a pessoa | `execution-page.test.tsx` (ordem `listen` → `run.snapshot`) |
| UI-P1-3 | `waitForRun` falha se a execução terminar sem resultado; a espera é interrompida na hora pelo `AbortSignal` | `harness-api.test.ts` (2) |
| UI-P1-3 (timeout do cliente) | **Não procede.** Todo `engine_request` passa pelo host, que já impõe 120 s (`REQUEST_TIMEOUT`) e devolve `ENGINE_TIMEOUT`; um segundo timeout na UI seria redundante | — |
| UI-P1-4 | `pick` valida a resposta do host com o mesmo schema de `openRecent` | `tauri-project-picker.test.ts` (2) |
| RS-P1-1 | `project_id` usa FNV-1a de 64 bits; a leitura recalcula os ids a partir do caminho, então listas gravadas por versões antigas se corrigem sem duplicar | `o_id_nao_depende_da_versao_do_rust`, `lista_gravada_com_ids_antigos_e_corrigida_na_leitura_sem_duplicar` |
| RS-P2-1 | `lock()` recupera de mutex envenenado em vez de `unwrap` (16 lugares) | testes do host |
| GATE-P2-1 | Rust formatado (`cargo fmt`); aviso do Clippy corrigido; 9 arquivos da UI formatados; CI roda `format:check` da UI e `cargo fmt --check` + `cargo clippy -D warnings` (Linux) | — |
| Avisos do .NET | Os 4 avisos de analisador nos testes foram corrigidos: o build fica com 0 avisos | — |

**Gates depois da Onda 1:**

| Gate | Resultado |
| --- | --- |
| `dotnet build --no-incremental` | 0 avisos, 0 erros |
| `dotnet test` | 352 + 2 |
| Python | 33 |
| `cargo fmt --check`, `cargo clippy -D warnings` | limpos |
| `cargo test` | 20 |
| UI (`pnpm check`: typecheck, ESLint, Prettier, testes) | limpo; 122 testes |
| `pnpm build` | ok |
