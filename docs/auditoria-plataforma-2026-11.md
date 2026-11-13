# Auditoria de qualidade — código da plataforma (fases A–G) — 2026-11-13

Escopo: tudo o que entrou depois da auditoria arquitetural de 2026-11-06 (`docs/auditoria-arquitetural-2026-11.md`, base `bf43f8b`): 530 arquivos, ~50 mil linhas (PRs #40–#56). Stacks: runtime .NET 10, host Tauri/Rust, UI React/TypeScript, `intelligence/` (Python/LangGraph), `contracts/`, `scripts/`, CI.
Método: leitura do código por stack (quatro leituras independentes, somente leitura) + gates existentes executados. Nada foi alterado nesta fase.

**Como ler a confiança.** `V` = reverificado por mim no código depois da leitura. `L` = confirmado pela leitura do agente, não reverificado. Os achados `V` são os que sustentam a priorização; os `L` entram no plano mas devem ser relidos antes de mexer.

## 1. Resumo executivo

O código continua em bom estado nas fronteiras: Core → Application → Infrastructure sem inversão, o host Rust não tem regra do motor nem `unwrap` em caminho de execução, a UI não calcula risco/previsão/métrica, o Python só orquestra. Não há P0.

**Linha de base (todos os gates passam):**

| Stack | Gate | Resultado |
|---|---|---|
| .NET | `dotnet build` / `dotnet test` | 0 avisos, 0 erros; 1062 passam, 11 ignorados |
| Rust | `cargo fmt --check` / `clippy -D warnings` / `cargo test` | limpo |
| React | `pnpm check` (tsc, eslint, prettier, vitest) / `pnpm build` | passam; build avisa de chunk > 500 kB |
| Python | `unittest discover -s tests -p "test_*.py"` / `ruff check` | 167 passam; ruff limpo |

Os problemas novos não estão no tamanho dos arquivos (`EngineSession` tem 276 linhas e delega a 11 handlers; `knowledge-section`, `decisions-section`, `ProjectAudit` e `ProjectTimeline` são grandes e coesos). Estão em seis pontos:

1. **O teto de rodadas da investigação não vale** (Python deixa chegar à rodada 5; o contrato diz 4) e o .NET mascara com `Math.Clamp`.
2. **Hierarquia de timeouts invertida** entre host (120 s), motor (150 s) e transporte do modelo (HttpClient de 15 s abaixo do prazo de chat de 30 s).
3. **Perda silenciosa de dados em stores de arquivo** (erro de leitura vira "vazio" e a próxima gravação apaga tudo) e **evidência cortada sem aviso** (assistente do portfólio, bloco de fatos da investigação).
4. **Laço serial do motor**: um pedido de rede longo (investigação de até 150 s) impede cancelar, aprovar ou consultar saúde.
5. **Conhecimento duplicado entre camadas e entre features**: vocabulário de execução na UI (3 cópias divergentes), catálogo do Harness (3 cópias da regra de stack), regras de risco/previsão (limiares diferentes para o mesmo fenômeno), grupos de fatos no Python.
6. **UI: leitura de dados fragmentada** (8 implementações do mesmo ciclo carregando/erro/pronto; nenhum `ResourceView` tem "Tentar de novo") e controles nativos onde o Synthra tem equivalente.

## 2. Inventário

Severidade: P0 funcionamento/segurança/integridade; P1 arquitetural relevante; P2 manutenibilidade; P3 legibilidade. Risco = risco de alterar.

### 2.1 .NET

| ID | Local | Problema | Princípio | Sev | Risco | Conf. |
|---|---|---|---|---|---|---|
| NET-E1 | Protocol/EngineSession.cs:103-114 | Laço serial: `await HandleAsync` por linha; `connection.*`, `integration.link`, `model.*`, `management.ask/investigate` seguram o motor até 150 s e bloqueiam `run.cancel`, `workflow.resume`, `runtime.health`, `engine.shutdown` | SRP/disponibilidade | P1 | médio | V (laço) |
| NET-E2 | Infrastructure/Integrations/FileIntegrationStore.cs:42-54; FileSyncedDataStore.cs:156-165; FileRiskHistoryStore.cs:24-31; FileWorkspaceRegistry | `catch (JsonException or IOException)` devolve vazio e o próximo `Save*` grava por cima: trava passageira de arquivo apaga todas as conexões (segredos órfãos no cofre) | Integridade, tratamento de erro | P1 | baixo | L |
| NET-E3 | Infrastructure/FileRunStore.cs:28-46,66-75 | Diário e links gravados sem lock e lidos sem lock; `File.Move` pode falhar sob leitura concorrente no Windows e marcar o run como "Falha interna" | Concorrência | P1 | baixo | L |
| NET-E4 | Infrastructure/HttpsModelTransport.cs:28; Models/ModelGateway.cs:34,206 | `HttpClient.Timeout = 15 s` limita o `ChatTimeout` de 30 s: o prazo real do chat é 15 s; respostas longas (até 8192 tokens) viram `MODEL_TIMEOUT` | Config espalhada | P1 | baixo | V |
| NET-E5 | Infrastructure/FileModelSettings.cs:36-42 | Chave do modelo em texto puro com permissão padrão no fallback (Linux sem keyring) | Segurança | P1 | baixo | L |
| NET-M1 | Management/Assistant/PortfolioAssistant.cs:77-91 | `if (own.Count >= MaxFactsPerProject) break;` corta fatos (riscos podem sumir, inclusive `risk.none`) sem registrar em `withheld`; `Withheld.Skip(1)` presume que o 1º aviso é o geral | Contrato implícito, integridade da evidência | P1 | baixo | V |
| NET-M11 | Management/RiskHistory.cs:74,112,146 | `Rank[severity]` lança `KeyNotFoundException` para valor desconhecido (arquivo antigo/editado); o `Collect` do Timeline não captura e a linha do tempo inteira cai | Primitive obsession, fail-fast mal posto | P1 | baixo | V |
| NET-M2 | Management/Investigation/InvestigationService.cs | 10 parâmetros (5 opcionais) e seis motivos para mudar: validação, montagem do request, memória com I/O, fatos de run/mudança, parsing do ack `JsonObject`, diário. O ack não passa por `AiWire` | SRP, DIP | P1 | médio | L |
| NET-E14 | Execution/RunQueries.cs (`Visible`) | `catalog?.CanAccess(...) ?? true`: a autorização do projeto vira opcional por construtor | Fail-open | P2 | baixo | L |
| NET-E6 | Integrations/IntegrationHub.cs + 8 consumidores | Hub usado como service locator (`.Projects`, `.Audit`); constrói 5 serviços com `new`; `VaultAvailable` congelado | DIP/ISP | P2 | médio | L |
| NET-E7 | Harness/HarnessComposition.cs:181-190; TechnologyCatalog.cs:323-327; Validation/Checks/RequirementsCheck.cs:31-33; HarnessRevisionService.cs:16-29 | Regra "stack → componente" e "caminho → componente" em 3-4 cópias; uma stack nova entra no plano e fica fora da validação | DRY (regra real) | P2 | médio | L |
| NET-E8 | Protocol/EngineSession.cs:43-96,173-201 | Construtor com 23 opcionais; handlers criados com `new` no ctor; despacho por 11 `when`; capacidades anunciadas que falham na chamada; `StringList` e `StrList` com regras diferentes | OCP, hidden deps | P2 | médio | L |
| NET-M4 | ManagementAssistant.cs:77-167; PortfolioAssistant.cs:98-154; InvestigationService.cs:318-455 | Duplicação real: montagem de `AssistantAnswer` (13 args), diário da pergunta, validação do tamanho da pergunta, instruções quase idênticas; `PortfolioAssistant` grava o mesmo `ModelUsage` em cada projeto (uso contado N vezes) | DRY | P2 | baixo-médio | L |
| NET-M3 | Timeline ↔ Management.Investigation | Ciclo entre pastas de feature | Acoplamento | P2 | baixo | L |
| NET-M5 | ManagementService.cs:160-167; ManagementAssistant.cs:45-51; ProjectReport.cs:62-69; ProjectTimeline.Read | `Sources()` e o arquivo sincronizado relidos 4× por pacote; `Timeline.Read` roda as 6 fontes mesmo com `kinds` filtrado; dados e `now` podem variar entre as leituras | Eficiência, consistência | P2 | médio | L |
| NET-M6 | Audit/ProjectAudit.cs:149-216 | `Describe` não conhece 6 ações gravadas (`management.assistant_answered` etc.): aparecem como "Operação registrada: <nome técnico>" | Contrato leitor/escritor por string | P2 | baixo | L |
| NET-M7 | ProjectTimeline.cs:82-113; ProjectAudit.cs:84-114; Execution/RunCatalog.cs | Paginação por `before` com regra de empate copiada 3× | DRY | P2 | baixo | L |
| NET-M8 | ManagementService.cs:135-147; ForecastCalculator.cs:137-153; RiskRules.cs:109-141 | "Entregue na janela" em 3 cópias; `ForecastCalculator.RiskFactors` e `RiskRules` com limiares/janelas diferentes (20% vs 50%): a tela pode mostrar fator sem risco, ou o contrário | DRY de regra, consistência | P2 | médio | L |
| NET-M9 | ProjectTimeline.cs:51-59; ProjectAudit.cs:67; InvestigationService.cs:40-50; ManagementService.cs:13 | Dependências opcionais só para testes: ramos mortos em produção e 8 `!` em closures | Hidden deps | P2 | médio | L |
| NET-M10 | Models/ModelGateway.cs, ModelContracts.cs, ModelBroker.cs | `ModelGateway` com 5 papéis; `Save`, `CompleteAsync`/`model.complete`, `CostUsd`, `Provider/DefaultModel/Endpoint` só usados por testes; CTS+`MODEL_TIMEOUT` copiado 3×; propósitos duplicados em `ModelBroker.Purposes` e `ModelPurposes` | SRP, código morto | P2 | médio | L |
| NET-M12 | Decisions.cs, ProjectAudit.cs, PortfolioAssistant.cs, InvestigationService.cs, ModelGateway.cs | 3 estilos de erro (`EngineErrors.*`, `IntegrationErrors.*`, `new EngineException("LITERAL")` em ~20 pontos); `DecisionService.List` devolve `JsonArray` | Tratamento de erro inconsistente | P2 | baixo | L |
| NET-E9 | Execution/RunCatalog.cs; RunLinkService.cs | Execution ↔ Protocol (ciclo); `RunCatalog` mistura paginação, projeção JSON e fachada de links; `RunLinkService` conhece o formato da chave de item; 3 tipos no mesmo arquivo | SRP, camadas | P2 | baixo | L |
| NET-E10 | `IRunStore.ListJournals` (10 chamadores); `RunLinkService.ForProject:161`; `IntegrationRequestHandler:65-85`; `ProjectKnowledgeService:109,139` | Varreduras e `Build` repetidos | Eficiência | P2 | baixo | L |
| NET-E11 | Integrations/SyncService.cs:150-165,246-262 | TOCTOU em `Reconciled` pode sobrescrever `Succeeded` por `SYNC_INTERRUPTED`; falha após `activity.End` deixa a Task sem observador e `sync.finished` nunca sai | Concorrência | P2 | baixo | L |
| NET-E12 | GitHubConnector.cs:57-71; JiraConnector.cs:88-97 | Paginação por `page=N` com `since` pode pular item editado durante a sync; JQL relativo recalculado a cada página | Correção de integração | P2 | médio | L |
| NET-E13/M11 | DesiredFile.Mode, status de Eval, `Probe()`, 71 `new EngineException("COD")` | Estados e códigos como string em vários arquivos; `"Passed"` literal em 3 lugares | Primitive obsession | P2 | baixo-médio | L |
| NET-E15/16/17 | LocalModelSettings; ConnectionService; FileAuditLog; Engineering/AgentPolicy.cs | Subprocessos do cofre sob lock; `FileAuditLog.ReadAll` carrega o arquivo inteiro sob o gate de escrita; política lida do fonte Python por regex | Eficiência/robustez | P2 | baixo-médio | L |
| NET-P3 | vários | Pluralização 5×; `Day()` 3× (uma morta); tupla `(ProjectIntegration, SyncedData, ConnectionStatus)` em 4 assinaturas; delegate de evento em 8 assinaturas; `HttpsModelTransport` segue redirect com `x-api-key`; regra de captura de risco em `Program.cs:41-44`; pendências NET-10/12/13 da auditoria anterior; fakes de teste copiados por arquivo | — | P3 | baixo | L |

### 2.2 Python, Rust, contratos, scripts, CI

| ID | Local | Problema | Princípio | Sev | Risco | Conf. |
|---|---|---|---|---|---|---|
| CT-N1 | workflows/investigation/nodes.py:142-169,172-208; graph.py:118-126; schema `ai.investigation.ack.v1` | `decide` só confere `round >= MAX_ROUNDS` antes de despachar; round 3 com busca e grupos → `retrieve` (4) → `gather` (5). O ack viola o schema (`maximum: 4`); o .NET esconde com `Math.Clamp`, e o `rounds` do diário mente. Não há teste do ack contra os limites do schema | Design by contract | P1 | baixo | V |
| PY-N1 | nodes.py:184-197; engineering/nodes.py:59-64; knowledge/index.py:257-266 | Só `KnowledgeError` degrada; `sqlite3.DatabaseError` e `OSError` escapam do nó e a execução termina sem ack (índice corrompido quebra todo run do projeto) | Falha de recurso opcional deve degradar | P1 | baixo | L |
| RS-N1 | engine.rs:29,233 (120 s para todo método); InvestigationService.cs (prazo 150 s); investigation/graph.py:37 (150000 ms) | Timeout externo menor que o interno: investigação legítima de 120–150 s mostra `ENGINE_TIMEOUT` enquanto o motor termina e grava; o usuário repete e paga os tokens duas vezes | Config espalhada | P1 | baixo | V |
| PY-N2 | memory/execution.py:47-54 | Lock por arquivo de checkpoint cobre a execução inteira do grafo (inclui chamadas de modelo); `run_cancel` espera o fim do run | Escopo do lock | P2 | médio | L |
| PY-N3 | knowledge/retrieval.py:33-35; index.py:269-312 | Resíduo do N+1: cada trecho abre conexão nova e roda 9 DDLs com commit (até ~42 conexões por busca) | Eficiência | P2 | baixo | L |
| PY-N4 | nodes.py:28-44; evaluations/management.py:75-80 | Vocabulário de grupos de fatos (`riscos`, `execucoes`...) embutido no Python sem contrato; renomear no .NET degrada em silêncio | Regra do motor no Python | P2 | médio | L |
| PY-N5 | agents/investigation.py:61-72; prompts.py:49-70 | Bloco de fatos truncado em 12000 caracteres sem limitação; `known` inclui fatos que o modelo não viu | Integridade da evidência | P2 | baixo | L |
| PY-N6/N7/N8/N9 | contracts/*.py, workflows/*, agents/* | Campos do contrato lidos e nunca usados; `workflowId/Version` não conferidos; imports de privados entre pacotes; regex de id e `_guard` repetidos; estado morto; dicts sem tipo (`Hit` com 12 chaves) | DRY, encapsulamento | P3 | baixo | L |
| CT-N2 | ai.event.v1, ai.agent.result.v1 | Citam `ai.ack.v2` inexistente; `ai.agent.result.v1` sem produtor/consumidor; regra de `question` diferente entre schema e Python | Deriva de contrato | P3 | baixo | L |
| RS-N2/N3 | lib.rs:91-94; recents.rs:180-190; protocol.rs:187-208; engine.rs:204 | Comando síncrono com E/S na thread principal e mutex segurado durante E/S; `params.cloned()` de até 4 MB e serialização dupla; linha > 4 MB descartada e o pedido só falha por timeout; comentário de `FRONTEND_METHODS` defasado | Rust API guidelines | P3 | baixo | L |
| SC-N1/N2 | ci.yml; build-runtime.ps1; evaluations/management.py | `dotnet publish` duplicado entre CI e script; `rust-toolchain@stable` flutuante com `clippy -D warnings`; `cargo test` sem `--locked`; testes Python só em ubuntu; `numbers()` do eval copia `AnswerVerifier.Numbers` sem fixture comum | Higiene | P3 | baixo | L |

### 2.3 React / TypeScript

| ID | Local | Problema | Princípio | Sev | Risco | Conf. |
|---|---|---|---|---|---|---|
| UI-N1 | shared/hooks/use-async-resource.ts; integrations/hooks/use-reloadable.ts; runs/hooks/use-run-list.ts; project/components/{harness,knowledge,profile,revisions}-section.tsx | 8 implementações do mesmo ciclo carregando/erro/pronto; `useAsyncResource` não tem `reload`, e **nenhum dos 12 `<ResourceView>` em 11 arquivos passa `onRetry`** (todas as telas de gestão e o catálogo ficam sem "Tentar de novo") | DRY, fonte única (regressão da UI-9) | P1 | médio | V |
| UI-N4 | runs/domain/run-labels.ts; project/domain/run-labels.ts; configure-project/domain/labels.ts; execution/domain/labels.ts | `runKindLabel` em 3 cópias com textos diferentes; `effectLabel` em 2 (+1 mapa); tom de `running/validating` diverge entre `STATE_TONE` e `stateTone`; rótulo de passo diverge entre telas; a Visão geral lê `harness.runs` e a central `run.list` | DRY de vocabulário | P2 | baixo | V (runKindLabel) |
| UI-N2 | management/management-page.tsx:55-95; project/project-page.tsx:41-47 | Cascata: 4 seções só montam depois do `Promise.all` do topo; o perfil só depois de discovery e runs | Eficiência | P2 | baixo | L |
| UI-N3 | project/components/profile-section.tsx:27-38 | Leitura que escreve: `readProfile ?? refreshProfile` em `useEffect` chama `engineering.analyze`; roda 2× em StrictMode | Efeito colateral em leitura | P2 | baixo | L |
| UI-N5 | integrations/domain/format-date.ts e outros | Utilitários compartilhados vivem na feature `integrations` (7 features importam dela só para formatar data); 5 formatadores de data, `plural` em 5 arquivos, `KIND_LABEL`/`PROVIDER_LABEL`/`CATEGORY_TONE` duplicados; `connectorName` morto | DRY, camadas | P2 | baixo | L |
| UI-N6 | timeline/timeline-page.tsx vs audit/audit-page.tsx | Mesma página copiada (~80% do SCSS igual; filtros e "Ver mais" em `runs-page`) | DRY | P2 | baixo | L |
| UI-N7 | management/assistant-page.tsx vs components/portfolio-ask.tsx | Formulário de pergunta e fluxo `busy/error/needsKey` copiados; padrão busy/try/finally em 11 sites | DRY | P2 | baixo | L |
| UI-N8 | runs-page.tsx (3), report-page.tsx, model-settings-page.tsx (2), decisions-section.tsx, run-links.tsx, choice-field.tsx | Controles nativos onde o Synthra exporta `Select`, `TextField`, `Checkbox` | Reuso da biblioteca (decisão já tomada) | P2 | médio | L |
| UI-N9 | runs/management/project/integrations `*.module.scss`; model.module.scss | Blocos `.rows/.row/.rowTitle/.plainList/.mono` idênticos em 4 arquivos; tokens inexistentes (`--color-surface-muted`, `--color-warning-text`: bug visual no tema escuro); classes mortas | DRY, bug visual | P2 | baixo | L |
| UI-N10 | shared/api/engine-client.ts:42; host protocol.rs:33 | `model.complete` exposto à UI sem uso; é superfície para gastar orçamento do modelo sem justificativa (par de NET-M10) | YAGNI, superfície | P2 | baixo | L |
| UI-N11/N12 | management/domain/activity-summary.ts; milestones-section.tsx:87; report-markdown.ts | Agregações e "contados" derivados no TS (podem divergir de `management.metrics`); `report-markdown.ts` é um segundo renderizador | Regra no lugar errado | P2 | médio | L |
| UI-N13 | test/fake-integration-engine.ts (303 linhas); testes de audit/timeline | Dublê monolítico com nome que não diz o escopo; helpers `clientWith`/`paged` copiados | DRY em testes | P3 | baixo | L |
| UI-N14..N21 | vários | Código morto (`Checklist`, `CommandDisplay`, ramo "indisponível" de `NavEntry`, 9 tipos); `!` e 5 casts `as keyof`; regras do motor espelhadas sem teste de paridade (`MIN_QUESTION`, `KEY_PATTERN`, credencial ≥ 8 em 3 lugares); `workflowSnapshotSchema` fora de `shared/schema`; `<RequiresHost>` 14×; `model.status` por 3 caminhos; 3 formas de mostrar erro; `ProjectIntegrations` com responsabilidades independentes | — | P3 | baixo | L |

## 3. Itens verificados sem problema

- Dependências Core → Application → Infrastructure; Application não referencia Infrastructure (única exceção: Execution ↔ Protocol, NET-E9).
- Autorização de projeto em toda leitura de Management (`IsAuthorized`/`EnsureAuthorized`); isolamento por projeto em `ProjectIntegrationService.Require`.
- Calculadoras puras com relógio injetado e semente determinística (`ForecastCalculator`); `AnswerVerifier` confere afirmações contra os fatos citados.
- Privacidade: diários e auditoria nunca gravam pergunta, resposta, título de item nem texto de decisão; segredo de conexão nunca volta em resposta, log ou auditoria; `HttpsConnectorTransport` não segue redirect, limita corpo a 16 MB; `JiraConnector` valida o domínio a cada requisição.
- Cofres: id por regex terminando em `\z`, segredo por stdin em base64, leitura de volta para confirmar, DPAPI com entropia, `UnavailableSecretVault` em vez de texto puro (a exceção é NET-E5, o modelo).
- Rust: `unwrap/expect` só em testes e no `build()`; host sem regra do motor; capabilities mínimas; `recents.rs` com gravação atômica e id estável com vetores de teste.
- Python: escuta só em 127.0.0.1, `hmac.compare_digest`, corpo ≤ 4 MB, log sem mensagem de exceção; `ModelClient` sem proxy de ambiente e restrito a loopback; prompts neutralizam delimitadores; travessia de caminho bloqueada; o laço de investigação termina (≤ 14 nós, abaixo do `recursion_limit`).
- Scripts/CI: `pip --require-hashes`, hash dos CPython de Linux/macOS, `tarfile filter="data"`, `permissions: contents: read`, `--frozen-lockfile`. SC-1 (hash do CPython embed do Windows) segue pendente por decisão do dono.
- UI: sem `any` nem `@ts-ignore`; ESLint barra imports profundos; `useEffect` restantes são fetch/assinatura/timer, sem estado derivado; schemas de gestão/runs/decisões/timeline validados contra `contracts/fixtures`; `ENGINE_METHODS` com teste de paridade com o host; acessibilidade estrutural (caption/scope, fieldset/legend, `aria-pressed`, skip link).

## 4. Hipóteses descartadas ou corrigidas na revisão

- "Nenhum `<ResourceView>` passa `onRetry`": verdadeiro para os 12 usos de `ResourceView`; o `grep` global mostra `onRetry` em 10 arquivos porque outros componentes (`PageState`, telas com tratamento próprio) o usam. O achado vale como escrito.
- `EngineSession` como "god class" (hipótese inicial pelo crescimento de 276 → 500 linhas no diff): descartada. O arquivo tem 276 linhas; o problema é o laço serial (NET-E1) e a montagem (NET-E8).
- Arquivos grandes e coesos (`knowledge-section.tsx`, `decisions-section.tsx`, `ProjectAudit.cs`, `ProjectTimeline.cs`, `shared/schema/management.ts`) não foram classificados como smell.

## 5. Plano de refatoração (incremental, um PR por onda)

| Onda | Itens | Projeto | Prova |
|---|---|---|---|
| 7a — integridade e contrato | CT-N1, PY-N1, NET-M11, NET-M1, NET-E4 + RS-N1 (hierarquia de timeouts), NET-E14 | Python, .NET, Rust | cada correção com teste que falha antes (ack contra os `maximum` do schema; índice corrompido; severidade desconhecida; fatos cortados em `withheld`) |
| 7b — persistência e concorrência | NET-E2, NET-E3, NET-E5, NET-E16, NET-E11 | .NET Infrastructure | testes de caracterização: arquivo travado não apaga; JSON corrompido vira `.corrupt-*` |
| 7c — laço do motor | NET-E1, NET-E8 | .NET Protocol | testes: `run.cancel` e `runtime.health` respondem durante `management.investigate` |
| 7d — SRP .NET | NET-M2, NET-M4, NET-E6, NET-E7, NET-M8, NET-M10, NET-M12, NET-E9 | .NET Application | suíte existente (1062) + fixtures golden |
| 7e — UI | UI-N1, UI-N4, UI-N5, UI-N6, UI-N7, UI-N9, UI-N10, UI-N14 | desktop/ui | vitest por role/label + `pnpm build` |
| 7f — UI Synthra | UI-N8 | desktop/ui | testes por role/label cobrem a regressão; aprovação visual do dono |
| 7g — contratos Python e higiene | PY-N4, PY-N5, CT-N2, PY-N2, PY-N3, SC-N1, RS-N2/N3 | Python, Rust, CI | `unittest`, clippy, fixtures |

Ordem: 7a primeiro (são defeitos, não estilo). 7c antes de 7d (muda o dispatcher que 7d consome). 7e antes de 7f. PY-N4 e UI-N11/N12 mudam contrato entre componentes e exigem decisão do dono (seção 6).

## 6. Decisões que dependem do dono do produto

- **NET-E5:** o fallback em texto puro da chave do modelo é decisão de produto? Se sim, restringir a permissão do arquivo e avisar na UI; se não, recusar gravar sem cofre como as conexões já fazem.
- **NET-M8:** unificar limiares de risco e previsão muda os números que a tela mostra. Qual é o limiar canônico (20% ou 50% de crescimento de escopo)?
- **PY-N4 / UI-N11 / UI-N12:** o motor passar a marcar papel dos grupos de fatos, devolver `counted`/`displayState` e o Markdown do relatório muda contratos e fixtures golden.
- **UI-N8:** migração dos controles nativos para o Synthra altera aparência; precisa de aprovação visual.
- **NET-M10:** remover `model.complete` do host, da UI e do .NET (nenhuma tela o chama).

## 7. Andamento

| Onda | Itens | Estado |
|---|---|---|
| 7a | CT-N1, PY-N1, NET-M11, NET-M1 (parcial), NET-E4 + RS-N1, NET-E14 | Mergeada (#57) |
| 7b | NET-E2, NET-E3, NET-E5 (permissão), NET-E11 | Mergeada (#58, chegou ao `master` pelo #60) |
| 7c | NET-E1 (NET-E8 adiado) | Mergeada (#59, chegou ao `master` pelo #60) |
| 7d | NET-M2, NET-M4 (parcial), NET-E7 (parcial) | Mergeada (#61) |
| 7e | UI-N1, UI-N4 (parcial), UI-N9 (tokens), UI-N14 (parcial) | Mergeada (#62) |
| 7f | UI-N8 (controles nativos → Synthra, menos os radios) | Mergeada (#63) |
| 7g | PY-N5, PY-N3, CT-N2 (descrições), SC-N1 (`--locked`), RS-N2, RS-N3 (comentário) | Neste PR |

**Decisões de escopo da 7a**
- CT-N1: quando a volta pede grupos e busca juntos e só cabe uma rodada, a investigação **para** e registra `INVESTIGATION_STOPPED` (em vez de escolher um dos dois pedidos). Teste compara o ack com os `maximum` do schema. O `Math.Clamp` do .NET em `InvestigationService` continua (defesa na borda, NET-M2 trata do ack tipado).
- PY-N1: `sqlite3.Error` e `OSError` viram `KnowledgeError` em `_session` e `_prepare_for_write`. **Não feito:** recriar o índice corrompido (ele é reconstruível). Hoje um índice corrompido degrada para a limitação "memória indisponível" em todo run do projeto até ser apagado.
- NET-M11: gravidade desconhecida fica abaixo da escala (`-1`) e o texto do registro é mostrado como veio. Não virou enum (NET-E13 segue aberto).
- NET-M1: só transparência (o aviso "N fatos ficaram de fora por passarem do limite" entra em `withheld`). **Não feito:** priorizar riscos antes de marcos quando o corte acontece; `Withheld.Skip(1)` segue.
- NET-E4 + RS-N1: hierarquia **120 s (runtime de IA, `RuntimeBudgetMilliseconds`) < 150 s (prazo do motor) < 180 s (host, só `management.investigate`)**. O transporte do modelo deixa de ter prazo próprio (o `ModelGateway` já põe um em cada operação). Os outros métodos seguem com 120 s no host.
- NET-E14: `RunQueries` exige o `RunCatalog`; a checagem de autorização não é mais opcional. Três testes passaram a construir o catálogo.
- Gates da onda: .NET 1066 passam (11 ignorados), 0 avisos; Rust fmt/clippy limpos e 24 testes; Python 169 testes e ruff limpo. UI não foi tocada.

**Decisões de escopo da 7b**
- NET-E2: `AtomicFile.ReadJson` é o ponto único de leitura dos stores de conexões, dados sincronizados, histórico de risco (e o registro de workspaces, que usa `AtomicFile.Quarantine`). `IOException` (arquivo travado) **sobe**; JSON inválido ou fora da forma vai para `<arquivo>.corrupt-<data>` e conta como vazio. Consequência visível: uma trava passageira agora aparece como "Falha interna (IOException)" em vez de lista vazia; repetir resolve e nada é apagado.
- NET-E3: `FileRunStore` ganhou um gate por arquivo (mesmo padrão dos outros stores) e escreve por `AtomicFile`. O teste de leitura/escrita simultânea do diário **falhou no código antigo** no Windows.
- NET-E5: só a **permissão** (`model.json` nasce 0600 no Linux/macOS). O fallback em texto puro continua: é decisão do dono (seção 6). O teste é `[UnixFact]` e **não rodou aqui** (Windows): fica ignorado, e a CI em Linux/macOS é quem o executa.
- NET-E11: `Reconciled` pergunta quem está rodando antes de ler o arquivo (antes, uma sincronização que terminava no meio tinha o desfecho e os itens sobrescritos por `SYNC_INTERRUPTED`); o pós-processamento (resumo da conexão, trilha, ouvintes) falha isolado e o aviso `sync.finished` sai sempre. Resta uma janela teórica (outra sincronização começar entre as duas leituras), que só existe se os handlers rodarem em paralelo: reavaliar na 7c.
- **Não feito:** NET-E16 (`FileAuditLog.ReadAll` carrega o arquivo todo sob o gate de escrita; precisa ler só o rabo e manter a numeração por linha). Fica para a 7d/7g.
- Gates da 7b: .NET 1077 passam (12 ignorados), 0 avisos. Rust, Python e UI não foram tocados.

**Decisões de escopo da 7c**
- NET-E1: `ProtocolMethods.Slow` lista os 11 métodos que esperam rede ou modelo (`model.save/test/complete`, `connection.create/test/rotate/reach`, `integration.link`, `management.ask/investigate/portfolioask`). O laço da `EngineSession` os despacha como tarefa e cada um escreve a própria resposta (a escrita já era serializada por `_outputGate`). Os demais métodos seguem **em ordem**, como antes.
- Encerramento: `engine.shutdown` (ou cancelamento do processo) cancela os pedidos longos em voo e espera até 10 s; o fim da entrada (EOF) **espera** por eles e os responde, como o motor fazia quando tudo era serial (um cliente por pipe não perde resposta).
- Mudança de comportamento: dois pedidos longos podem rodar ao mesmo tempo (por exemplo duas perguntas ao assistente). Os serviços já tinham gates, porque a sincronização em segundo plano os usava em paralelo; não achei estado compartilhado sem proteção, mas é a primeira coisa a olhar se aparecer defeito intermitente.
- NET-E11 (janela teórica em `Reconciled`): `integration.sync` e `integration.history` **não** são lentos e continuam no laço serial; a janela segue fechada.
- **NET-E8 adiado de propósito:** unificar os handlers numa interface (`IRequestHandler`) é P2, de risco médio, e nenhum handler novo está previsto; o ganho é só de manutenção. Resolvido o P1 (E1), a decisão fica para a 7d, se o dono quiser.
- Testes: `ProtocolConcurrencyTests` (4). Dois falhavam com o laço serial (`engine.hello` não era respondido durante `model.test`; `engine.shutdown` não era lido); dois caracterizam o que não pode mudar (ordem dos pedidos curtos e espera no EOF). Rodados 8 vezes seguidas, sem flakiness.
- Gates da 7c: .NET 1081 passam (12 ignorados), 0 avisos. Rust, Python e UI não foram tocados.

**Decisões de escopo da 7d** (só o que é SRP puro; o que depende do dono ficou de fora)
- NET-M2: `InvestigationService` (478 → 203 linhas) só orquestra. Saíram: `InvestigationWire` (monta o pedido `ai.investigation.start.v1` e lê o ack, funções puras), `InvestigationFacts` (fatos de execuções e mudanças), `InvestigationMemory` (seleção dos documentos da memória) e `InvestigationJournal` (diário). O construtor público não mudou (testes e `Program.cs` intactos); os colaboradores internos são criados nele. Refatoração pura, protegida pelos 879 testes de `ProjectInvestigationTests` e pelos de contrato; nenhum teste novo.
- NET-M4 (parcial): `AnswerVerifier.Verified.ToAnswer(...)` substitui as 3 montagens de `AssistantAnswer` com 13 argumentos; `ManagementAssistant.CleanQuestion` substitui as 3 validações do tamanho da pergunta. **Não feito:** o diário de pergunta (`RecordRun`) e o de investigação continuam separados (diferem mais do que parecem), as duas `Instructions` dos assistentes, e `PortfolioAssistant` gravar o mesmo `ModelUsage` em cada projeto (o uso aparece N vezes em `run.list` e na auditoria: parece defeito de contabilidade; precisa decidir se cada projeto registra a parte dele).
- NET-E7 (parcial): `HarnessComposition.IsJavascriptStack` e `IsRepositoryCommand` são a pergunta única do plano e da validação (`RequirementsCheck` tinha a lista de stacks copiada em literal). `TechnologyCatalog` já usava a fonte única. **Não feito:** o inverso caminho → componente (`HarnessRevisionService.ComponentByPath`) continua à mão; derivá-lo do `Catalog` mexe na revisão do Harness.
- **Não feitos nesta onda, por dependerem de decisão do dono ou de risco maior:** NET-M8 (limiar canônico de risco), NET-M10 (remover `model.complete`), NET-E6 (`IntegrationHub` como service locator, 8 consumidores), NET-M12 (um só estilo de erro), NET-E9 (`RunCatalog`), NET-E8 (handlers).
- Gates da 7d: .NET 1094 passam (12 ignorados), 0 avisos. Rust, Python e UI não foram tocados.

**Incidente de fim de linha (corrigido)**: os scripts Python que usei para editar arquivos nas ondas 7a–7c gravaram CRLF em 11 arquivos que eram LF (e em 3 novos); o repositório inteiro é LF (934 arquivos conferidos). Isso inflou os diffs (`HarnessComposition.cs` aparecia com 280/274 linhas por uma mudança de 8) e o #57 já levou parte do ruído ao `master`. O #60 normaliza tudo sem mudar conteúdo (`git diff --ignore-space-at-eol` vazio). Daqui em diante: edição por `Edit`/`Write` ou por script que lê e grava bytes; depois de cada onda, comparar o estilo de fim de linha com a base.

**Atenção ao empilhar PRs**: ao mergear uma pilha, o PR de cima vai para a branch de baixo, não para o `master`, se a de baixo já foi mergeada. Foi o que aconteceu com o #58 e o #59. Mergear de baixo para cima e esperar o GitHub trocar a base do seguinte antes de mergear, ou abrir cada PR contra o `master`.

**Decisões de escopo da 7e**
- UI-N1: `useAsyncResource` ganhou `reload` (volta a `loading` e refaz a leitura) e o `ResourceView` usa `onRetry ?? resource.reload`. As 11 telas que usavam `ResourceView` sem "Tentar de novo" (toda a gestão, catálogo, execuções) passam a ter o botão sem mudar uma linha em cada uma. O resultado do hook só muda de identidade quando a leitura muda (há teste: quem o usa em dependências não entra em laço). `Resource<T>` agora tem `reload?` no tipo; os testes que comparavam com `toEqual({ status })` passaram a `toMatchObject`. **Não feito:** unificar os outros 7 hooks de leitura (`useReloadable`, `useRunList`, as 4 seções de `project/components`): são variações com semântica própria (manter o valor na tela durante a releitura, paginação) e precisam de desenho à parte.
- UI-N4 (parcial): `runKindLabel` e `runEffectLabel` foram para `shared/utils` (eram 2 cópias idênticas em `runs` e `project`); `management` deixou de importar de `@/features/runs` só por um rótulo. **Corrijo o relatório:** o terceiro vocabulário (`configure-project/domain/labels.ts`: "Aplicação", "Reversão", "Validação") **não** é cópia, é a frase da jornada ("Aplicação em andamento…"), então fica. Não feito: unificar o tom de `running/validating` entre `STATE_TONE` e `stateTone`, e o rótulo dos passos ("pendente" × "aguardando"): são decisões de texto/visual.
- UI-N9 (só o bug): `--color-surface-muted` e `--color-warning-text` **não existiam**; o hover da lista de execuções agora usa `--color-menu-hover` e o aviso de modelo ausente usa `--tone-warning-fg`. **Mudança visual pequena: conferir no app (tema escuro).** Não feito: o SCSS duplicado entre as 4 telas e as classes mortas.
- UI-N14 (parcial): removidos `Checklist` e `CommandDisplay` (componentes, SCSS e testes; só os testes os usavam), `connectorName` e o alias `isTerminal`. `workflowStartParamsSchema` **fica**: só os testes o usam, mas é a guarda de contrato do que a UI envia. Não feito: o ramo "indisponível" de `NavEntry` e os tipos sem uso.
- **Não feitos (fora da 7e):** UI-N2 (cascata de requisições), UI-N3 (leitura que escreve), UI-N5 (formatadores de data/plural), UI-N6 (Timeline e Audit), UI-N7 (formulário de pergunta), UI-N10 (`model.complete`: aguarda decisão do dono), UI-N11/N12 (regra no TS: muda contrato), UI-N8 (Synthra, é a 7f).
- Gates da 7e: `pnpm check` (tsc, eslint, prettier, 621 testes em 65 arquivos) e `pnpm build` passam. .NET, Rust e Python não foram tocados.

**Decisões de escopo da 7f** (UI-N8)
- Migrados para o Synthra: 6 `<select>` → `SelectField` (3 filtros de Execuções, período do Relatório, provedor e modelo por finalidade em Configurações), 3 `<input>`/`<textarea>` do formulário de decisões e o campo "Ligar a um item" de Execuções → `TextField` (os `<textarea>` com `multiline`), 1 checkbox → `Checkbox`. Cada controle perdeu o `<label>` e o SCSS próprio (`.select`, `.input`, `.field`, `.label`, `.purposeTitle`), o que também fecha parte do UI-N9 (SCSS morto).
- **Os 2 radios (`choice-field`, `ai-environment-section`) ficam nativos:** o `@mariliz/ui-kit` não tem Radio. Se o dono quiser, o caminho é pedir um Radio ao Synthra, não escrever um aqui.
- O `SelectField` é um MUI `Select` (combobox com lista), não um `<select>` nativo: muda o jeito de interagir (lista em popover) e, por isso, **a mudança visual e de comportamento precisa de aprovação do dono**. Valor "sem filtro" virou o texto `all` (um valor vazio num `Select` deixa o rótulo por cima do texto).
- Testes: `src/test/choose-option.ts` (`chooseOption`, `optionsOf`) substitui `userEvent.selectOptions`. 17 chamadas de `selectOptions` e 10 asserções de `toHaveValue`/lista de opções foram reescritas para o que a pessoa vê (texto do combobox e das opções). Os testes de decisões e os de acessibilidade (axe) passaram sem mudança.
- **Não verificado:** a aparência no app real (tema escuro, largura mínima dos filtros) e leitor de tela com o combobox do MUI. Telas para olhar: Execuções (filtros e "Ligar a um item"), Relatório (período), Configurações (provedor e modelo por finalidade, agora com o rótulo flutuante no lugar do título em negrito) e o formulário de decisões.
- Gates da 7f: `pnpm check` (tsc, eslint, prettier, 621 testes em 65 arquivos) e `pnpm build` passam. .NET, Rust e Python não foram tocados.

**Decisões de escopo da 7g** (só o que é verificável aqui e não depende de decisão do dono)
- PY-N5: o bloco de fatos do pedido ao modelo cortava os últimos fatos em silêncio, e o modelo ainda podia citá-los (a lista de ids conhecidos tinha todos). Agora `fit()` decide o que cabe, o agente só conhece (e só aceita citar) o que o modelo viu, e os nós registram `INVESTIGATION_FACTS_TRUNCATED` (código por padrão, não enum: sem mudança de contrato). Teste: o fato cortado não aparece nos pedidos, citá-lo vai para `rejected` e o ack sai `completed_with_limitations`.
- PY-N3: `retrieval.search` abria uma conexão por trecho candidato (até ~40, cada uma com DDL e commit). `index.chunk_bodies` lê todos numa sessão (o teste conta as sessões: ≤ 3 contra 13 antes). O corpo continua fora do contrato dos trechos.
- RS-N2: `list_recent_projects` passou a `#[tauri::command(async)]` (comando síncrono roda na thread principal no Tauri 2), a conferência de pastas roda fora do mutex (a lista é copiada sob o lock) e `open_recent_project` confere a pasta em `spawn_blocking`. Teste: o lock está livre enquanto as pastas são conferidas (falhava antes). `record` continua gravando sob o lock de propósito, para a ordem das gravações não se inverter.
- RS-N3 (parcial): só o comentário de `FRONTEND_METHODS`. **Não feito:** o `params.cloned()` (até 4 MB): mudar a assinatura de `validate_frontend_request` mexe em todos os testes por um ganho que só aparece em pedidos enormes.
- CT-N2 (parcial): as descrições de `ai.event.v1` e `ai.agent.result.v1` apontam para `ai.ack.v3` (que tem o `$defs/action`). Os ADRs antigos em `docs/architecture` seguem falando de `ai.ack.v2` por serem históricos. **Não feito:** decidir o destino de `ai.agent.result.v1` (sem produtor nem consumidor) e alinhar a regra de `question` entre schema e Python.
- SC-N1 (parcial): `--locked` no `clippy` e no `cargo test` do CI (o `Cargo.lock` já está versionado; passa local). **Não feito:** fixar a versão do toolchain Rust, mover o clippy para antes do job do instalador, e testes Python em Windows: precisam do CI com orçamento para serem validados, e não dá para provar aqui.
- **Não feitos, por serem decisão do dono ou de risco maior:** PY-N4 (vocabulário de grupos de fatos no Python: muda o contrato com o motor), PY-N2 (lock do checkpoint cobre o grafo inteiro: precisa de teste de concorrência e desenho), PY-N6..N9 (campos mortos, imports entre pacotes, dicts sem tipo) e SC-N2.
- Gates da 7g: Python 173 testes e ruff limpo; Rust `fmt`, `clippy --locked -D warnings` e 25 testes com `--locked`; .NET build sem avisos e 169 testes de contrato/protocolo; UI 62 testes de schema. O `dotnet test` completo e o `pnpm check` não foram refeitos: nenhum código .NET ou UI mudou nesta onda.

## 8. Fechamento da auditoria (2026-11-13)

Ondas 7a–7g entregues (#57, #60, #61, #62, #63 e a 7g). O que sobra, por quem decide:

| Dono | Item | Por que está parado |
|---|---|---|
| ~~Dono do produto~~ | ~~NET-E5: manter ou não o fallback de chave do modelo em texto puro~~ | **Decidido (2026-11-13): manter o fallback**, com permissão 0600 e o aviso da tela. Registrado em `LocalModelSettings` |
| ~~Dono do produto~~ | ~~NET-M8: limiar canônico de risco e previsão (20% × 50%)~~ | **Decidido (2026-11-13): dois níveis nomeados.** 20% é atenção (previsão), 50% é risco (lista); constantes e fórmulas em `TrendRules`; nenhum número da tela mudou |
| Dono do produto | NET-M10 / UI-N10: remover `model.complete` | superfície sem uso, mas é contrato |
| Dono do produto | `PortfolioAssistant` grava o mesmo `ModelUsage` em cada projeto | parece defeito de contabilidade; decidir como dividir o uso |
| Dono do produto | PY-N4, UI-N11/N12: papel dos grupos de fatos, `counted`, Markdown do relatório | mudam o contrato entre componentes |
| Dono do produto | UI-N8: Radio no Synthra | o ui-kit não tem; os 2 radios seguem nativos |
| Desenho à parte | os outros 7 hooks de leitura da UI (UI-N1), UI-N2/N3/N5/N6/N7 | semântica própria; cada um pede desenho |
| Desenho à parte | NET-E6 (`IntegrationHub`), NET-M12, NET-E9, NET-E8, NET-E16, PY-N2 | refatorações de risco médio sem defeito em aberto |
| CI com orçamento | SC-N1 (toolchain fixo, ordem dos jobs, Python no Windows) | não dá para validar sem rodar a CI |

### Decisões do dono tomadas depois do fechamento (2026-11-13)
- **NET-E5 — manter o fallback de chave sem cofre.** Nada mudou no comportamento: `model.json` com permissão 0600 (onda 7b) e o aviso "sem a proteção do sistema" que a tela já mostrava. A decisão ficou escrita no comentário de `LocalModelSettings`. Recusar sem cofre, ou pedir confirmação na tela, seguem possíveis como uma onda própria (contrato, tela e testes).
- **NET-M8 — dois níveis nomeados, de propósito.** O nível de atenção (chegadas 20% acima das entregas, janela de 6 a 12 semanas) baixa a confiança da previsão e lista o fator em "Fatores que podem atrasar"; o nível de risco (50%, últimas 4 semanas) é o que entra na lista de riscos. `TrendRules` guarda as constantes e as duas fórmulas (`ScopeGrowing`, `ThroughputDropped`) que a previsão e `RiskRules` antes escreviam cada uma por conta própria; a queda de ritmo é a mesma conta nos dois lugares (só a janela muda). **Nenhum número da tela mudou.** Dois testes novos de fronteira e um de caracterização (a previsão avisa com +25% e a lista de riscos fica quieta) travam o comportamento. O texto da tela já separava os dois conceitos ("Fatores que podem atrasar" × "Riscos"), então nenhum texto foi alterado; `riskFactors` é nome de campo do contrato e ficou.

### Itens da seção 8: decisões e andamento (2026-11-13)
- **Uso de modelo do portfólio — decidido e feito:** valor cheio em cada projeto, com explicação. O diário de cada projeto ganha o passo "Pergunta ao portfólio" ("A pergunta cobriu N projetos. O uso de modelo abaixo é o da pergunta inteira, repetido no diário de cada projeto."); uma pergunta sobre um projeto só não ganha o passo. Sem mudança de contrato e sem número estimado. Não dividir entre projetos (seria um número inventado) e não gravar só no primeiro (assimetria).
- **`model.complete` — investigado, aguarda confirmação do dono para remover.** Evidência: nasceu em 2026-10-31 (`79f22d8`, "guardar a chave do modelo fora do projeto", sem corpo) como a primeira superfície de chamada ao modelo; esse commit não tocou a UI e a UI **nunca** o chamou (o nome só aparece na lista de métodos permitidos, `76b291a`, UI-12); desde a Fase 6 (2026-11-05) os agentes falam com o modelo pelo proxy do motor (`ChatAsync`, ADR 0001: "quem cobra: o proxy do motor"); em produção só o handler o aciona e, fora ele, só testes (`ModelGatewayTests`, `LiveValidationTests`). Nenhum ADR, roadmap ou fase o planeja; o único documento que o cita é a tabela de estado atual ("conclusão simples"). **Veredito: legado superado, não feature futura.** Remover de ponta a ponta (UI, host, handler, `CompleteAsync`, schema `model.complete.result`, testes) continua sendo o recomendado, mas só com o seu de acordo.
- **Radio — decidido:** não vai para o `@mariliz/ui-kit` (Synthra). Entra na camada de atomic design do app (`shared/components/ui/atoms`), seguindo o padrão dos componentes de `FormField` e `UI` do próprio kit. Em andamento em PR à parte.
- **PY-N4 / UI-N11 / UI-N12 — decidido: contrato v2 coordenado.** Pede um desenho antes do código (papel dos grupos de fatos, `counted`, estado de exibição, Markdown do relatório; schemas, fixtures golden e as três camadas). Desenho por fases em PR à parte.
