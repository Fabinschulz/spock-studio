# Auditoria arquitetural — Mariliz AI Engineering (2026-11-06)

Escopo: runtime .NET 10, host Tauri/Rust, UI React/TypeScript, `intelligence/` (Python/LangGraph), `scripts/`.
Método: leitura do código por stack (somente leitura) + execução dos gates existentes. Nenhum código foi alterado nesta fase.

## 1. Resumo executivo

O código está em bom estado. As fronteiras entre camadas são respeitadas em todas as stacks e não há problema P0.

**Linha de base (todos os gates passam):**

| Stack | Gates | Resultado |
|---|---|---|
| .NET | `dotnet build` / `dotnet test` | 0 warnings, 0 erros; 459 testes passam, 3 ignorados (E2E empacotados); 2 de integração passam |
| Rust | `cargo fmt --check` / `clippy --all-targets --all-features` / `cargo test` | limpo, 0 warnings, 21 testes passam |
| React | `tsc` / `eslint` / `vitest` / `vite build` | limpo, 206 testes passam; build avisa de chunk de 864 kB |
| Python | `unittest discover` | 96 testes passam; **não existe lint nem verificação de tipos** |

**Fronteiras:**
- .NET: Core → Application → Infrastructure, sem inversão.
- Rust: sem regra de negócio do motor, sem `unwrap` em caminho de execução.
- React: ESLint já proíbe `any`, imports profundos entre features e `shared → features`.

**Principais problemas:**
1. Regra do motor .NET duplicada no Python (`graph.py`: caminho de decisão, mapa stack→área).
2. `graph.py` é um módulo com várias responsabilidades.
3. `LoopbackAiRuntime`, `WorkflowService` e `EngineSession` acumulam papéis. O mapeamento de exceções é inconsistente.
4. Duas implementações independentes de "acompanhar uma execução" na UI.
5. Código morto em volume: ~40% do `panels.module.scss`, tokens, glyphs, dependência npm.

## 2. Inventário

### .NET (NET)
| ID | Local | Problema | Princípio | Sev |
|---|---|---|---|---|
| NET-1 | LoopbackAiRuntime.cs:60-74, 122, 290-318 | Health check mistura "ping" e "aguardar partida" (bloqueia ~20s com processo travado, também no Cancel) | SRP | P1 |
| NET-2 | LoopbackAiRuntime.cs | 5 responsabilidades (processo, HTTP, NDJSON, localização de Python, porta); env vars e timeouts soltos | SRP | P1 |
| NET-3 | Ports/IAiRuntime, WorkflowAck, RunHandle.Result | `JsonObject` vaza pelas portas; desfecho re-derivado por string | DIP, primitive obsession | P1 |
| NET-4 | WorkflowService.cs | 9 params (3 opcionais com `!`), mistura autorização, aprovação, journal, juiz, erros | SRP | P1 |
| NET-5 | WorkflowService.cs:253-259 + RunHandle/EngineSession/ActionGovernance | `catch` amplo mostra erro de disco como "runtime de IA não respondeu"; 5 políticas de catch diferentes | Tratamento de erro | P1 |
| NET-6 | EngineSession.cs | Handlers inconsistentes; lista de capacidades e despacho são duas fontes de verdade | SRP/OCP | P2 |
| NET-7 | ActionGovernance.cs | Classe estática com política, hash, transição, execução e render | SRP | P2 |
| NET-8 | ApplyExecution.cs | 13 parâmetros; padrão JournalOp copiado 2x | Long parameter list, DRY | P2 |
| NET-9 | ConfigureProjectService.cs | `new` em inicializadores; 3 formatos de falha | Hidden deps | P2 |
| NET-10 | CliRunner.cs:179-226 | Política de aprovação e mapeamento de erro duplicados do protocolo | DRY | P2 |
| NET-11 | ModelBroker.cs | 8 tipos em um arquivo; `CostUsd` morto; `Text()` duplicado | SRP | P2 |
| NET-12 | HarnessComposition.cs | Builder de uso único com `null!` e acoplamento temporal | Temporal coupling | P2 |
| NET-13 | RunHandle.cs:75-93 | I/O sob lock; eventos podem sair fora de ordem | Concorrência | P2 |
| NET-14 | WorkflowContextBuilder.cs | Dois modos via ctor privado; `Directory.CreateDirectory` em Application | Null-object ad hoc | P2 |
| NET-15 | vários | Timeouts e limites espalhados; 60s e 60_000ms acoplados sem constante | Config espalhada | P2 |
| NET-16 | 4 arquivos | `Sha256` reimplementado apesar de `Hashing.Sha256` | DRY | P2 |
| NET-17 | WorkflowService/WorkflowEvents | Status do ack em literais | Magic strings | P2 |
| NET-18..25 | ProposalReader, RunTracker, LocalModelAccess, Program.cs... | Repetições e helpers JSON duplicados; `LocalEngine` como service locator | DRY | P3 |

### Rust (RS)
| ID | Local | Problema | Sev |
|---|---|---|---|
| RS-1 | engine.rs:56-96 | Handshake falho marca `Crashed` mas o sidecar continua vivo | P2 |
| RS-6 | recents.rs:149-176, lib.rs:35-102 | `Recents` conhece o formato de resposta do motor; fluxo authorize→record duplicado | P2 |
| RS-2 | engine.rs:170 | `restarts` nunca zera (só 1 restart por vida do app) | P2/P3 |
| RS-3, RS-4, RS-5, RS-7, RS-8, RS-9 | engine.rs, recents.rs, protocol.rs, status.rs | Envelope de shutdown duplicado; entrada de `pending` pode vazar; mutex num enum `Copy`; clones; erros em string; fallback silencioso para `temp_dir` | P3 |

### Python / scripts (PY, SC)
| ID | Local | Problema | Sev |
|---|---|---|---|
| PY-1 | graph.py, contracts/engineering.py:205-250 | Regra do motor no Python: alvo `.mariliz/decisions/…`, mapa stack→área, limites | P1 |
| PY-2 | graph.py (687 linhas) | Módulo com estado, nós, orçamento, modelo, ack e checkpoint | P1/P2 |
| PY-3 | graph.py:658 | Linha de ~290 caracteres escondendo a regra de aceite do juiz | P2 |
| PY-4 | graph.py | Orçamento aplicado só a 5 nós; `time.monotonic()` ignora `segment.clock` | P2 |
| PY-5 | graph.py | `architecture_agent` e `_model_step` duplicam sequência | P2 |
| PY-7 | server.py | Exceções engolidas; stream sem ack de falha | P2 |
| PY-9 | knowledge/index.py | Efeitos colaterais em resolução de caminho; N+1 em busca | P2 |
| PY-10 | intelligence/ | Sem ruff, mypy ou pyproject | P2 |
| PY-11, PY-12 | contracts/*, index.py | Validação manual duplicando JSON Schema; política de fontes duplicada | P2 |
| SC-1 | build-runtime.ps1 | CPython embed do Windows baixado sem SHA256 | P2 |
| PY-6, 8, 13, 14, 15, SC-2, SC-3 | vários | `context_builder` morto, docstring desatualizada, `index.search` morto... | P3 |

### React (UI)
| ID | Local | Problema | Sev |
|---|---|---|---|
| UI-1 | configure-project/components/panels.module.scss | ~33 classes sem uso; `.envItem` duplicado; stylesheet comum a 18 arquivos | P1 |
| UI-2 | src/prototype (239 arquivos) | UI paralela fora do build; decisão de produto pendente (fase 6) | P1 |
| UI-3 | configure-project vs execution | Dois motores de acompanhamento de execução (um com protocolo seq/gap, outro com polling) | P1 |
| UI-4 | execution-page.tsx | Apresentação + estado + caso de uso `approve` | P2 |
| UI-5 | execution/domain | `AGENT_LABEL` duplicado; helpers `text()` repetidos | P2 |
| UI-6 | glyphs.tsx, scrollable-region.tsx, labels.ts, Checklist/CommandDisplay, `@mui/x-data-grid` | Código morto e dependência sem uso | P2 |
| UI-7 | shared/styles/_tokens.scss | 53 de 155 tokens sem uso; aliases iguais; `--color-danger` inexistente; `palette as never` | P2 |
| UI-8 | hooks, engine-client | 5 estilos de erro; issue do Zod descartada | P2 |
| UI-9 | 4 hooks | Mesmo efeito de fetch-on-mount copiado; import de service entre features | P2 |
| UI-10..16 | vários | Hook de jornada, props de steps, protocolo em string, casts, ícones duplicados, dois componentes de aviso, chunk de 864 kB | P3 |

## 3. Plano de refatoração (incremental, um PR por onda)

| Onda | Itens | Projeto | Testes/gate |
|---|---|---|---|
| 1. Remoção segura | UI-1, UI-6, UI-7 (aliases), NET-11 (`CostUsd`), NET-16, PY-3, PY-8, PY-14, RS-3 (envelope) | UI, .NET, Python, Rust | tsc, eslint, vitest, build, dotnet test |
| 2. Correção e diagnóstico | NET-1, NET-5, RS-1, RS-2, PY-7, SC-1 | .NET, Rust, Python | testes de caracterização (stub travado, handshake falho) antes de alterar |
| 3. Decomposição .NET | NET-4, NET-7, NET-2, NET-6, NET-8, NET-14, NET-15, NET-17 | .NET | suíte existente (459 testes) protege |
| 4. Decomposição Python | PY-2, PY-4, PY-5, PY-9, PY-10 | intelligence | `unittest` (96) + ruff/mypy gradual |
| 5. UI estrutural | UI-3, UI-4, UI-5, UI-8, UI-9 | desktop/ui | execution-page.test, journey.test |
| 6. Contrato (coordenado) | PY-1, PY-11, PY-12, NET-3, RS-6, UI-12 | .NET + Python + Rust + UI | fixtures golden |

Ordem de dependência: a onda 6 muda contratos entre componentes e deve vir por último. A onda 3 deve vir antes de NET-3.

## 4. Decisões que dependem do dono do produto
- **UI-2:** finalizar a fase 6 do redesign e apagar `src/prototype`, ou isolá-lo em outro workspace? Não será apagado sem aprovação.
- **PY-1:** o motor .NET passa a resolver o alvo da decisão e o mapa de áreas (muda o contrato `ai.workflow.*` e as fixtures golden).

## 5. Itens verificados sem problema
Direção de dependências .NET; Core (Policy/Text/Discovery/Plan/Evals); lógica de reserva e liquidação de orçamento do ModelBroker; `FixedTimeEquals` nos tokens; protocol.rs, status.rs e resources.rs do host; ausência de `any` e `@ts-ignore` na UI; ausência de `useEffect` para estado derivado.
