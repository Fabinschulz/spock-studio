# Arquitetura atual e proposta

> Parte do estudo [Engineering Intelligence Hub](README.md) (2026-11-16). §21.1 do pedido. Diagramas em texto para
> ficarem versionáveis e revisáveis em diff.

## 1. Arquitetura atual

```text
┌───────────────────────── Máquina da pessoa ─────────────────────────────────────────────┐
│                                                                                          │
│  Desktop (Tauri)          Motor .NET (sidecar NDJSON)              Runtime Python         │
│  ┌──────────────┐  invoke ┌───────────────────────────────┐ HTTP   ┌──────────────────┐   │
│  │ UI React     │────────►│ Protocol (99 métodos)         │ loop-  │ LangGraph        │   │
│  │ (sem regra)  │  host   │ Discovery · Harness · Plan    │ back + │ engenharia,      │   │
│  └──────────────┘ allow-  │ Integrations · Management     │ token  │ investigação     │   │
│                   list    │ Cloud · IaC · Models(broker)  │◄──────►│ Índice FTS5      │   │
│                           │ Audit · Timeline · Runs       │        │ (por projeto)    │   │
│                           └──────────────┬────────────────┘        └──────────────────┘   │
│                                          │ mesmo executável, argumento `mcp`               │
│  IDE (Claude Code…) ──stdio──► Servidor MCP (Mcp/) — composição própria (McpEntry)        │
│                                 só leitura · escopo opcional · só texto                    │
│                                                                                          │
│  Dados: MARILIZ_DATA_DIR (workspaces, audit.ndjson, projects/<p-hash>/…, integrations/)  │
│  Segredos: cofre do SO          Projeto: mariliz.yaml, mariliz.lock, .claude/, .mariliz/  │
└──────────────────────────────────────────────────────────────────────────────────────────┘
        │ só GET, HTTPS                       │ SDK oficiais
        ▼                                     ▼
   Jira Cloud · GitHub                   AWS · Azure · GCP        Provedores de modelo (allowlist)
```

## 2. Arquitetura proposta

```text
┌───────────────────────── Máquina da pessoa ─────────────────────────────────────────────┐
│                                                                                          │
│  Desktop (Tauri + UI)           Motor .NET                                              │
│  ┌──────────────────┐          ┌───────────────────────────────────────────────────────┐ │
│  │ + Conexão com    │─────────►│ Composição única (LocalEngine) ◄── usada pelo MCP     │ │
│  │   IDEs (diag.)   │          │                                                       │ │
│  │ + Entregas       │          │  Project Intelligence                                 │ │
│  └──────────────────┘          │   identity (stableId) · profile · architecture(N)     │ │
│                                │  Context Engine (N)                                    │ │
│                                │   modelo canônico · composição por intenção ·          │ │
│                                │   orçamento · proveniência · frescor · política        │ │
│                                │  Harness canônico (A) ─► Adaptadores por IDE (N) ─►    │ │
│                                │                          PlanBuilder/Apply/Revert (R)  │ │
│                                │  Connector Hub (A): conexão · vínculo · recorte ·      │ │
│                                │   política por destino · Jira(A) GitHub(A) ADO(N)      │ │
│                                │   Cliente MCP (N, P2)                                  │ │
│                                │  Management (R) + catálogo de métricas (N)             │ │
│                                │  Delivery Verification (N)                             │ │
│                                │  Access Policy (A) · Audit (R) · Models/Broker (R)     │ │
│                                └───────────┬────────────────────────┬──────────────────┘ │
│                                            │                        │ HTTP loopback       │
│                                            │                        ▼                     │
│  Claude Code ─┐                            │                 Runtime Python (R)            │
│  Cursor ──────┼─stdio─► SPOCK MCP (C/A) ───┘                 workflows · índice FTS5 (A)  │
│  Codex ───────┤         vínculo obrigatório · structuredContent · bootstrap ·            │
│  Copilot ─────┘         gestão · entrega · infra                                          │
│     ▲                                                                                     │
│     └── arquivos projetados no projeto: CLAUDE.md/AGENTS.md (bloco) · .mcp.json ·         │
│         .cursor/ · .codex/ · hooks → `spock context session|prompt`                       │
│                                                                                          │
│  (opcional, Modelo C) repositório Git da organização: política e portfólio, só leitura    │
└──────────────────────────────────────────────────────────────────────────────────────────┘
```

Legenda: **R** reutilizado, **A** ampliado, **C** corrigido, **N** novo.

## 3. Componentes

| Componente | Situação | Mudança |
| --- | --- | --- |
| Host Tauri, UI | R | Tela de diagnóstico de IDE; tela de entregas |
| Protocolo NDJSON | R | Métodos novos `context.*`, `ide.*`, `delivery.*` |
| Discovery, perfil | R | Detector de arquitetura (N) |
| Harness: declaração, lock, plano, aplicação, reversão | R | Modo `block`; lista de ambientes; projeções |
| Adaptadores de IDE | N | Claude Code, Cursor, Codex (P1); Copilot, Gemini (P2) |
| Context Engine | N | Serviço no .NET, compõe o que já existe |
| Servidor MCP | C + A | Vínculo obrigatório, composição única, saída estruturada, ferramentas novas |
| Comando `spock context` | N | Hooks de sessão e de prompt, sem modelo |
| Lançador `spock` no PATH | N | Instalador (decisão DD-6) |
| Integrations | A | Política por destino; Jira com critérios; Git e GitHub tipados; Azure DevOps (P2) |
| Management | R | Catálogo de métricas versionado |
| Delivery Verification | N | Pacote de tarefa, verificador, execução `delivery` |
| Cliente MCP | N (P2) | A partir do inventário existente |
| Runtime Python | R | Índice: arquivo inteiro, orçamento de indexação, `indexedAt` na busca; teto vindo do motor |
| Cofre, broker de modelo | R | — |
| Processos filhos | C | Filtro de ambiente |

## 4. Fluxos

### 4.1 Dados (gestão)

```text
Jira / GitHub / ADO ──(sincronização sob demanda, incremental, só GET)──► integrations/data/<p>/<pint>.json
      └► ManagementService (regras do catálogo) ──► métricas + MetricSource(syncedAt, complete)
            ├► Tela de Gestão
            └► MCP project_status ──► IDE (structuredContent + texto)
```

### 4.2 Autenticação

```text
Credencial de fonte: pessoa ─► tela ─► motor ─► teste no provedor ─► cofre do SO (id)   [nunca sai do motor]
Credencial de nuvem: SDK lê o perfil/SSO da máquina; motor guarda só nomes e ARN
Chave de modelo: cofre (ids reservados); Python recebe token de broker, não a chave
IDE ─► MCP: processo filho local por stdio; sem credencial; identidade do cliente só declarada
```

### 4.3 Autorização (toda chamada MCP)

```text
tools/call
  └► projeto resolvido? (stableId + pasta autorizada)      não → PROJECT_NOT_BOUND
      └► ferramenta liberada para o projeto?                não → TOOL_NOT_ENABLED
          └► fonte vinculada ao projeto?                     não → SOURCE_NOT_LINKED
              └► política do destino "ide" permite o nível?  não → POLICY_DENIED (diz o que existe)
                  └► recorte (DataScope) ─► dados ─► redação ─► auditoria ─► resposta
```

### 4.4 Contexto

```text
Sessão nova:  arquivo de instrução (bloco) ─► hook `spock context session` ─► resumo ≤1.500 tokens
Prompt:       hook `spock context prompt` (onde existe) ─► aviso curto só se algo mudou
Sob demanda:  agente ─► ferramenta MCP ─► Context Engine(intent, budget) ─► itens com proveniência
Mudança no SPOCK: contextVersion muda ─► plano com diff dos arquivos projetados ─► aprovação
```

### 4.5 Gestão na IDE e entrega

```text
"Como está o projeto?" ─► agente ─► project_status/project_risks ─► números do motor com fonte e data
"Terminei"             ─► delivery_check ─► Git diff + testes + critérios ─► pendências
                                              └► execução `delivery` ─► Gestão (risco, linha do tempo)
```
