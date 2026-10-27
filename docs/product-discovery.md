# Mariliz AI Engineering — Product Discovery

**Fase:** 0 (Discovery e planejamento). **Data:** 2026-10-25. **Estado:** aguardando aprovação; nada foi implementado.

Convenção de rótulos usada em todos os documentos desta pasta:

| Rótulo | Significado |
| --- | --- |
| **[FATO]** | Inspecionado ou executado nesta fase, com a evidência citada |
| **[INFERÊNCIA]** | Deduzido de fatos, com o passo explícito |
| **[HIPÓTESE]** | Não verificado; precisa de confirmação |
| **[DECISÃO PENDENTE]** | Depende do responsável pelo produto |

Documentos relacionados: [`experience-blueprint.md`](experience-blueprint.md), [`architecture-blueprint.md`](architecture-blueprint.md), [`implementation-roadmap.md`](implementation-roadmap.md).

---

## 1. Estado atual do ambiente

### 1.1 Repositório do produto

- **[FATO]** `MyApps/frontend/mariliz-ai-engineering/` existe, com `git init` e **nenhum commit**. Estes quatro documentos são os primeiros arquivos.
- **[FATO]** Não há código, CI, licença nem `README.md` no repositório.

### 1.2 Ferramentas na máquina de desenvolvimento (Windows 11 x64)

| Ferramenta | Versão detectada | Situação para o produto |
| --- | --- | --- |
| .NET SDK | 10.0.402 | Suficiente para o motor e a CLI |
| Node.js | 25.2.1 | Atende `engines >= 22` do Mariliz Web |
| pnpm | 9.1.2 | Disponível; o Mariliz Web usa **npm** (`package-lock.json`) e a Synthra declara `pnpm@9.12.2` |
| npm | 11.6.2 | — |
| Python | 3.12.10 | Necessário só para reaproveitar gates em Python (decisão na seção 6) |
| Git / gh | 2.56.0 / 2.50.0 | — |
| WebView2 | 155.0.4283.62 | Runtime do Tauri no Windows presente |
| **Rust (rustc/cargo)** | **ausente** | **Bloqueia a Fase 1** — instalar via `rustup` |
| **MSVC C++ Build Tools** | **não detectado** (`vswhere` sem componente VC.Tools) | **Bloqueia a compilação do Tauri no Windows** |

- **[FATO]** Não há máquina macOS nem Linux disponível. Builds dessas plataformas só podem ser validadas em CI (runners hospedados) ou em máquinas a providenciar.

### 1.3 Versões publicadas relevantes (consultadas no registro npm em 2026-10-25)

| Pacote | Versão |
| --- | --- |
| `@tauri-apps/cli`, `@tauri-apps/api` | 2.12.4 |
| `@tauri-apps/plugin-dialog` | 2.8.3 |
| `@tauri-apps/plugin-shell` | 2.5.0 |
| `@tauri-apps/plugin-updater` | 2.14.0 |
| `@synthra.io/ui-kit` | 0.5.0 (publicada em 2026-09-30) |

As versões dos crates Rust correspondentes não foram consultadas; serão fixadas no `Cargo.lock` da Fase 1.

---

## 2. Evidências sobre os Harnesses existentes

Base: a auditoria de 2026-10-22 (Artifact "Auditoria Mariliz AI Engineering") revalidada nesta fase.

| Projeto | Stack | Agentes | Método | Evals | Revalidação em 2026-10-25 |
| --- | --- | --- | --- | --- | --- |
| Easymart.IntegradorAnymarket | .NET 10 | 7 | TLC v3.3.0 | Lint Tier 0 em Python (9 regras), CI | **[FATO]** `python .claude/evals/harness_lint/lint.py` → `harness lint: OK (9 regras)`. 11 features em `.specs/features` (eram 8). Arquivos de agentes alterados desde 2026-10-22 |
| Backoffice.Web | Next.js | 5 | TLC | RAG eval 22/23 (não roda no CI) | Sem alterações de harness desde 2026-10-22 |
| Easymart.CRM | Angular 19 | 3 | TLC | Nenhum no CI | 29 arquivos de harness alterados desde 2026-10-22; 24 features |
| EmpregaNetAPI | .NET 10 + Next.js | 8 | SDD próprio | `check-harness.mjs` + `audit-traces.mjs` | **[FATO]** `node .claude/evals/check-harness.mjs` → `harness ok — 8 agentes, 6 skills, 17 documentos verificados` |

Conclusões que continuam válidas:

- **[FATO]** A orquestração atual é prosa seguida pela sessão do Claude Code. Não existe motor de execução, estado persistido, idempotência nem cancelamento.
- **[FATO]** Os gates de entrega (Jira, PR, sem atribuição de IA) são reforçados por instrução, não por hook.
- **[FATO]** Os harness continuam evoluindo por cópia: o CRM e o Integrador mudaram nos últimos três dias sem fonte comum. A divergência entre forks cresce a cada semana em que o núcleo não existe.

---

## 3. Classificação dos ativos

Classificação da seção 3.1 do briefing. "Uso no produto" indica se o ativo pode servir de referência técnica ou de base de código, sempre sujeito à seção 4.

| Ativo | Classe | Onde está | Uso no produto |
| --- | --- | --- | --- |
| Contrato de evidência (`harness-contract`: FACT/INFERENCE/ASSUMPTION/UNKNOWN, confiança HIGH/MEDIUM/LOW, bloco YAML) | **Core compartilhado** | Integrador, EmpregaNet (versões diferentes) | Reescrever como contrato da Mariliz, a partir do conceito |
| Padrão de autoria de agentes (frontmatter + 9 seções fixas) | **Core compartilhado** | `agents/README.md` dos 4 projetos | Base do manifesto de agente |
| Agentes `verifier`, `code-reviewer`, `debug-specialist` | **Core compartilhado** (declarados "stack-agnostic") | 3 a 4 forks divergentes | Consolidar numa versão de referência |
| Lint estrutural do harness (H-01 a H-09; regras do `check-harness.mjs`) | **Core compartilhado** | Integrador (Python), EmpregaNet (Node) | Reimplementar em .NET no motor (Tier 0) |
| Leitor de transcripts (`audit-traces.mjs`) | **Experimental** | EmpregaNet | Referência para evals de trajetória |
| Desenho de evals (`DESIGN.md`, 52 KB) | **Experimental** (só o Tier 0 foi construído) | Integrador | Referência para a estratégia de evals |
| `dotnet-architect`, `dotnet-implementer`, `test-engineer`, `performance-optimizer`, `backend-skill` | **Profile .NET** | Integrador, EmpregaNet | Destilar o profile `.NET` |
| `frontend-engineer`, `frontend-skill`, `frontend-conventions` | **Profile Next.js/React** | Backoffice.Web, EmpregaNet | Destilar o profile `nextjs` |
| `easymart-frontend-skill` | **Profile Angular** (com conteúdo de projeto) | CRM | Fase 5 |
| Skills de QA E2E pela UI (`e2e-qa-skill`, `e2e-regression`) | **Experimental** | CRM, Backoffice.Web, EmpregaNet | Fora do MVP |
| `integrador-backend-skill`, `legado-flutter`, ADs em `.specs/STATE.md`, roadmaps, Jira ESM0036/ESM0026/BAC | **Project-specific** | Projetos Easymart | **Não reutilizar** |
| RAG do Backoffice (código Dart copiado, dossiês, `extracted/*.json`) | **Project-specific** | Backoffice.Web | **Não reutilizar** conteúdo; a arquitetura serve só como referência |
| Arquitetura do RAG (extração determinística → dossiês ancorados → busca híbrida → eval recall@k + âncoras com hash) | **Experimental** | Backoffice.Web | Referência para a Fase 6 |
| MCPs `anymarket-db`, `anymarket-sandbox` | **Project-specific** | Integrador | Não reutilizar; o padrão "dbhub `readonly=true` + `max_rows`" é referência |
| TLC spec-driven v3.3.0 (Felipe Rodrigues, CC-BY-4.0) | **Third-party** | 3 projetos | Ver seção 4 |
| Regras "AWS Agent Toolkit" | **Third-party** | CLAUDE.md do EmpregaNet | Não incluir |
| `shadcn` MCP, `mermaid-mcp`, `@bytebase/dbhub` | **Third-party** | `.mcp.json` | Avaliar licença e manutenção caso a caso |
| `@synthra.io/ui-kit` 0.5.0 (MIT, autoria "Synthra", repositório `Fabinschulz/synthra-components-library`) | **Third-party / dependência** | npm | Dependência visual principal |
| Mariliz Web (organização de frontend, tema, ESLint, Vitest) | Referência técnica | `MyApps/frontend/mariliz` | Padrões, não código copiado em bloco |

---

## 4. Ativos que exigem autorização ou investigação de licença

| Ativo | Pergunta em aberto | Bloqueia |
| --- | --- | --- |
| Tudo que nasceu nos repositórios Easymart (agentes, skills, lint Python, `DESIGN.md`) | A Mariliz pode reutilizar e comercializar? Existe contrato de cessão ou de prestação de serviço que trate de PI? | Consolidação do núcleo (Fase 2) |
| EmpregaNetAPI (em `MyApps`) | Quem detém os direitos? É projeto pessoal, da Mariliz ou de cliente? | Uso de `check-harness.mjs` e do profile .NET derivado |
| TLC spec-driven | CC-BY-4.0 permite uso comercial com atribuição e indicação de mudanças. Falta decidir se o produto **redistribui** o TLC, se instrui o cliente a instalá-lo pelo instalador oficial, ou se usa um método próprio | Escolha do método do núcleo |
| Synthra UI | Licença MIT. Falta confirmar se "Synthra" e a Mariliz são a mesma titular e se há plano de versão estável (hoje 0.x) | Nenhum para o MVP; afeta a política de atualização |
| Dependências de terceiros que o produto vai instalar no projeto do cliente (MCPs, servidores) | Licença, manutenção e superfície de permissão de cada uma | Fase 4 |

**Regra aplicada nesta fase:** nenhum arquivo dos projetos de referência foi copiado para este repositório. Os lints foram apenas executados, em modo leitura.

---

## 5. Lacunas técnicas

| Lacuna | Impacto |
| --- | --- |
| Não existe núcleo único; os agentes genéricos têm 3 ou 4 versões divergentes | Sem uma versão de referência, o produto não tem o que distribuir |
| Os gates de entrega dependem de instrução | O produto precisa impor política por código (motor e hooks do runtime), não por prosa |
| Não há evals comportamentais (Tier 1+) em nenhum projeto | Não há como afirmar a qualidade dos agentes distribuídos |
| Dois métodos concorrentes (TLC e SDD próprio) | O núcleo precisa escolher um ou suportar ambos via profile de método |
| O harness inteiro pressupõe o formato do Claude Code (`.claude/`, `tools:`, `.mcp.json`) | É preciso um adaptador de runtime e uma matriz de capacidades |
| Lints em duas linguagens (Python, Node), nenhum em .NET | O motor .NET precisa reimplementar as regras, ou invocar scripts externos (dependência de runtime extra) |
| Nenhuma métrica de custo, retrabalho ou intervenção humana é registrada hoje | Não há baseline para comparar versões do harness |
| Rust e MSVC Build Tools ausentes; sem macOS/Linux | A Fase 1 começa por preparar o ambiente e o CI |

---

## 6. Arquitetura geral recomendada

A decisão de referência do briefing é adotada: **Tauri 2 + React/TypeScript/Vite + Synthra UI + motor .NET 10 (sidecar) + CLI .NET compartilhando a camada Application**. O detalhamento está em [`architecture-blueprint.md`](architecture-blueprint.md).

```
┌──────────────────────── Aplicativo desktop ────────────────────────┐
│ desktop-ui (React + Vite + Synthra)                                 │
│    │  invoke("engine_request") / listen("engine_event")             │
│ desktop-host (Tauri 2, Rust mínimo)                                 │
│    │  stdin/stdout NDJSON versionado (contrato em packages/contracts)│
│ Mariliz.Engine (.NET 10, sidecar, sem UI)                           │
└─────┬───────────────────────────────────────────────────────────────┘
      │ mesma camada Application
Mariliz.Cli (.NET 10) ── automação, CI, usuários avançados
      │
Mariliz.Application → Mariliz.Core ← Mariliz.Infrastructure
                                       (FS, processos, git, keychain,
                                        conectores, runtime adapters)
```

Princípios:

1. **Regras só no .NET.** A UI apresenta e coleta; o Rust só gerencia janela, permissões e o processo do motor.
2. **O motor é a autoridade.** Permissões, aprovação, idempotência e validação são impostas pelo motor, nunca pela UI nem por instrução a um agente.
3. **Projeto do cliente é a fonte da verdade.** O que é versionado vive no repositório do cliente (`mariliz.yaml`, `mariliz.lock`, artefatos gerados). O motor não mantém estado oculto que o projeto não consiga reconstruir.

### 6.1 Alternativas e trade-offs

| Decisão | Escolhida | Alternativas avaliadas | Por quê |
| --- | --- | --- | --- |
| Host desktop | **Tauri 2** | Electron; Avalonia (.NET puro); .NET MAUI; WPF/WinForms | Tauri usa o WebView do sistema (pacote menor que Electron) e permite reaproveitar React + Synthra. Avalonia eliminaria Rust e o sidecar, mas obrigaria a abandonar a Synthra. MAUI não suporta Linux. WPF/WinForms só rodam no Windows. **Custo assumido:** uma terceira linguagem (Rust) e o empacotamento do sidecar |
| Comunicação host ↔ motor | **stdin/stdout, NDJSON** | HTTP em loopback; named pipes / Unix sockets; gRPC | stdio não abre porta nem exige token efêmero e morre junto com o processo pai. NDJSON é trivial de depurar. Pipes nomeados exigem código por plataforma |
| Contrato entre linguagens | **JSON Schema versionado + geração de tipos + testes de fixtures** | Protobuf; tipos mantidos à mão | JSON Schema funciona nas três linguagens e se valida em teste. Protobuf é mais rígido do que o necessário para um único processo local |
| Formato do motor | **`dotnet publish` self-contained por RID** | NativeAOT; framework-dependent | Self-contained dispensa o runtime .NET na máquina do usuário. NativeAOT reduz tamanho e tempo de início, mas restringe reflexão e bibliotecas (YAML, serialização); avaliar depois de medir |
| Gerenciador JS | **pnpm workspaces, um lockfile** | npm workspaces | A Synthra já usa pnpm; pnpm evita instâncias duplicadas de React/MUI/Emotion em monorepo. O Mariliz Web usa npm, mas é um repositório separado |
| Runtime de IA alvo | **Claude Code primeiro**, via adaptador | Agnóstico desde o início | Todos os ativos existentes estão nesse formato. O contrato interno da Mariliz fica separado do formato de saída |

---

## 7. Modelo local-first

| Camada | Onde vive | Versionado no projeto? |
| --- | --- | --- |
| Configuração declarativa (`mariliz.yaml`) | Raiz do projeto | Sim |
| Manifesto de instalação (`mariliz.lock`) | Raiz do projeto | Sim |
| Memória de engenharia (specs, decisões, casos de regressão, evals) | `.mariliz/` no projeto | Sim |
| Artefatos gerados para o runtime (`.claude/**`) | Projeto | Sim, marcados como gerenciados |
| Histórico operacional (execuções, logs, relatórios) | Diretório de dados do usuário (`%APPDATA%\Mariliz`, `~/Library/Application Support/Mariliz`, `~/.local/share/mariliz`) | Não |
| Arquivos temporários de importação | Diretório temporário controlado, com retenção | Não |
| Segredos | Cofre do sistema operacional (Windows Credential Manager, macOS Keychain, Secret Service/libsecret) | Nunca |

**O que não é local, e deve ser dito ao usuário sempre que acontecer:** chamadas a provedores de modelo, autenticação OAuth no navegador, chamadas às APIs de Jira/GitHub/Azure DevOps, downloads de servidores MCP. Nada disso é necessário para o fluxo de configuração do MVP, exceto se o usuário habilitar uma integração.

**Telemetria:** nenhuma no MVP.

---

## 8. Escopo do MVP

### 8.1 Tensão com a auditoria anterior

A auditoria de 2026-10-22 recomendou começar por uma CLI, porque a hipótese de valor ainda não foi validada. O briefing atual exige um aplicativo desktop. As duas coisas são compatíveis desde que **o desktop seja uma casca fina sobre a mesma camada Application usada pela CLI**. O MVP abaixo segue essa linha: todo caso de uso nasce no motor, é coberto por testes e pode ser chamado pela CLI; a UI só o expõe.

**[INFERÊNCIA]** O desktop acrescenta à Fase 1 três frentes que não existem na CLI: o host Rust, o empacotamento do sidecar por plataforma e a assinatura de instaladores. Isso atrasa a validação da hipótese de valor; o roadmap mantém essas frentes no menor tamanho possível.

### 8.2 Escopo

| Dentro | Fora |
| --- | --- |
| Janela inicial da seção 5.4 do briefing, com os 4 cartões e o seletor de pasta | Dashboard, login, conta, preços |
| Jornada **Configurar projeto** completa: discovery → recomendação → revisão com diff → aplicar → validar → relatório | Jornadas Importar requisitos, Conectar ferramentas e Modernizar sistema (cartões visíveis, marcados como "Em breve") |
| Profiles `.NET` e `nextjs` | Angular, Flutter, React Native |
| Núcleo genérico mínimo: `verifier`, `code-reviewer`, contrato de evidência | Agentes de QA pela UI, performance, arquitetura |
| Adaptador de runtime Claude Code | Outros runtimes |
| `mariliz.yaml` + `mariliz.lock`, aplicação idempotente, detecção de conflito, preservação de personalizações | Atualização automática do produto |
| Lint Tier 0 do harness gerado, rodando no motor e na CLI | Evals com LLM (Tier 3) |
| CLI com `discover`, `plan`, `apply`, `validate` | Workflows automáticos, gatilhos por evento |
| Instalador Windows x64; builds macOS/Linux compilando no CI | Assinatura e notarização em produção (avaliado, não obrigatório no MVP) |

### 8.3 Critérios de aceite do MVP

Os 23 critérios da seção 22 do briefing, filtrados para o escopo acima, estão em [`implementation-roadmap.md`](implementation-roadmap.md), seção 4, com o teste que comprova cada um.

---

## 9. Riscos

| Risco | Probabilidade | Impacto | Mitigação |
| --- | --- | --- | --- |
| Titularidade dos ativos não autorizada | Média | Alto: o núcleo teria de ser reescrito do zero | Decidir antes da Fase 2; Fase 1 não depende de nenhum ativo de cliente |
| Dependência do formato do Claude Code | Alta | Alto: mudanças do runtime quebram o harness gerado | Adaptador isolado, testes de contrato do formato gerado, matriz de capacidades por runtime |
| Custo da terceira linguagem (Rust) | Alta | Médio | Rust restrito a ~3 comandos e ao ciclo de vida do sidecar; sem regra de negócio |
| Empacotamento e assinatura em 3 sistemas | Alta | Médio | CI matricial desde a Fase 1; assinatura tratada como decisão separada |
| Synthra 0.x muda API entre versões | Média | Médio | Versão fixa, wrappers finos em `apps/desktop-ui/src/shared/components/ui`, atualização controlada |
| Lacunas da Synthra (sem `Card`, sem `Stepper`, sem ícones de pasta/código/documento) | Certa | Baixo | Compor com MUI já instalado (peer da Synthra) ou contribuir os ícones na própria Synthra |
| Conteúdo de projeto e instruções em arquivos importados tentarem ampliar permissões (prompt injection) | Média | Alto | Política imposta pelo motor; conteúdo importado tratado como dado |
| Tamanho do instalador (WebView + motor self-contained) | Média | Baixo | Medir na Fase 1; avaliar trimming e NativeAOT |
| Demanda comercial não comprovada | — | Alto | Critérios de continuidade do roadmap |

---

## 10. Decisões pendentes

Em ordem de prioridade. Detalhes em [`implementation-roadmap.md`](implementation-roadmap.md), seção 6.

1. **[DECISÃO PENDENTE]** Titularidade: o que dos projetos Easymart e EmpregaNet pode alimentar o núcleo?
2. **[DECISÃO PENDENTE]** Método do núcleo: TLC (redistribuído com atribuição ou instalado pelo canal oficial) ou método próprio da Mariliz.
3. **[DECISÃO PENDENTE]** Assinatura de código: certificado Authenticode e conta Apple Developer para o primeiro instalador, ou instaladores não assinados em fase de avaliação.
4. **[DECISÃO PENDENTE]** Licença do próprio repositório `mariliz-ai-engineering` (proprietária, source-available ou outra).
5. **[DECISÃO PENDENTE]** Ícones faltantes: contribuir na Synthra (pasta, código, documento, conexão) ou compor com ícones existentes.
6. **[DECISÃO PENDENTE]** Tema inicial do aplicativo: claro, escuro, ou seguir o sistema (o Mariliz Web usa o escuro).
