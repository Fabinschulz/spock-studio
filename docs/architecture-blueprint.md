# Mariliz AI Engineering — Architecture Blueprint

**Fase:** 0. **Data:** 2026-10-25. Rótulos conforme [`product-discovery.md`](product-discovery.md). Tudo aqui é proposta para aprovação; nenhum código existe.

---

## 1. Separação entre interface, CLI e motor

| Processo | Linguagem | Responsabilidade | Não faz |
| --- | --- | --- | --- |
| `desktop-ui` | TypeScript (React, Vite, Synthra) | Apresentar estado, coletar decisões do usuário, validar formulários (forma) | Regra de negócio, acesso a arquivos, segredos |
| `desktop-host` | Rust (Tauri 2) | Janela, ciclo de vida, diálogo nativo de pasta, iniciar/encerrar o motor, repassar mensagens | Interpretar o conteúdo das mensagens, executar comandos arbitrários |
| `Mariliz.Engine` | C# (.NET 10) | Todos os casos de uso, política, permissões, idempotência, validação | UI |
| `Mariliz.Cli` | C# (.NET 10) | Mesmos casos de uso via linha de comando, saída humana ou `--json` | UI |

Regra de dependência (.NET):

```
Mariliz.Engine ─┐
                ├─▶ Mariliz.Application ─▶ Mariliz.Core
Mariliz.Cli ────┘           ▲
                            │ (interfaces declaradas em Application/Core)
               Mariliz.Infrastructure
```

`Core` não referencia nada externo. `Application` declara portas (ex.: `IFileSystem`, `IProcessRunner`, `ISecretStore`, `IRuntimeAdapter`, `IConnector`). `Infrastructure` implementa. `Engine` e `Cli` são composição (DI) e transporte.

---

## 2. Módulos e responsabilidades

Responsabilidades lógicas, implementadas como namespaces/pastas dentro dos projetos acima. Um projeto físico novo só nasce quando houver motivo concreto (ex.: um conector com dependência pesada).

| Módulo | Camada principal | MVP |
| --- | --- | --- |
| Workspace (seleção e autorização de pastas) | Application | Sim |
| Discovery (detecção por evidências) | Application + Infrastructure | Sim |
| Profiles | Core (contrato) + pacotes de conteúdo | Sim (.NET, nextjs) |
| Harness (agentes, skills, gates; geração por runtime) | Core + Application | Sim |
| Plan/Apply (preview, diff, conflitos, aplicação idempotente, backup) | Application | Sim |
| Validation (lint Tier 0, gates do profile) | Application | Sim |
| Runs (estado, eventos, cancelamento, checkpoints, histórico) | Application | Sim |
| Runtime adapters (Claude Code) | Infrastructure | Sim |
| Secrets | Infrastructure | Fase 4 |
| Connectors (Jira, GitHub, Azure DevOps, bancos) | Infrastructure | Fase 4 |
| MCP management | Application + Infrastructure | Fase 4 |
| Ingestion (documentos) | Application + Infrastructure | Fase 3 |
| Requirements/Specs | Core + Application | Fase 3 |
| Legacy/RAG | Application + Infrastructure | Fase 6 |
| Feedback/Evals | Application | Tier 0 no MVP; demais na Fase 7 |

---

## 3. Comunicação entre host, UI e motor

### 3.1 Transporte

- **stdin/stdout do motor, NDJSON** (um objeto JSON por linha, UTF-8, `\n`). **stdout é exclusivo do protocolo**; logs vão para stderr (capturado pelo host e gravado no histórico operacional) e para arquivo estruturado do motor.
- Limite de mensagem: 4 MiB (configurável). Conteúdo maior (diffs grandes, relatórios) é gravado pelo motor em arquivo no histórico operacional e referenciado por id.
- Não há porta de rede. Se no futuro um endpoint HTTP local for necessário (ex.: callback de OAuth), ele escuta só em `127.0.0.1`, porta efêmera, com token de uso único, e vive apenas durante o fluxo.

### 3.2 Envelope (protocolo v1)

```json
{ "v": 1, "kind": "request",  "id": "r-42", "method": "project.discover", "params": { "projectId": "p-1" } }
{ "v": 1, "kind": "response", "id": "r-42", "result": { "runId": "run-7" } }
{ "v": 1, "kind": "response", "id": "r-43", "error": { "code": "PATH_NOT_AUTHORIZED", "message": "…", "data": {} } }
{ "v": 1, "kind": "event",    "runId": "run-7", "type": "step.completed", "seq": 12, "data": { } }
```

- `seq` monotônico por execução permite à UI detectar perda e pedir `run.snapshot`.
- **Handshake** obrigatório: o host envia `engine.hello { protocol: [1], hostVersion }`; o motor responde com a versão escolhida, a própria versão e as capacidades. Incompatibilidade gera tela de erro, não comportamento indefinido.
- **Cancelamento:** `run.cancel { runId }`. O motor termina o passo atômico corrente, grava checkpoint e emite `run.state = "Falhou"` com motivo `cancelled`, ou `Concluído com limitações`, conforme o caso.
- **Erros:** códigos estáveis (catálogo em `packages/contracts`); `message` já sanitizada (sem segredos, caminhos completos apenas quando necessários).

### 3.3 Papel de cada camada no protocolo

| Camada | O que valida |
| --- | --- |
| Rust | Só o envelope (`v`, `kind`, `id`/`runId`, tamanho). Repassa `params`/`result`/`data` sem interpretar |
| TypeScript | Payload completo contra os tipos gerados do schema (validação em runtime nas respostas e eventos) |
| C# | Payload completo na entrada; o motor rejeita o que não segue o schema |

Assim o Rust não precisa conhecer cada método e não vira um terceiro lugar com regras.

### 3.4 Comandos Tauri expostos ao frontend

| Comando | Faz |
| --- | --- |
| `engine_request(envelope)` | Envia uma requisição ao motor e devolve a resposta |
| `pick_project_folder()` | Abre o diálogo nativo **no lado Rust**, registra a pasta no motor (`workspace.authorize`) e devolve só `{ projectId, displayName, displayPath }` |
| `engine_status()` | Estado do processo do motor (iniciando, pronto, caiu, reiniciando) |

Eventos do motor chegam à UI pelo canal de eventos do Tauri (`engine_event`). O frontend **não** recebe permissão do plugin shell nem de leitura/escrita de arquivos; capabilities em `src-tauri/capabilities/` listam só esses três comandos e o necessário para a janela.

### 3.5 Ciclo de vida do sidecar

- O host inicia o motor na abertura do app, com `tauri-plugin-shell` chamado **a partir do Rust** (`sidecar("mariliz-engine")`), sem expor o plugin ao frontend.
- Uma instância de motor por janela; o app é instância única (segunda abertura foca a janela existente).
- Se o motor encerrar inesperadamente: o host emite `engine_status = crashed`, reinicia uma vez e a UI pede `run.snapshot` das execuções abertas. Duas quedas seguidas → tela de diagnóstico com o caminho do log.
- Ao fechar a janela com execução em andamento: confirmação na própria UI; o host envia `engine.shutdown` e espera até 10 s antes de encerrar o processo.

---

## 4. Contratos compartilhados e validação cross-language

`packages/contracts/`:

```
schemas/
  protocol/envelope.v1.schema.json
  protocol/methods/*.schema.json      # params e result por método
  protocol/events/*.schema.json
  config/mariliz.schema.json          # mariliz.yaml
  config/mariliz-lock.schema.json
  manifests/{profile,agent,skill,connector,runtime-adapter}.schema.json
fixtures/                             # exemplos válidos e inválidos
generated/ts/                         # tipos TS gerados (não editados à mão)
```

| Linguagem | Como consome |
| --- | --- |
| TypeScript | Tipos gerados a partir dos schemas; validação em runtime das mensagens recebidas |
| C# | Records escritos à mão em `Mariliz.Application/Contracts`, com **teste de contrato** que serializa cada record e valida contra o schema, e desserializa cada fixture válida |
| Rust | Só a struct do envelope (serde) |

**[DECISÃO PENDENTE]** Ferramentas exatas de geração e validação (TS: `json-schema-to-typescript` + Ajv ou geração para Zod; C#: biblioteca de validação de JSON Schema). Critério: manutenção ativa e suporte ao draft 2020-12. A escolha fica para o início da Fase 1, com um protótipo de um método de ponta a ponta.

CI: um job falha se os tipos gerados estiverem desatualizados em relação aos schemas, ou se qualquer fixture deixar de validar em TS ou C#.

---

## 5. Contratos de Profiles, agentes e Skills

Todos os componentes distribuídos têm manifesto com: `id`, `version` (SemVer), `kind`, `compatibility`, `requires`, `provides`, `permissions`, `limitations`, `status` (`planned | experimental | implemented | validated | supported`) e `tests`.

### 5.1 Profile

```yaml
id: mariliz.profile.dotnet
version: 0.1.0
kind: profile
status: experimental
detect:                       # evidências exigidas; todas determinísticas
  anyOf:
    - file: "*.sln"
    - file: "*.slnx"
    - file: "**/*.csproj"
  versions:
    targetFramework: { from: "**/*.csproj", xpath: "//TargetFramework" }
compatibility:
  tested: ["net10.0"]          # só o que tem teste
  detectable: ["net8.0", "net9.0", "net10.0"]
commands:                      # propostos; confirmados pelo usuário no onboarding
  build: "dotnet build {solution}"
  test: "dotnet test {solution}"
provides:
  agents: [mariliz.agent.dotnet-implementer]
  skills: [mariliz.skill.dotnet-conventions]
limitations:
  - "Não detecta arquitetura por camadas automaticamente; apenas sugere"
```

### 5.2 Agente

```yaml
id: mariliz.agent.verifier
version: 0.1.0
kind: agent
status: experimental
purpose: "Julgamento independente pós-feature (autor ≠ verificador)"
inputs: [spec, tasks, diff]
outputs: [validation-report]
permissions:
  tools: [Read, Grep, Glob, Bash]
  write: [".mariliz/validation/**"]   # imposto por hook do runtime quando suportado
success: "Relatório com veredito e evidência arquivo:linha para cada critério"
evaluation: [mariliz.eval.verifier.seeded-defects]
runtimes:
  claude-code: { supported: true }
```

### 5.3 Skill

Mesmo cabeçalho, com `scope` (o que cobre e o que não cobre), `body` (Markdown do conteúdo) e `successCriteria`.

### 5.4 Adaptador de runtime

Converte componentes internos para o formato do runtime e declara uma matriz de capacidades:

| Capacidade | Claude Code |
| --- | --- |
| Agentes com lista de ferramentas | Sim (`.claude/agents/*.md`) |
| Skills | Sim (`.claude/skills/*/SKILL.md`) |
| Hooks bloqueantes (política imposta) | Sim (`settings.json` → hooks) |
| Permissões allow/deny | Sim |
| MCP por projeto | Sim (`.mcp.json`) |

Quando um runtime não suporta uma capacidade, o adaptador falha no `plan` com mensagem explícita, nunca degrada em silêncio.

---

## 6. Modelo de configuração e versionamento

### 6.1 Arquivos no projeto do cliente

```yaml
# mariliz.yaml — declarado e editável pelo usuário
schema: 1
project:
  name: EmpregaNet
profiles:
  - id: mariliz.profile.dotnet
    root: backend
  - id: mariliz.profile.nextjs
    root: frontend
runtime: claude-code
harness:
  agents: [mariliz.agent.verifier, mariliz.agent.code-reviewer]
  gates:
    test: { command: "dotnet test backend/EmpregaNet.sln" }
policies:
  requireApprovalFor: [push, pull-request, external-write]
```

```yaml
# mariliz.lock — gerado; nunca editado à mão
schema: 1
engine: 0.1.0
components:
  mariliz.profile.dotnet: { version: 0.1.0, hash: "sha256:…" }
  mariliz.agent.verifier: { version: 0.1.0, hash: "sha256:…" }
managedFiles:
  .claude/agents/verifier.md: { component: mariliz.agent.verifier, hash: "sha256:…" }
```

### 6.2 Aplicação idempotente com preservação de personalizações

Para cada arquivo gerenciado, o motor compara três versões: a registrada no lock (base), a que está no disco (atual) e a nova gerada (proposta).

| Situação | Ação |
| --- | --- |
| Proposta = atual | Nada a fazer |
| Atual = base (não foi personalizado) | Atualiza |
| Atual ≠ base (personalizado) e proposta ≠ base | **Conflito**: decisão do usuário na Revisão |
| Arquivo não gerenciado no caminho de destino | **Conflito**; nunca sobrescreve |
| Componente removido da configuração | Remove só se atual = base; caso contrário, conflito |

Antes de escrever, o motor faz backup dos arquivos afetados no histórico operacional (`runs/<runId>/backup/`) para permitir **Desfazer**. A escrita usa arquivo temporário + rename atômico. Repetir `apply` com a mesma configuração não produz nenhuma escrita.

### 6.3 Compatibilidade entre peças

UI ↔ motor: versão do protocolo (handshake). Motor ↔ componentes: `compatibility.engine` no manifesto. Motor ↔ projeto: `schema` do `mariliz.yaml` e do lock, com migração explícita quando mudar.

---

## 7. Gerenciamento de MCP (Fase 4)

- Catálogo **curado** de servidores MCP com manifesto próprio (origem, licença, versão fixada, transporte, permissões, comandos de verificação). Não há instalação a partir de busca livre.
- Ciclo: descobrir os já configurados (`.mcp.json`) → propor → instalar a versão fixada → testar (listar ferramentas expostas) → verificar permissões → registrar no lock → atualizar ou remover com o mesmo fluxo de Plan/Apply.
- Credenciais de MCP vêm do cofre do SO e entram no processo do servidor por variável de ambiente no momento da execução; nunca em `.mcp.json` versionado.
- Servidor MCP remoto (HTTP) exige confirmação explícita e mostra o domínio.

---

## 8. Arquitetura dos conectores (Fase 4 e 6)

```csharp
public interface IConnector
{
    ConnectorDescriptor Describe();             // id, versão, recursos, auth, permissões, limitações
    Task<CheckResult> CheckAsync(CheckLevel level, CancellationToken ct);
    // level: ConfigValid | Authenticated | Reachable | OperationsVerified
}

public interface IReadableSource<TEntity>  { IAsyncEnumerable<TEntity> ReadAsync(Query q, CancellationToken ct); }
public interface IWritableTarget<TEntity>  { Task<WriteResult> WriteAsync(TEntity e, IdempotencyKey key, CancellationToken ct); }
```

- Um conector só declara `IWritableTarget` quando a escrita existir e tiver teste.
- Cada escrita carrega chave de idempotência derivada de (origem, id externo, versão). Migrações gravam checkpoint por lote e reconciliam no fim.
- Quando a API de destino não permite rollback, o descritor declara isso e a Revisão mostra o aviso.
- Bancos: conectores somente leitura no MVP das integrações, com timeout, limite de linhas e lista de schemas permitidos. Escrita, DDL e administração não existem como operação.

---

## 9. Segurança e credenciais

| Requisito | Como é imposto |
| --- | --- |
| Menor privilégio no host | Capabilities do Tauri limitadas aos 3 comandos; sem plugin shell nem fs no frontend; CSP restritiva no WebView |
| Pastas autorizadas | Só pastas escolhidas no diálogo nativo entram na lista `workspace`; todo caminho é normalizado e verificado contra a raiz autorizada (protege contra path traversal e links simbólicos que saem da raiz) |
| Execução de processos | O motor executa somente comandos declarados no `mariliz.yaml` aprovado ou no profile, com timeout, diretório de trabalho fixo e ambiente limpo |
| Segredos | Cofre do SO via `ISecretStore`; nunca em log, mensagem de protocolo, prompt, arquivo versionado ou relatório. **[DECISÃO PENDENTE]** biblioteca .NET multiplataforma de cofre |
| Logs sanitizados | Filtro de redação central no logger (padrões de token, connection string, chaves conhecidas) com teste próprio |
| Conteúdo não confiável | Arquivos importados, saídas de ferramentas e respostas de serviços são dados; nunca alteram política nem permissões |
| Aprovação | Toda escrita no projeto ou em serviço externo passa pelo estado "Aguardando aprovação" |
| Rastreabilidade | Cada execução grava: entrada, plano aprovado, escritas, validações, resultado e versões (motor, componentes) |

---

## 10. Memória local por projeto

| Conteúdo | Local |
| --- | --- |
| Especificações, decisões, casos de regressão, evals do projeto | `.mariliz/` no repositório (versionado) |
| Índice de busca (se houver RAG) | `.mariliz/cache/` (ignorado pelo git, reconstruível) |
| Histórico de execuções e logs | Diretório de dados do usuário, por `projectId` |

Isolamento: o motor nunca lê a memória de outro projeto numa execução. O `projectId` é derivado do caminho canônico + id gerado na primeira configuração.

---

## 11. Feedback, regressões e Evals

| Tier | O que | Onde roda | MVP |
| --- | --- | --- | --- |
| 0 — Estrutural | `mariliz.yaml` e lock válidos, manifestos íntegros, referências resolvem, permissões dos agentes coerentes com o manifesto, hashes dos arquivos gerenciados, ausência de segredos | Motor e CLI (`mariliz validate`), CI do cliente | Sim |
| 1 — Funcional | Discovery, plan, apply idempotente, conflitos, retomada após falha | Testes do próprio produto (xUnit, fixtures de projetos sintéticos) | Sim |
| 2 — Integração | Conectores, MCP, bancos de teste descartáveis | CI do produto com serviços em container | Fase 4 |
| 3 — Comportamental | Agentes contra defeitos semeados, fidelidade de especificações, recall de RAG | Runner dedicado, N execuções, comparação com baseline | Fase 7 |

Registro de eventos de engenharia: JSONL local com `schema` versionado (`run.started`, `step.*`, `validation.result`, `harness.version`), base das métricas futuras. Nenhuma métrica é exibida antes de ter denominador e fonte definidos.

Evolução do harness: uma falha vira caso de regressão no projeto; a melhoria fica no projeto até passar no Tier 3 em mais de um projeto e ser autorizada para o núcleo.

---

## 12. Estratégia de extensibilidade

- Profiles, agentes, skills, adaptadores de runtime e manifestos de MCP são **pacotes de conteúdo** (YAML + Markdown + schemas), carregados pelo motor e validados no Tier 0. Adicionar um novo não exige recompilar o motor.
- Conectores são **código** (.NET) e, no início, ficam no repositório principal. Um modelo de plugin binário só será criado quando houver um conector de terceiro real.
- Não existe extensão em Rust nem em TypeScript.

---

## 13. Decisão Tauri 2 + React/TypeScript/Vite + motor .NET 10

Adotada (justificativa em [`product-discovery.md`](product-discovery.md), seção 6.1). Restrições adicionais:

- O desktop é uma SPA Vite, sem React Router framework mode, sem pré-renderização e sem SSR (diferente do Mariliz Web, que usa `ssr: false` + `prerender` para SEO).
- React Router em modo de dados (SPA). Zod e React Hook Form já são dependências da Synthra/Mariliz; nenhuma biblioteca de estado global no MVP (estado do motor via eventos, estado local via React).
- GSAP e bibliotecas de animação do Mariliz Web não entram.

---

## 14. Empacotamento por sistema operacional e arquitetura

| Alvo | RID .NET | Target triple (nome do sidecar) | Formato do instalador | Validação |
| --- | --- | --- | --- | --- |
| Windows x64 | `win-x64` | `x86_64-pc-windows-msvc` | NSIS (`.exe`) e/ou MSI | Máquina local + CI |
| macOS Apple Silicon | `osx-arm64` | `aarch64-apple-darwin` | `.dmg` / `.app` | CI (runner macOS arm64) |
| macOS Intel | `osx-x64` | `x86_64-apple-darwin` | `.dmg` / `.app` | CI; **[DECISÃO PENDENTE]** manter ou não |
| Linux x64 | `linux-x64` | `x86_64-unknown-linux-gnu` | AppImage e `.deb` | CI (Ubuntu) |
| Linux ARM64 | `linux-arm64` | `aarch64-unknown-linux-gnu` | — | Depois de verificar dependências do WebKitGTK |

Processo por alvo: `dotnet publish src/Mariliz.Engine -r <RID> --self-contained -p:PublishSingleFile=true` → renomear para `mariliz-engine-<triple>[.exe]` em `apps/desktop-host/src-tauri/binaries/` → `tauri build --target <triple>`. O usuário final não precisa do SDK .NET.

Dependências e cuidados por plataforma:

- **Windows:** WebView2 (presente no Windows 11; o instalador do Tauri pode incluir o bootstrapper). Sem assinatura Authenticode, o SmartScreen alerta.
- **macOS:** o sidecar também precisa ser assinado e o app notarizado para não ser bloqueado pelo Gatekeeper; exige conta Apple Developer.
- **Linux:** WebKitGTK 4.1 e bibliotecas relacionadas no sistema do usuário (AppImage reduz o problema).
- **Atualização automática** (`tauri-plugin-updater`) exige chave de assinatura dos pacotes e um local de publicação; fica para a Fase 8.
- Tamanho do instalador: medir na Fase 1 antes de decidir sobre trimming ou NativeAOT.

---

## 15. Integração da `@synthra.io/ui-kit`

- **[FATO]** Versão 0.5.0, MIT, ESM, `sideEffects: false`. Peers: `react` 18|19, `@mui/material` ^9, `@mui/x-data-grid` ^9, `@emotion/react`/`styled` ^11, `react-hook-form` ^7.55, `next` opcional (o subpath `./next` não é importado pelo entry principal — verificado: nenhum import de `next` em `dist/index.js`).
- Instalar como dependência de `apps/desktop-ui` com versão **exata** (`0.5.0`), atualizada por PR dedicado.
- `packages/ui` contém só wrappers que a Synthra não cobre (`ActionCard`, `WizardSteps`, `DiffView`, `ProjectPicker`), compostos com MUI e com os tokens do tema; nenhuma cópia de componente da Synthra.
- Deduplicação: `pnpm` com `react`, `react-dom`, `@mui/material` e `@emotion/*` declarados uma vez; teste de build verifica uma única instância (`pnpm why` no CI).
- Tema: `initializeTheme` com overrides completos de paleta, seguindo a nota do Mariliz Web de que a Synthra não recalcula tons derivados.
