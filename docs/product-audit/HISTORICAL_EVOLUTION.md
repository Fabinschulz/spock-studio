# Evolução histórica: de Mariliz a SPOCK

> Parte da auditoria estratégica do SPOCK (2026-11-15). Navegação e método em [README.md](README.md).
> Somente leitura: nenhum código foi alterado. Legenda de estados: A implementado e validado, B parcial, C só documentado,
> D não implementado, E substituído por melhoria válida, F substituído sem justificativa, G regressão, H conflito entre
> documento e código, I decisão pendente, J não se aplica.

Auditor: domínio "visão e histórico". Data: 2026-11-15/16. Somente leitura (nenhum arquivo do repositório foi alterado).

## 0. Fontes e método

| Fonte | Cobertura | Limitação |
| --- | --- | --- |
| histórico de conversas do Claude Code (fora do repositório) (419 mensagens, 35 sessões, 2026-10-29T01:14Z → 2026-11-16T00:43Z) | Lida inteira (por índice de cabeçalhos e leitura direta dos trechos com conteúdo) | (1) **Começa em 29/10**: o briefing original, a jornada TLC/Harness Engineer (26 a 28/10) e todo o trabalho de 31/10 (separação `intelligence/`, RAG lexical, chave de modelo, CPython embarcado) **não têm prompt registrado**; o commit `591ebe8` (regra `.cursor/`) sugere que parte foi feita no Cursor (não verificado). (2) Os prompts colados (`pasted_content`) estão **truncados** em ~150 linhas. (3) Muitas mensagens aparecem duplicadas em 2 a 4 IDs de sessão (forks/continuações); contei cada uma uma vez. (4) Os blocos "This session is being continued…" são resumos escritos pelo assistente, não palavras do dono; uso-os só como evidência secundária e marco assim ("resumo"). |
| `git log` (378 commits, a488ee9 em 2026-10-26 → 06443a5 em 2026-11-15) | Completo; PRs #1-#108 mergeados (faltam #58, #76, #81 nos merges; #109 aberto na branch atual) | Datas de commit em -03:00; datas de prompt em UTC. |
| Documentos em `docs/` | Lidos os estratégicos (discovery, roadmap, blueprints, ADR-0001, auditoria agentic, plataforma/*, compliance/LICENSE_DECISIONS e TRADEMARK, validação real, estado atual) | `THIRD_PARTY_NOTICES.md`, SBOMs e inventários não foram lidos (fora do domínio). |
| Memória do dono (`~/.claude/projects/.../memory/*.md`) | Lida para cruzar datas e decisões | É escrita pelo assistente; tratada como evidência secundária. |

Notação de fonte: `P <timestamp UTC> [sessão]` = prompt do dono; `R <timestamp> [sessão]` = resumo de continuação; `arquivo:linha` = linha no arquivo atual (HEAD 06443a5), salvo quando indicado `a488ee9:arquivo`; `<hash>` = commit.

---

## A. Visão original (2026-10-25/26, commit a488ee9)

O primeiro commit (`a488ee9`, 2026-10-26 20:26 -03) traz quatro documentos de discovery datados de **2026-10-25**, "aguardando aprovação; nada foi implementado" (`docs/product-discovery.md:5`), um spike de protocolo NDJSON em .NET 10 e a janela inicial React sobre a Synthra. Eles citam um **briefing** anterior ("seção 5.4 do briefing", "23 critérios da seção 22") e uma **auditoria de 2026-10-22** ("Artifact 'Auditoria Mariliz AI Engineering'"), que **não estão no repositório** (não verificados).

### A.1 Nome e propósito
- Nome: **"Mariliz AI Engineering"** (`a488ee9:docs/product-discovery.md:1`). "Mariliz" aparece também como titular/marca de outros ativos ("Mariliz Web", "se 'Synthra' e a Mariliz são a mesma titular", `product-discovery.md` tabela §3 e §4).
- Problema de partida (FATO no discovery): os Harnesses dos projetos do dono (Easymart.IntegradorAnymarket, Backoffice.Web, Easymart.CRM, EmpregaNetAPI) "continuam evoluindo por cópia", sem núcleo comum; "a orquestração atual é prosa seguida pela sessão do Claude Code. Não existe motor de execução, estado persistido, idempotência nem cancelamento"; "os gates de entrega (Jira, PR, sem atribuição de IA) são reforçados por instrução, não por hook" (`product-discovery.md` §2, linhas ~95 a 110).
- Propósito implícito: um produto que **consolida e distribui um Harness de engenharia com IA** (agentes, skills, regras, hooks, evals) com política imposta por código, aplicado de forma idempotente e reversível ao repositório do cliente.

### A.2 Usuários-alvo (`docs/blueprints/experience-blueprint.md:11-14`)
Gestor/PO ("transformar requisitos em backlog e roadmap revisáveis"), arquiteto/tech lead ("fixar padrões do projeto e garantir que agentes os respeitem"), desenvolvedor ("ter o harness funcionando no repositório sem montá-lo à mão") e equipe de modernização ("entender o legado antes de reescrever").

### A.3 Jornadas/pilares originais (`experience-blueprint.md:190-193`)
1. **Configurar projeto**: "Analisar o repositório e configurar o Harness." (única no MVP)
2. **Importar requisitos**: "Gerar especificações, backlog e roadmap a partir de documentos ou ferramentas existentes." (Fase 3)
3. **Conectar ferramentas**: "Configurar Jira, Azure DevOps, GitHub, MCPs e bancos de dados." (Fase 4)
4. **Modernizar sistema**: "Analisar um legado e planejar sua modernização." (Fase 6)

### A.4 Capacidades e escopo do MVP (`product-discovery.md:300-310`)
Dentro: jornada Configurar completa (discovery → recomendação → revisão com diff → aplicar → validar → relatório); profiles `.NET` e `nextjs`; núcleo mínimo (`verifier`, `code-reviewer`, contrato de evidência); "Adaptador de runtime Claude Code"; `mariliz.yaml` + `mariliz.lock`; "Lint Tier 0 do harness"; CLI `discover/plan/apply/validate`; instalador Windows x64.
Roadmap (`docs/implementation-roadmap.md`): Fase 2 núcleo e profiles; 3 requisitos/documentação; 4 integrações (cofre, catálogo MCP, GitHub/Jira leitura primeiro, bancos só leitura, `:114`); 5 expansão de stacks; 6 modernização com "RAG local só quando a busca simples não bastar (com eval de recall desde o primeiro dia)" (`:130-132`); 7 feedback/automação; 8 distribuição e comercial (`:146`).

### A.5 Não-objetivos (`product-discovery.md:302` e `implementation-roadmap.md:272`)
"Dashboard, login, conta, preços"; "Importação de documentos, integrações (Jira, Azure DevOps, GitHub, MCP, bancos), migração, modernização, RAG, Angular/Flutter/React Native, evals com LLM, métricas, workflows automáticos e gatilhos por evento, atualização automática, assinatura obrigatória, telemetria, nuvem, contas, licenciamento." Telemetria: "nenhuma no MVP" (`product-discovery.md:272`). Critério de continuidade: avançar às Fases 3+ só se "pelo menos um usuário ou equipe fora do desenvolvimento usou o produto" (`implementation-roadmap.md:219`).

### A.6 Intenção arquitetural (`product-discovery.md:201-244`)
- "**Tauri 2 + React/TypeScript/Vite + Synthra UI + motor .NET 10 (sidecar) + CLI .NET compartilhando a camada Application**" (`:201`); "o desktop seja uma casca fina sobre a mesma camada Application usada pela CLI" (`:288`).
- Princípios: "**Regras só no .NET.**" (`:227`); "**O motor é a autoridade.** Permissões, aprovação, idempotência e validação são impostas pelo motor, nunca pela UI nem por instrução a um agente" (`:228`); "**Projeto do cliente é a fonte da verdade**" (`:229`).
- Host ↔ motor: "**stdin/stdout, NDJSON**", HTTP em loopback **rejeitado** para esse trecho ("stdio não abre porta nem exige token efêmero", `:240`). Contrato: JSON Schema versionado + fixtures (`:241`). Runtime de IA alvo: "**Claude Code primeiro**, via adaptador" (`:244`).
- **Python não fazia parte da arquitetura**: "Necessário só para reaproveitar gates em Python" (`:53`). Não havia módulo de IA, RAG nem chamada a modelo; o blueprint dizia que "a intelligence não chama modelo" (citado em `auditoria-orquestracao-agentic.md:573`, decisão D6).
- Local-first (`product-discovery.md` §7): `mariliz.yaml`/`mariliz.lock`/`.mariliz/` versionados no projeto; histórico no diretório de dados do usuário; segredos "Cofre do sistema operacional … Nunca" no projeto (`:264`).
- Decisões pendentes já em 25/10 (`:356-361`): titularidade dos ativos Easymart/EmpregaNet; método do núcleo (TLC ou próprio); assinatura de código; licença do repositório; ícones faltantes na Synthra; tema inicial.

**Arquitetura atual comparada:** os quatro processos (Tauri/Rust, React/Vite, .NET 10, **Python/LangGraph**) e o canal motor ↔ Python por **HTTP em 127.0.0.1 com token** (`docs/blueprints/architecture-blueprint.md:33`) foram acrescentados depois (31/10 e 02/11). O princípio "regras só no .NET" foi preservado (Python só propõe; ADR-0001 e `MARILIZ_CURRENT_STATE.md:285`).

---

## B. Linha do tempo por fase

| # | Data | Commits / PRs | O que mudou | Por quê / fonte |
| --- | --- | --- | --- | --- |
| 0 | 25 a 26/10 | `a488ee9`, `36b8350`, `9d58afc` | Discovery, spike NDJSON, host Tauri, instalador Windows, UI seguindo o "Mariliz Web" | Briefing (fora do repo) |
| 1 | 27/10 | `67e369e`, `750264f`, `2fe998e` | **Jornada "Configurar projeto" com TLC e Harness Engineer**: discovery só leitura, TLC como **único** SDD instalado por comando aprovado, proposta do Harness Engineer validada pelo motor, plano de três vias com diff, apply idempotente e reversível, 14 Evals; "sempre a última versão do TLC" | Sem prompt registrado; `docs/phase-1b-configure-project.md` |
| 2 | 27 a 28/10 | `aa26305`, `7a5fbc8`, `5cdb8ef`, `f451929` | Renome para **"Mariliz Studio"**; UI moderna, recentes no host | Sem prompt registrado |
| 3 | 29 a 30/10 | `006062b`, `5afc6c6`, `926c36c`, `e20e9c3` | Refactor espelhando o **EmpregaNet**; **modelo único de implementação Mariliz** (TLC/SDD fora da UI e dos contratos, seletor de método removido); Enums/ e Interfaces/; sem co-autor | P 2026-10-29T01:14Z, 10-30T02:17Z [b801eaae]; P 10-30T02:57Z, 03:20Z, 03:22Z [237a6fdf] |
| 4 | 30/10 a 10/11 | `df2c63e`…`1c4b955`; PRs #10-#16; `e16d23a` (#39) | **Redesign Workbench** (direção A aprovada; tema escuro único; `#33C1BA`); fases 1 a 5 em 30/10, fase 6 tela a tela em 04 a 05/11, protótipo removido em 10/11 | P 10-30T22:10Z [1fd3a323] (brief), P 10-31T01:38Z "Aprovar A"; P 11-11T01:21Z "terminar a fase 6 do redesign" |
| 5 | 31/10 | `7bf44a4`, `d42027d`, `e54ee72`, `79f22d8`, `eaad23c`, `830b458`, `1cbd5b8`, `bdb6833` | **Separação desktop/runtime/intelligence** (Python stdlib), workflow local com **busca lexical por projeto**, avaliação de impacto, **chave do modelo fora do projeto**, CPython embed no instalador, jornada "Pedido de engenharia", **eval de recall lexical antes de embeddings** | **Sem prompt registrado** (não verificado quem pediu) |
| 6 | 02/11 | `283a75f`, `284c7e0`, `d5a8632`, `d8a661e` (#1); `83e6fb2` (#2) | **LangGraph + contratos v2 (ADR-0001)**; eventos ao vivo (D8); RAG híbrido com chunking estrutural | P 11-02T22:06Z [6b64e54a] (spec), 22:19Z "Aprovar e iniciar", 22:38Z "python-build-standalone", 23:10Z "Sim, fazer a D8" |
| 7 | 02 a 04/11 | #3, #4, #5/#8, #6/#7 | Auditoria de qualidade (ondas 1 a 4); componentes Synthra no lugar de HTML nativo | P 11-02T23:57Z; P 11-04T12:19Z |
| 8 | 04/11 |: | Repositório GitHub renomeado **duas vezes**; já se chama `spock-studio` em 10/11 | P 11-04T14:24Z, 19:40Z [188ad72f]; nome intermediário não verificado |
| 9 | 05/11 | `e1733a7` (#15) | **ui-kit**: `@synthra.io/ui-kit` → `@mariliz/ui-kit` 0.6.0 (publicado no npm; 0.6.1 depois); clone local apagado | P 11-05T01:13Z, 21:27Z, 21:31Z [9c868755] |
| 10 | 05/11 | #17-#27 | Fases 4 a 10 da evolução agentic: contratos de eventos, **proxy de modelo com orçamento (D4: só OpenAI, 8.000 tokens)**, Planning/Architecture com modelo, botão `useModel`, **juiz independente**, **governança de ações**, ciclo de vida do runtime, **distribuição sem assinatura Windows/Linux (macOS fora)**, workspace de execução, validação ponta a ponta | P 11-05T11:45Z…21:19Z [4b9c3dab]; "nesta primeira versão não pretendo disponibilizar o app para SO mac" (P 11-05T21:10Z) |
| 11 | 06 a 10/11 | #28-#38 | Auditoria arquitetural (ondas 1 a 6d): extrações SRP, switch expressions, intents em vez de ações (6c), `AckStatus` | P 11-06T12:13Z [f98d1179] e aprovações onda a onda; P 11-10T03:37Z (switch moderno) |
| 12 | 11/11 | `a161145` (#40), `b464b53`, `25860d2` (#42), `cab7dc4` (#43) | **Plataforma Engenharia + Gestão**: ambientes, Integration Hub (Jira/GitHub), portfólio e indicadores determinísticos, previsão Monte Carlo e riscos por regra, assistente gerencial verificado | P 11-11T01:43Z [0981e1ce] |
| 13 | 11/11 | `f659642`, `88fb81e`, `fa326b0` (#41) | **Renome para Spock Studio**, sigla, ícone "olhar lógico"; sem travessão | P 11-11T19:19Z, 20:00Z [0981e1ce] |
| 14 | 11 a 12/11 | #44-#48 | Fase 6 da plataforma: perfil tecnológico, conhecimento, estado do Harness, dispensa/reaplicação, revisões | "siga com os próximos passos" (P 11-11T20:31Z…22:31Z); ordem aprovada P 11-12T01:47Z "Sim, siga a ordem" |
| 15 | 12 a 13/11 | `8e27c7a` + docs; #49-#56 | **Análise de referência referência externa de produto** → roadmap **A a G**: B shell (#49), A chave no cofre e validação opt-in (#50/#51), C central de execuções (#52), D gestão que explica (#53), E catálogo do Harness/MCP/política (#54), F hub nível 2 com Anthropic, modelo por finalidade, cofre Unix (#55), G linha do tempo, auditoria, investigação LangGraph, laço agentic do RAG, decisões como memória, evals de gestão, assistente do portfólio (#56) | P 11-12T11:03Z [c50e36d6]; "Sim, A e B antes" (11:56Z); "Continuar a fase X" |
| 16 | 13/11 | #57-#73 | Auditoria da plataforma (ondas 7a a 7g), contrato v2 da investigação, remoção de `model.complete`, limpeza de comentários narrativos, **remoção dos docs do referência externa de produto** (#66) | P 11-13T18:02Z [581267b7]; "não quero que registre nada no projeto spock algo sobre o referência externa de produto" (P 11-13T20:24Z [f206ef78]) |
| 17 | 13 a 14/11 | #74 (H), #75 (V), #77 (U) | Shell (paleta de comandos, configurações, diagnóstico), **visual carvão com teal só como assinatura**, acessibilidade e linguagem para leigos | P 11-13T20:59Z, 22:15Z; 11-14T02:27Z, 02:47Z, 03:09Z, 04:01Z "Aprovo, começar U" |
| 18 | 14/11 | #78/#79 (W), #80 (I1/I2), #82 (P), #83 (C), #84 (S), #85 (A) | **Plano do ecossistema U→W→I2→P→C→S→A**: terminal pty e painéis; CLIs de IA detectadas e executadas **só leitura**; Gemini; **servidor MCP local**; revisão de contexto/tokens; varredura de segurança; "da ideia ao projeto" | P 11-14T03:09Z (visão "Ecosistema de automação empresarial"), "Seguir com I2/P/C/S/A" |
| 19 | 14/11 | #86-#89 | Consolidação (`.gitattributes`, `verify.py`, validação ao vivo), Harness a partir da arquitetura, busca no índice pelo MCP, teto de tokens por finalidade | P 11-14T21:12Z [693571a7] |
| 20 | 14 a 15/11 | #90-#100 | **Validação real ponta a ponta** (`MARILIZ-E2E-VALIDATION-2026`) e correções C1, A1, A2, B1 a B3, M1 a M4, A4, D2 | P 11-14T21:54Z [05736cd8]; P 11-15T14:19Z [0abef53f] |
| 21 | 15/11 | `9090791` (#94), #95, #96 | Remediação + **conectores cloud AWS/Azure/GCP só leitura** (AWS validada); screenshots removidas; CI Linux | P 11-15T03:00Z [05736cd8]; 13:23Z; 14:09Z |
| 22 | 15/11 | #97, #101-#105, #107, #108 | **IaC**: dependência Terraform gerenciada (1), **OpenTofu padrão** + IaC Runtime Abstraction, validate/plan (2), aprovação ligada ao plano e apply (3), state (4), geração por blueprint (5), drift e custo (6), perfis `aws login` (7) | P 11-15T13:32Z [49d60201], 14:14Z (OpenTofu), 14:36Z (desenho do dono) |
| 23 | 15 a 16/11 | `547f0f3` (#106), `c508d44` (#109 aberto) | **Auditoria de licenças e SBOM**; decisões **D-01..D-12**; Berkeley DB cumprido via avisos | P 11-15T15:45Z [1af233b7]; "Cumprir a Sleepycat" (22:21Z); P 11-15T22:30Z (implementação das decisões, identidade oficial SPOCK) |
| 24 | 16/11 |: | Esta auditoria estratégica: "A mudança de nome não representa uma mudança na essência do produto." | P 11-16T00:43Z [bcffbbb3] |

### B.1 Notas por tema da linha do tempo
- **TLC/Harness Engineer (27/10):** o TLC (`tlc-spec-driven`, Tech Leads Club, CC-BY-4.0) é instalado pelo canal oficial `npx -y @tech-leads-club/agent-skills install -s tlc-spec-driven -a claude-code` (`phase-1b-configure-project.md:65`). O Harness Engineer é um agente **original** ("nenhum Harness Engineer com origem e licença comprovadas foi autorizado para reuso", `:74`) que roda no ambiente de IA da pessoa e grava `.mariliz/proposal.json`; "o motor não dispara o agente nem chama modelos" (`:167`). Em 30/10 o TLC virou detalhe interno ("O Mariliz é o produto. A dependência técnica é apenas um detalhe de implementação", P 10-30T02:57Z) e hoje vive concentrado em `runtime/Mariliz.Application/Specification/SpecificationManifest.cs:5`.
- **Workbench (30/10):** a direção B "Sessão" (conversa-canvas) foi descartada porque "implica um interlocutor que não existe: o motor é determinístico e o Harness Engineer roda fora" (`docs/redesign/proposta-de-redesign.md:111`); a recomendação: "o Mariliz é uma ferramenta de configurar, aprovar e verificar" (`:118`).
- **Separação da inteligência + LangGraph (31/10 e 02/11):** o Python apareceu em 31/10 (`src/ai-runtime/graph.py`, `rag.py` no commit `7bf44a4`) como sequência fixa em stdlib; a ADR-0001 (`docs/architecture/adr/0001-langgraph-e-contratos-v2.md:45-69`) adotou LangGraph após o spike D1 (48 MB no disco, +8 MB no instalador, importação ~0,7 s, nenhuma conexão de rede).
- **RAG lexical antes de embeddings:** decidido no roadmap original (`implementation-roadmap.md:260`: "Começar por busca lexical + estrutura; vetorial só com eval comparativo"), materializado em `bdb6833` e na Fase 4 agentic (BM25 + símbolos + RRF k=60). D3 segue pendente.
- **Chave de modelo no cofre:** `79f22d8` (31/10) tirou a chave do projeto (arquivo `model.json` no diretório de dados); a fase A (`59b5051`, 12/11) migrou para o cofre do SO com migração que só apaga o arquivo depois de conferir; NET-E5 manteve o fallback em arquivo quando não há cofre (decisão P 11-13T20:19Z "NET-E5 e NET-M8"; `docs/auditoria-plataforma-2026-11.md:135`).
- **"Spock não executa comandos":** princípio implícito desde o discovery (o motor aplica arquivos gerenciados; o Harness Engineer roda fora). A memória do plano do ecossistema registra: "W e I2 mudam o princípio 'o Spock não executa comandos'". W deu à pessoa um terminal pty; I2 executa CLIs de IA **só leitura** (Claude Code `--permission-mode plan`, Codex `--sandbox read-only`, Cursor `--sandbox enabled`, Gemini sem `--yolo`; R 11-14T14:03Z). Depois, a IaC passou a **executar `tofu/terraform` plan/apply** com aprovação ligada ao plano e a **baixar e instalar binários** verificados por assinatura: a fronteira deslocou-se de "não executa" para "executa só com autorização explícita e verificação".
- **referência externa de produto A a G:** a análise de 12/11 virou seis docs; três foram apagados em 13/11 (`72f1463`) por ordem do dono; os demais (`SPOCK_PRODUCT_EVOLUTION.md`, `SPOCK_DESIGN_DIRECTION.md`, `SPOCK_IMPLEMENTATION_ROADMAP.md`, `MARILIZ_CURRENT_STATE.md`) permanecem sem citar o nome.

---

## C. Catálogo de itens (DECISÃO / PROPOSTA / EXPLORAÇÃO)

Legenda do "Depois": **Mantido**, **Mudou**, **Abandonado**, **Pendente**, **Parcial**, **Não verificado**.

### C.1 Orquestração multiagente e agentes especializados

| ID | Item | Cat. | Fonte | Depois |
| --- | --- | --- | --- | --- |
| H-01 | Python + LangGraph como capacidade especializada de IA; .NET continua autoridade; "O Python não deve duplicar regras de negócio do .NET"; "Não criar um segundo motor" | DECISÃO | P 11-02T22:06Z [6b64e54a] | Mantido (ADR-0001; `architecture-blueprint.md:41`) |
| H-02 | "Não comece criando dezenas de agentes"; primeiro um workflow funcional de 12 passos | DECISÃO | P 11-02T22:06Z | Mantido: um grafo de engenharia com 10 nós e um de investigação com 6 (`intelligence/workflows/*/graph.py`) |
| H-03 | Lista de agentes pedida: Request Analyzer, Context Builder, RAG Research, Planning, Architecture, Specification, Harness Engineer (lista truncada no log) | PROPOSTA | P 11-02T22:06Z | Parcial: Planning, Architecture e Judge usam modelo; Request Analyzer, Context Builder e RAG Research seguem nós determinísticos; Specification, Tools&MCP e Code Analysis "adiados" e nunca feitos (`auditoria-orquestracao-agentic.md:501-510`) |
| H-04 | Regra "cada agente existe por benefício demonstrável"; modelo só quando a eval mostrar ganho | DECISÃO (auditoria aprovada) | `auditoria-orquestracao-agentic.md:497` | Mantido; Request Analyzer por modelo nunca entrou por falta de eval com modelo real (`:708`) |
| H-05 | "Execução paralela quando fizer sentido" no LangGraph | PROPOSTA | P 11-02T22:06Z §4.1 | Não implementado: arestas lineares/condicionais (`graph.py:125-140`) |
| H-06 | Fase "6: Multiagentes" do plano agentic = proxy de modelo + Planning + Architecture | DECISÃO | `auditoria-orquestracao-agentic.md:603`; PRs #18/#19 | Mantido; "multiagente" ficou como pipeline sequencial com 3 agentes com modelo, não como agentes cooperando |
| H-07 | Juiz por modelo independente (outro prompt/critério, se possível outro modelo); veredito sempre determinístico; juiz só registra | DECISÃO | ADR-0001:76; `e74235d` (#21) | Mantido; nunca rodado com chave real |
| H-08 | Harness Engineer: agente original no ambiente de IA da pessoa, grava `.mariliz/proposal.json` | DECISÃO | `67e369e`; `phase-1b-configure-project.md:72-85` | Mantido fora do produto (`desktop/ui/src/features/configure-project/constants/judge-states.ts:13`); D5 (trazer para dentro) Pendente (ADR-0001:134) |
| H-09 | Agentes de profile do discovery (`dotnet-architect`, `dotnet-implementer`, `test-engineer`, `performance-optimizer`, `frontend-engineer`) destilados dos projetos de referência | PROPOSTA | `product-discovery.md` §3 (tabela "Profile .NET/Next.js") e Fase 2 do roadmap | Abandonado na prática: o Harness gerado tem só `harness-engineer`, `code-reviewer`, `verifier` (`runtime/Mariliz.Application/Assets/agents/`); dependia da titularidade (H-62) |
| H-10 | Agentes especializados de implementação, teste e pesquisa | Sem registro como decisão |: | Não existem como agentes do Spock; implementação fica com CLIs externas em modo leitura (I2) |
| H-11 | "Modelo mais capaz para planejamento, especializado para implementação" | **Não encontrado nas fontes** |: | O mais próximo: escolha de modelo por finalidade (`f595f61`) e o comentário "um mais forte para o juiz, um mais barato para classificar" (`runtime/Mariliz.Application/Models/ModelPurposes.cs:5`); só 3 finalidades: assistant, judge, workflow |
| H-12 | Workflow de investigação de gestão em LangGraph, com laço agentic do RAG | DECISÃO | Ordem aprovada item 6 (`SPOCK_IMPLEMENTATION_ROADMAP.md:762`); `6cf912e`, `ae2f758` | Mantido; "pronto quando" atendido só com modelo falso (`SPOCK_IMPLEMENTATION_ROADMAP.md` "Fase G: balanço") |
| H-13 | Agentes técnicos "com prioridades e objetivos" | DECISÃO | ordem aprovada item 5 (`SPOCK_IMPLEMENTATION_ROADMAP.md:761`) | Mudou: virou catálogo de fichas e política dos agentes (fase E), sem prioridades/objetivos configuráveis |

### C.2 Seleção de modelo e custo

| ID | Item | Cat. | Fonte | Depois |
| --- | --- | --- | --- | --- |
| H-14 | D4: só OpenAI, `gpt-4o-mini`, teto em **tokens** (8.000 e 60 s por execução), cobrado no proxy do motor | DECISÃO | R 11-05T16:09Z ("D4 answered: only OpenAI, token cap per execution"); ADR-0001:115-124 | Mudou: provedor como conexão e Anthropic (F), Gemini e chave por provedor (I1, `dcb9ffd`), teto por finalidade workflow 8k / assistant 30k (#89) |
| H-15 | Segundo provedor = Anthropic "por escolha de implementação: confirmar com o dono" | PROPOSTA | `SPOCK_IMPLEMENTATION_ROADMAP.md:806` | Pendente de confirmação explícita (não achei aprovação no log) |
| H-16 | O agente nunca escolhe modelo, provedor nem chave; pedido de modelo não carrega chave; proxy só em 127.0.0.1 | DECISÃO | ADR-0001:73-79 | Mantido |
| H-17 | Modelo opt-in por execução (`useModel` padrão falso) | DECISÃO | P 11-05T13:42Z "Botão useModel na UI"; ADR-0001:124 | Mantido |
| H-18 | Custo em dinheiro sempre 0 (`costUsd`), sem tabela de preços | DECISÃO | ADR-0001:122; `SPOCK_PRODUCT_EVOLUTION.md:332` | Parcial: a IaC estima custo pela lista pública da AWS (entrega 6); o uso de modelo continua sem custo |
| H-19 | Spock como "engenheiro que controla os gastos de tokens desnecessários, filtrando e modelando prompts" | DECISÃO (visão) | P 11-14T03:09Z [f206ef78] | Parcial: revisão de contexto antes de enviar (fase C, #83), tokenizer, teto por finalidade (#89); sem filtro de prompts das IDEs |

### C.3 Skills, metodologia e nomes de terceiros

| ID | Item | Cat. | Fonte | Depois |
| --- | --- | --- | --- | --- |
| H-20 | Método do núcleo: TLC (redistribuído/instalado pelo canal oficial) ou método próprio | PROPOSTA (pendente em 25/10) | `product-discovery.md:357`; recomendação "método próprio enxuto" (`implementation-roadmap.md:287`) | Resolvido em 2 tempos: TLC como único SDD (27/10, H-21) e depois escondido como detalhe (30/10, H-22) |
| H-21 | TLC como único SDD, instalado só com comando aprovado, "sempre a última versão" | DECISÃO | `67e369e`, `750264f` | Mantido internamente (`SpecificationManifest.cs:5,8,13`) |
| H-22 | Modelo único de implementação Mariliz; não oferecer TLC, Superpowers, Spec Power ou kits SDD; não criar catálogo/seletor de metodologias | DECISÃO | P 10-30T02:57Z [237a6fdf] | Mantido (`5afc6c6`; nenhuma ocorrência de "superpowers" no repo) |
| H-23 | Superpowers, Spec Power e "outros modelos de SDD" usados na experimentação | EXPLORAÇÃO | P 10-30T02:57Z ("Esses conceitos foram úteis durante a construção e experimentação") | Abandonado |
| H-24 | Remover nomenclatura de terceiros da UI ("Método SDD" → "Modelo de implementação"; "TLC" → "Modelo de engenharia Mariliz") | DECISÃO | P 10-30T02:57Z (tabela) | Mantido; após o renome virou "Modelo de implementação Spock" (`evolucao-engineering-management.md:463`) |
| H-25 | Skills geradas no projeto | DECISÃO (implícita) | `Assets/skills/harness-contract/SKILL.md` | Só uma skill (`harness-contract`) + a skill TLC instalada; sem autoria de skills (`MARILIZ_CURRENT_STATE.md:307`) |
| H-26 | Harness Management: configurar e gerenciar agentes, skills, rules, hooks, MCP servers, tools e instruções | PROPOSTA (spec do dono) | P 11-11T01:43Z [0981e1ce] §5.1.C | Parcial: estado, dispensa/reaplicação, revisões e catálogo só leitura; sem editor ("Editor livre de agente" fora, `SPOCK_PRODUCT_EVOLUTION.md:333`) |

### C.4 MCP, plugins, hooks, rules e evals

| ID | Item | Cat. | Fonte | Depois |
| --- | --- | --- | --- | --- |
| H-27 | Gerenciamento de MCP e catálogo curado (Fase 4 original) | PROPOSTA | `implementation-roadmap.md:114-116`; blueprint §7 | Parcial: inventário de MCP com alerta de segredo literal (`57d4513`); catálogo, instalação e grants inexistentes (`MARILIZ_CURRENT_STATE.md:97`) |
| H-28 | Servidor MCP local do Spock, "plugin" de contexto para Claude Code, Cursor, Codex e Gemini | DECISÃO | P 11-14T03:09Z; fase P (#82) | Mantido e ampliado: `search_project` (#88), escopo por projeto (#92), resumo completo (#99), ferramentas cloud e IaC só consulta |
| H-29 | Servidor MCP como P2 "depois da fase F; pede investigação própria de segurança" | PROPOSTA | `SPOCK_IMPLEMENTATION_ROADMAP.md:779` | Mudou: antecipado na ordem do ecossistema (14/11) |
| H-30 | MCP Adapter e "Spock Plugin" como conceitos de referência para os conectores cloud | PROPOSTA | P 11-15T03:00Z [05736cd8] §3.1 ("não uma determinação de entidades") | Mudou: ferramentas MCP cloud com `agentAccess` por vínculo; não há sistema de plugins |
| H-31 | Sistema de plugins/marketplace | Sem registro |: | Inexistente (nenhum conceito de plugin no código além de `CliIacRuntime.cs`, que trata plugin de provider do Terraform) |
| H-32 | Política imposta por código (motor e hooks), não por prosa | DECISÃO | `product-discovery.md` §5 ("O produto precisa impor política por código") | Mantido: hook `mariliz-policy.py` (PreToolUse), `catalog/policy.json`, política explicada em linguagem simples (`30fd0e0`) |
| H-33 | Evals Tier 0 no MVP; Evals com LLM fora | DECISÃO | `implementation-roadmap.md:14`; `product-discovery.md:308` | Mudou: 14 EVs estruturais mantidos (`EV-*` no código) **e** juiz por modelo (#21) e evals de gestão (`949fa76`), estes nunca rodados com modelo real |
| H-34 | Regras por stack | DECISÃO | Assets `rules/stack-dotnet.md`, `stack-javascript.md` | Parcial: descoberta reconhece Python, Rust, Go, Java (A4, `203552e`) mas só há regras para .NET e JS/TS (`MARILIZ_CURRENT_STATE.md:307`) |
| H-35 | Harness inicial a partir da arquitetura aprovada (regra `mariliz.rule.architecture`) | DECISÃO | P 11-14T21:48Z "Sim, começar"; #87 | Mantido |

### C.5 Ciclo de Harness Engineering

| ID | Item | Cat. | Fonte | Depois |
| --- | --- | --- | --- | --- |
| H-36 | Plano com diff → aprovação granular → apply idempotente → Evals → reversão; `mariliz.yaml` (pessoa) e `mariliz.lock` (gerado) | DECISÃO | `product-discovery.md:304-308`; `67e369e` | Mantido como invariante (`MARILIZ_CURRENT_STATE.md:286-287`) |
| H-37 | "Projeto do cliente é a fonte da verdade"; motor sem estado oculto | DECISÃO | `product-discovery.md:229` | Mantido |
| H-38 | Perfil tecnológico com histórico, conhecimento do projeto, estado do Harness, dispensa e revisões | DECISÃO | P 11-11T01:43Z §5.1.B; #44-#48 | Mantido |
| H-39 | Catálogo do Harness só leitura; mudança sempre pela jornada | DECISÃO | `SPOCK_PRODUCT_EVOLUTION.md` §11 (risco "Catálogo virar editor"); #54 | Mantido |
| H-40 | Configuração gerada só para Claude Code ("Claude Code primeiro, via adaptador") | DECISÃO | `product-discovery.md:244,306` | Mantido; Codex/Cursor/Gemini só recebem a configuração do MCP (P), não Harness próprio |
| H-41 | Segurança: varredura e correção pelo caminho do Harness (escopo `.gitignore`) | DECISÃO | P 11-14T03:09Z ("apontar falhas de segurança e corrigi-las"); #84 | Mantido |
| H-42 | "Validar cada etapa do desenvolvimento … sem alucinações ou slop de IA" | DECISÃO (visão) | P 11-14T03:09Z | Parcial: checagens determinísticas por etapa na fase A (#85), verificador de respostas da gestão; nada que escreve com modelo foi validado com chave real |

### C.6 Integrações (Jira/GitHub) e gestão

| ID | Item | Cat. | Fonte | Depois |
| --- | --- | --- | --- | --- |
| H-43 | Integration Hub com Jira e GitHub primeiro; Connection, Project Integration, Data Scope e Contextualization Policy separados; sync com checkpoints, idempotência e retry | DECISÃO | P 11-11T01:43Z; R 11-11T22:37Z; `evolucao-engineering-management.md:98-122` | Mantido |
| H-44 | Só leitura, só HTTPS, sem webhooks, sincronização sob demanda | DECISÃO | `evolucao-engineering-management.md:107-111` | Mantido; escrita em provedores P3 (`SPOCK_IMPLEMENTATION_ROADMAP.md:789`) |
| H-45 | Credenciais em cofre do SO; sem cofre, recusar (não gravar texto puro) | DECISÃO | `evolucao-engineering-management.md:100-101` | Mantido; cofre Linux/macOS implementado (`3248450`) mas nunca rodado em máquina real |
| H-46 | Azure DevOps e GitLab | PROPOSTA (original Fase 4) | `implementation-roadmap.md:114`; `evolucao…md:131` | Adiado; Pendente |
| H-47 | Ciclo completo do Jira (criar/atualizar/transicionar/sincronizar) | DECISÃO (validação pedida) | P 11-14T21:54Z §5.2 | Bloqueado: sem projeto de teste e token; OAuth 3LO pendente (`MARILIZ_CURRENT_STATE.md` §7) |
| H-48 | Sem dados fictícios; sem LLM para cálculos; métricas determinísticas; Monte Carlo com semente; riscos por regra | DECISÃO | R 11-11T22:37Z (regras da spec); `evolucao…md:458` | Mantido |
| H-49 | Papéis (Administrador, Gestor, Líder técnico…) | PROPOSTA | P 11-11T01:43Z (resumida) | Não feito por falta de identidade; Pendente (decisão 4 de `evolucao…md:493`) |
| H-50 | Sem indicador por pessoa | DECISÃO | `SPOCK_PRODUCT_EVOLUTION.md:334` ("Decisão já tomada") | Mantido; origem da decisão não verificada no log |
| H-51 | Fases A e B antes da ordem aprovada (C a G) | DECISÃO | P 11-12T11:56Z "Sim, A e B antes"; `SPOCK_IMPLEMENTATION_ROADMAP.md:764-767` | Mantido |

### C.7 RAG, Chroma e embeddings

| ID | Item | Cat. | Fonte | Depois |
| --- | --- | --- | --- | --- |
| H-52 | RAG só quando a busca simples não bastar; eval de recall desde o primeiro dia; lexical antes de vetorial | DECISÃO | `implementation-roadmap.md:130-132,260`; `bdb6833` | Mantido |
| H-53 | Armazenamento em SQLite FTS5 por projeto + `sqlite-vec` quando houver vetores | DECISÃO | `auditoria-orquestracao-agentic.md:380` | Mantido; `sqlite-vec` no lock, sem uso (`MARILIZ_CURRENT_STATE.md:114`) |
| H-54 | **Chroma embarcado**: "puxa onnxruntime, numpy e outras dependências pesadas; passa do orçamento"; Qdrant (binário separado) e LanceDB (pyarrow) também rejeitados | EXPLORAÇÃO (rejeitada) | `auditoria-orquestracao-agentic.md:381-383` | Abandonado. **Chroma via Docker não aparece em nenhuma fonte do repo**; não há Dockerfile (`docs/compliance/LICENSE_AUDIT.md:96`) |
| H-55 | D3 embeddings: (a) provedor via proxy com consentimento por projeto, (b) ONNX local, (c) adiar até a eval mostrar ganho; recomendação (c) e depois (a) | PROPOSTA pendente | `auditoria-orquestracao-agentic.md:572`; ADR-0001:132 | Pendente; o nó registra `EMBEDDINGS_NOT_READY` (`intelligence/workflows/engineering/nodes.py:83`); recall@5 = 0,33 (`MARILIZ_CURRENT_STATE.md:308`) |
| H-56 | Recuperação híbrida BM25 + símbolos + RRF k=60, chunking estrutural | DECISÃO | #2 (`83e6fb2`) | Mantido; correção M1 (`adb65d5`) |
| H-57 | Índice e checkpoint fora do repositório, um por projeto | DECISÃO | `7bf44a4`; R 11-04T02:09Z | Mantido |
| H-58 | Política de contextualização nasce fechada; dado sincronizado só entra no contexto com política | DECISÃO | `f5d566b`; `MARILIZ_CURRENT_STATE.md:148` | Mantido |
| H-59 | Decisões como memória do índice (`@decision/`) e conhecimento externo (`@external/`) | DECISÃO | `f86ecb6`; `runtime/Mariliz.Application/Integrations/ExternalKnowledge.cs:28` | Mantido |
| H-60 | "Memória aprendida" | EXPLORAÇÃO (rejeitada) | `SPOCK_PRODUCT_EVOLUTION.md:330` | Abandonado |

### C.8 Referências externas, EmpregaNet, Easymart, referência externa de produto

| ID | Item | Cat. | Fonte | Depois |
| --- | --- | --- | --- | --- |
| H-61 | Espelhar a arquitetura, pastas, tipagem, forms, hooks e services do EmpregaNet (frontend, depois backend) | DECISÃO | P 10-29T01:14Z, 10-30T02:17Z [b801eaae] | Mantido (memória `reference-empreganet-architecture`) |
| H-62 | Titularidade dos ativos Easymart/EmpregaNet antes de reutilizá-los | PROPOSTA pendente | `product-discovery.md:356` | Contornada: o produto escreveu agentes originais; nunca registrada como resolvida |
| H-63 | Nenhum arquivo dos projetos de referência copiado | DECISÃO | `product-discovery.md:168` | Mantido (não auditado arquivo a arquivo) |
| H-64 | Modelo Terraform da `infra/` de um projeto Easymart registrado em Markdown, anonimizado, "para evitar consultas ao repositório direto no github" | DECISÃO | R 11-15T16:00Z [49d60201] (citação do dono); `docs/plataforma/TERRAFORM_PROJECT_LAYOUT.md:7-11` | Mantido |
| H-65 | "Mariliz Web" como referência de organização do frontend | DECISÃO | `9d58afc`; `product-discovery.md` §3 | Mantido historicamente |
| H-66 | referência externa de produto como referência de produto/UX, sem copiar código, marca ou imagens | DECISÃO | P 11-12T11:03Z [c50e36d6] | Mantido |
| H-67 | "Não quero que registre nada no projeto spock algo sobre o referência externa de produto" | DECISÃO | P 11-13T20:24Z [f206ef78] | Parcial: docs removidos (`72f1463`), mas o nome segue no histórico público (commit `8e27c7a` e arquivos apagados) e em `docs/MARILIZ_REAL_WORLD_VALIDATION.md:222` (pasta "referência externa de produto" usada como projeto não autorizado no teste) |
| H-68 | Spock "superior" ao referência externa de produto, produto independente, linguagem visual do referência externa de produto com UX melhor | DECISÃO | P 11-13T22:15Z; 11-14T02:27Z | Mantido (fases H, V, U, W) |
| H-69 | Mascote "Kai" (robô) | EXPLORAÇÃO | P 11-13T16:29Z ("o robozinho"); documento de extração de design apagado em 13/11 ("Não levar o desenho") | Abandonado |

### C.9 Gestão de infraestrutura e cloud

| ID | Item | Cat. | Fonte | Depois |
| --- | --- | --- | --- | --- |
| H-70 | Remediar P0/P1 antes de implementar conectores cloud | DECISÃO | P 11-15T03:00Z [05736cd8] | Mantido (`REMEDIATION_REPORT.md`) |
| H-71 | Conectores AWS, Azure e GCP com estrutura comum e só leitura | DECISÃO | P 11-15T03:00Z; `9090791` | Parcial: AWS validada com conta real; Azure e GCP "implementados, não validados" (`CLOUD_CONNECTORS_STATUS.md:13-19`) |
| H-72 | Infrastructure Management com IaC; Terraform como dependência gerenciada, detectada, instalada só com autorização, verificada, sem mudar o PATH | DECISÃO | P 11-15T13:32Z [49d60201] | Mantido (entregas 1 a 6) |
| H-73 | OpenTofu padrão "se for gratuito"; Terraform opcional | DECISÃO | P 11-15T14:14Z; D-01 (`LICENSE_DECISIONS.md:8`) | Mantido |
| H-74 | Terraform "liberado, sem trava; o jurídico negocia à parte" | DECISÃO | `LICENSE_DECISIONS.md:60` (decisão do dono de 15/11) | **Em tensão** com D-02 "aguarda aprovação" e a recomendação de só instalar OpenTofu em builds comerciais (`LICENSE_DECISIONS.md:62-66`); memória cita "trava de Terraform gerenciado" no PR #109 (não verificado no código) |
| H-75 | Camadas: Infrastructure Management → IaC Runtime Abstraction → OpenTofu (padrão) / Terraform (opcional) → provedores | DECISÃO | P 11-15T14:36Z ("segue o desenho de como penso que o spock"); R 11-15T17:27Z | Mantido (`b4ddbf1`, `IIacRuntime`) |
| H-76 | IA nunca escreve HCL: blueprint como dado, checagens determinísticas, templates fixos | DECISÃO | R 11-15T17:27Z | Mantido |
| H-77 | Aprovação ligada ao plano (planId), apply com verificação, state sensível | DECISÃO | #102, #104 | Mantido |
| H-78 | Perfis do `aws login` | DECISÃO | #108 | Mantido |

### C.10 Posicionamento, comercial, distribuição

| ID | Item | Cat. | Fonte | Depois |
| --- | --- | --- | --- | --- |
| H-79 | Plataforma de Engenharia **e** Gestão: dois públicos, três pilares (Engineering, Management, Connected Intelligence) | DECISÃO | P 11-11T01:43Z | Mantido |
| H-80 | "O foco do Spock é ser um **Ecosistema de automação empresarial**" | DECISÃO (visão) | P 11-14T03:09Z [f206ef78]; reafirmado P 11-14T17:44Z [24a23bc2] | **Contradiz** `SPOCK_PRODUCT_EVOLUTION.md:21,322` ("O Spock não é plataforma de automação"; construtor de automações fora), escrito em 12/11 e não revisado |
| H-81 | Comercialização como **software proprietário** | DECISÃO | P 11-15T15:45Z [1af233b7] | Mantido (docs/compliance) |
| H-82 | Conta, planos e contratação (fase L), rotinas e notas (K), áreas "Fora" (M) | PROPOSTA | P 11-13T20:59Z ("parte de contratação de plano, automação…"); R 11-13T23:21Z | Pendente; "K: notas e rotinas" iniciado e interrompido (P 11-14T21:08Z [c34b280a]) |
| H-83 | Landing page / site comercial | **Sem registro** nas fontes |: | Não verificado |
| H-84 | Critério de continuidade: um usuário externo antes das Fases 3+ | DECISÃO original | `implementation-roadmap.md:213-219` | Não cumprido/abandonado em silêncio: não há registro de usuário externo, e o produto avançou muito além |
| H-85 | Primeira versão só Windows (e Linux se não exigir certificado); macOS fora | DECISÃO | P 11-05T21:10Z [4b9c3dab]; ADR-0001:112 | Mantido |
| H-86 | Instaladores sem assinatura por ora; assinatura quando houver usuários externos | DECISÃO | `c32a0d1` (#24); `auditoria-orquestracao-agentic.md:752` | Pendente (certificados) |
| H-87 | D-02..D-12 que aguardam o dono (Terraform, Linux, avisos, EULA, política de licenças, validador JSON Schema, marca) | PROPOSTA | `LICENSE_DECISIONS.md:9-19` | Pendente |
| H-88 | D-05 cumprir a Sleepycat (Berkeley DB) em vez de podar `_dbm` | DECISÃO | P 11-15T22:15Z ("pode remover o _dbm da poda"), 22:21Z "Cumprir a Sleepycat" | Mantido; parecer pendente (`LICENSE_DECISIONS.md:12`) |
| H-89 | D-08 CLIs de IA só invocando o binário do usuário | DECISÃO | P 11-15T22:30Z; `LICENSE_DECISIONS.md:15` | Mantido; termos Anthropic/Cursor pendentes |
| H-90 | Telemetria desligada; local-first; sem servidor do Spock; "Aplicativo web" fora | DECISÃO | `product-discovery.md:272`; `SPOCK_PRODUCT_EVOLUTION.md:331`; `MARILIZ_CURRENT_STATE.md:294` | Mantido |

### C.11 Identidade e nome

| ID | Item | Cat. | Fonte | Depois |
| --- | --- | --- | --- | --- |
| H-91 | Trocar "Mariliz" por "Spok Studio" [corrigido para Spock], ícone "com referencia ao Dr. Spok", sigla "System of Programming, Organization, Code and Knowledge" | DECISÃO | P 11-11T19:19Z [0981e1ce] | Mudou em 15/11 (H-93) |
| H-92 | Ícone "direção 1, olhar lógico" combinado com o wordmark (braço do "k" em teal = sobrancelha) | DECISÃO | P 11-11T20:00Z | Mantido no código (`fa326b0`); ícone e texto ainda descrevem a origem no personagem (H-93) |
| H-93 | "Não associe o nome do projeto ao personagem Dr. Spock, à franquia Star Trek…"; significado oficial único de SPOCK | DECISÃO | P 11-15T22:30Z [1af233b7] (linhas 4833-4837 do log) | **Pendente de alinhamento**: `evolucao-engineering-management.md:464-466` ("curiosidade cética associada ao personagem") e comentários de `spock-studio.svg`/`favicon.svg` ("que inspira o nome", `TRADEMARK_ASSESSMENT.md` §4); D-12 aguarda parecer |
| H-94 | Manter identificadores internos (`Mariliz.*`, `.mariliz/`, `mariliz.yaml/lock`, `MARILIZ_*`, `@mariliz/ui-kit`, `identifier` do Tauri, sidecar `mariliz-runtime`) | DECISÃO | `evolucao…md:470-473`; memória "decisão do usuário" | Mantido; migração com compatibilidade segue como decisão 6 (`evolucao…md:495`) |
| H-95 | Textos gravados nos projetos (`Assets/`) com o nome antigo | PROPOSTA pendente | `evolucao…md:474-476` (decisão 5) | Pendente: `Assets/rules/general.md:3` "Managed by Mariliz Studio", hook "Managed by Mariliz AI Engineering" |
| H-96 | "Spock Studio", nunca "Spok" | DECISÃO | R 11-12T15:44Z | Mantido; mas o dono passou a escrever **"SPOCK"** (P 11-15T03:00Z, 22:30Z, 11-16T00:43Z) enquanto o produto usa "Spock Studio" (`tauri.conf.json:3`; 170 ocorrências de "Spock Studio" contra 38 de "SPOCK") |
| H-97 | Sem travessão em código, interface, commits e PRs | DECISÃO | R 11-11T22:37Z; `f659642`; `TypographyTests` | Mantido; docs com travessão (decisão 7 pendente) |
| H-98 | Teal `#33C1BA` como identidade; tema escuro único | DECISÃO | P 10-30T22:10Z; `proposta-de-redesign.md:311` | Mudou na fase V: paleta carvão neutra, teal só como assinatura, `#16171bcc` avaliado (P 11-14T02:47Z) |

### C.12 Experiência, UI e princípios de execução

| ID | Item | Cat. | Fonte | Depois |
| --- | --- | --- | --- | --- |
| H-99 | Conceito "AI Engineering Workspace"; referências ChatGPT/Claude Desktop, Linear, Cursor sem copiar | DECISÃO | P 10-30T22:10Z [1fd3a323] | Mantido |
| H-100 | Direção A Workbench aprovada; direção B Sessão (conversa) descartada por prometer um interlocutor inexistente | DECISÃO / EXPLORAÇÃO | P 10-31T01:38Z "Aprovar A"; `proposta-de-redesign.md:111-118` | Mantido |
| H-101 | Usar componentes do Synthra/ui-kit em vez de tags HTML | DECISÃO | P 11-04T12:19Z | Mantido (#6, onda 7f) |
| H-102 | Radio fica na camada de atomic design do app, não na lib | DECISÃO | R 11-13T23:21Z (palavras do dono citadas) | Mantido (`c69051e`) |
| H-103 | Migrar a lib para `@mariliz/ui-kit`, publicar no npm e não atuar mais nela | DECISÃO | P 11-05T01:13Z, 21:27Z | Mantido; componentes novos ficam no app (decisão 9 do roadmap em aberto) |
| H-104 | Shell com terminal, painéis que abrem e fecham, minimizar/maximizar | DECISÃO | P 11-14T03:09Z; fase W (#78/#79) | Mantido |
| H-105 | Paleta de comandos como P2 "quando houver mais de cinco projetos" | PROPOSTA | `SPOCK_IMPLEMENTATION_ROADMAP.md:775` | Mudou: entregue na fase H (`ac189bb`) |
| H-106 | Falar o idioma de todos os públicos, de seniores a leigos | DECISÃO | P 11-14T03:09Z | Mantido (fase U: glossário `Term`, "Pedido de engenharia" no lugar de "Workflow") |
| H-107 | "A IA propõe; o motor decide; a pessoa aprova o plano exato"; agente só propõe intenção | DECISÃO | `SPOCK_PRODUCT_EVOLUTION.md:38`; 6c (`b5805a0`) | Mantido |
| H-108 | "O Spock não executa comandos" | DECISÃO implícita | memória `mariliz-ecossistema-plano` ("W e I2 mudam o princípio") | **Mudou**: W (terminal da pessoa), I2 (CLIs só leitura), IaC (apply e instalação com autorização) |
| H-109 | Revisar o contexto de um pedido antes de gastar tokens | DECISÃO | fase C (#83) | Mantido |
| H-110 | Da ideia ao projeto: especificação, arquitetura e backlog validados por etapa | DECISÃO | P 11-14T03:09Z ("arquitetar novos projetos a partir de uma ideia"); #85 | Mantido, não validado com modelo real |
| H-111 | Modernização de projetos (jornada "Modernizar sistema" e pedido de 14/11) | DECISÃO (visão) | `experience-blueprint.md:193`; P 11-14T03:09Z ("ajuda na modernizações de projetos") | **Não implementado** como jornada; não há discovery de legado nem mapeamento origem/destino |
| H-112 | Importar requisitos a partir de documentos | DECISÃO original (Fase 3) | `experience-blueprint.md:191` | Parcial: "da ideia ao projeto" gera especificação e backlog, mas não importa documentos |
| H-113 | Investigar `model.complete` antes de remover ("feature futura, ou uma slop de IA") | DECISÃO | R 11-13T23:21Z (palavras do dono) | Removido de ponta a ponta (#73) |
| H-114 | Validação real ponta a ponta com serviços reais e recursos temporários padronizados | DECISÃO | P 11-14T21:54Z | Feita (`MARILIZ_REAL_WORLD_VALIDATION.md`), com bloqueios de Jira e modelo |
| H-115 | Credenciais só no terminal do dono (`MARILIZ_LIVE_*`), nunca no chat | DECISÃO | R 11-13T01:21Z | Mantido |
| H-116 | Repositório público: nada privado da empresa em arquivos versionados | DECISÃO | R 11-15T16:00Z | Mantido (layout Terraform anonimizado); checagem rápida não achou ids reais |
| H-117 | Ordem do ecossistema U→W→I2→P→C→S→A, K/L/M depois, cada fase com aprovação | DECISÃO | P 11-14T04:01Z "Aprovo, começar U"; memória | Concluída até A (#85) |
| H-118 | Ordem anterior H → I1 → J1 → I2 → K → L → M | DECISÃO | P 11-13T22:15Z "eu aprovo a ordem"; R 11-14T02:33Z | Mudou para H-117; J1 (Conexões) sem commit com esse nome (não verificado se foi absorvida) |

---

## D. Renome Mariliz → Spock → SPOCK

### D.1 Cronologia
| Data | Evento | Fonte |
| --- | --- | --- |
| 2026-10-25 | "Mariliz AI Engineering" nos documentos de discovery | `a488ee9:docs/product-discovery.md:1` |
| 2026-10-27 | Produto passa a "Mariliz Studio" (UI, docs) | `aa26305` |
| 2026-10-30 a 11-13 | O dono continua chamando de "Mariliz AI Engineering" nos prompts | P 10-30T02:57Z, 11-02T22:06Z, 11-06T12:13Z, 11-11T01:43Z, 11-13T18:02Z |
| 2026-11-04 | Repositório renomeado duas vezes no GitHub; em 10/11 já é `Fabinschulz/spock-studio` | P 11-04T14:24Z, 19:40Z; R 11-10T15:56Z |
| 2026-11-11 | Pedido "trocar o nome Mariliz e os ícones" por "Spok Studio" com a sigla; ícone "olhar lógico" | P 11-11T19:19Z, 20:00Z; `88fb81e`, `fa326b0` (#41) |
| 2026-11-12 | Prompt do referência externa de produto mistura "Spock Studio", "Mariliz Studio" e "Mariliz" | P 11-12T11:03Z |
| 2026-11-14 | Prompt de validação: "MARILIZ: VALIDAÇÃO REAL" e "Projeto Spock … para avaliar a capacidade do Mariliz": o dono trata Mariliz como o motor/produto e Spock como o repositório | P 11-14T21:54Z |
| 2026-11-15 | Prompts passam a "SPOCK"; identidade oficial sem o personagem | P 11-15T03:00Z, 22:30Z |
| 2026-11-16 | "A mudança de nome não representa uma mudança na essência do produto." | P 11-16T00:43Z |

### D.2 Ocorrências remanescentes de "mariliz" (git grep -i, HEAD 06443a5)
- **4.162 ocorrências em 840 arquivos.**
- Por área: `runtime/Mariliz.Application` 1.049; `runtime/tests` 973; `docs/compliance` 713 (SBOM/inventário com nomes de pacotes); `contracts/fixtures` 237; `runtime/Mariliz.Infrastructure` 184; `desktop/ui` 148; `docs/plataforma` 108; `docs/product-discovery.md` 91; `docs/implementation-roadmap.md` 66; `docs/blueprints` 66; `docs/ui-modernization.md` 51; `runtime/Mariliz.Core` 47; `runtime/Mariliz.Runtime` 43; `docs/architecture` 43; `docs/MARILIZ_REAL_WORLD_VALIDATION.md` 42; demais < 40.
- Por tipo de identificador:
  - Namespaces/projetos `Mariliz.<X>` em `.cs/.csproj/.sln`: **1.985**.
  - Variáveis `MARILIZ_*`: **199** (as mais citadas: `MARILIZ_UPDATE_GOLDEN` 70, `MARILIZ_LIVE_*`, `MARILIZ_DATA_DIR`, `MARILIZ_PYTHON`, `MARILIZ_AI_TOKEN`).
  - Pasta `.mariliz/` nos projetos: **134**; `mariliz.yaml`/`mariliz.lock`: **218**.
  - Pacotes `@mariliz/...` (ui-kit, desktop-ui, desktop-host): **150**.
  - Sidecar `mariliz-runtime`/`mariliz-desktop-host`: **23**; `identifier` Tauri `dev.mariliz.ai-engineering`.
  - IDs de componentes do Harness `mariliz.agent.*`, `mariliz.rule.*`, `mariliz.hook.policy` (`Assets/catalog/components.json`).
- **Texto visível ao usuário:** a UI não mostra "Mariliz" como nome do produto; mostra nomes de arquivo (`mariliz.yaml`, `.mariliz/arquitetura`) em telas como Configurar, Catálogo e Ideia. `productName` e título da janela são "Spock Studio" (`desktop/host/src-tauri/tauri.conf.json:3,17`). Restos: `Cargo.toml:4` "Host desktop do Mariliz Studio".
- **Texto gravado nos projetos do cliente:** `Assets/rules/general.md:3` "Managed by Mariliz Studio"; `Assets/hooks/mariliz-policy.py:3` "Managed by Mariliz AI Engineering"; `harness-engineer.md`/`verifier.md` citam "Mariliz engine"/"Mariliz specification cycle". Mantido de propósito para não mudar o hash do Harness aplicado (`evolucao…md:474-476`).
- **Docs:** títulos ainda "Mariliz Studio: …" (`product-discovery.md:1`, `phase-1b-configure-project.md:1`, `proposta-de-redesign.md:1`); arquivos `MARILIZ_CURRENT_STATE.md` e `MARILIZ_REAL_WORLD_VALIDATION.md` (o dono pediu para regenerar o primeiro com o nome antigo, P 11-15T14:53Z).

### D.3 Perda de funcionalidade no renome
- **Nenhuma perda encontrada.** O renome (`88fb81e`) mexeu só no que é visível; identificadores e contratos ficaram, o que evitou quebrar projetos já configurados.
- Efeitos colaterais conhecidos e não verificados: trocar o `productName` muda a pasta de instalação e "quem tem a versão anterior pode ficar com as duas instaladas" (`evolucao…md:477-478`).
- **Defeito de documentação introduzido em 30/10** (não pelo renome, mas pela padronização de nomes): o commit `5afc6c6` inseriu a mesma nota "Nota (2026-10-30)…" **60 vezes** em `product-discovery.md`, **55** em `implementation-roadmap.md` e **35** em `ui-modernization.md` (uma por bloco), poluindo os documentos-fonte da visão original.

---

## E. Princípios e restrições recorrentes do dono

1. **Uma fase por vez, com aprovação explícita** ("Sim, onda N", "Continuar a fase X", "Seguir com P"); decisões antes de avançar (memória `mariliz-agentic-evolution-status`).
2. **Auditar antes de implementar; não recriar o que existe; não reescrever** ("Não estamos autorizando uma reescrita", P 11-02T22:06Z; "Não presuma que funcionalidades … já existem", P 11-11T01:43Z).
3. **Preservar a arquitetura** Tauri/Rust, React/Vite, .NET 10 como núcleo, Python/LangGraph só para orquestração; não migrar tecnologia (P 11-02, 11-11, 11-12).
4. **Clean code, SOLID/SRP, DRY, KISS, YAGNI, um tipo por arquivo, Enums/ e Interfaces/** (P 10-29, 10-30, 11-02T23:57Z, 11-14T01:13Z).
5. **Sem comentários que narram o código** ("comentários desnecessários que polui o código", P 10-29T01:14Z; limpeza geral P 11-13, memória `feedback-comentarios-enxutos`).
6. **Sem co-autoria nem atribuição a Claude** em commits e PRs (P 10-30T03:22Z "não quero que o claude apareça como contribuidor"; P 11-14T13:33Z "subir o commit sem coauthor").
7. **Sem dados fictícios, sem simular atividade de IA, sem alucinação ou "slop de IA"** (spec de 11-11: "no fictitious data, no LLM for calculations"; P 11-14T03:09Z; direção B do redesign descartada; "Nunca afirmar sucesso que não foi observado", `SPOCK_PRODUCT_EVOLUTION.md:39`).
8. **Validação real com serviços reais** e evidência; "Não declare conformidade sem testes e evidências" (P 11-14T21:54Z; P 11-15T22:30Z regra 9).
9. **Segurança de credenciais**: nunca no chat; cofre do SO; nada privado no repo público.
10. **Produto próprio**: nomes de metodologias e de referências (TLC, Superpowers, referência externa de produto) fora da UI e do repositório.
11. **Instalação, atualização e execução só com autorização explícita** (Terraform, P 11-15T13:32Z).
12. **Gratuito antes de licenciado** ("eu prefiro o gratuito", P 11-15T14:14Z) e preparação para venda proprietária.
13. **Não empilhar PRs; master limpa** (P 11-15T02:22Z; memória).
14. **Sem travessão** em código, interface, commits e PRs.
15. **Linguagem para todos os públicos** (P 11-14T03:09Z).

---

## F. Perguntas em aberto e o que não pude verificar

1. **Briefing original e auditoria de 2026-10-22** citados no discovery não estão no repositório nem no log de prompts.
2. **Quem pediu o trabalho de 31/10** (intelligence em Python, RAG lexical, chave de modelo, CPython): sem prompt; possivelmente Cursor (`591ebe8`, depois removido em `af01592`).
3. **"Modelo mais capaz para planejamento, especializado para implementação"**: não achei como decisão nem proposta nas fontes.
4. **Chroma Docker vs embarcado**: só a opção embarcada (e Qdrant/LanceDB) está documentada (`auditoria-orquestracao-agentic.md:381-383`); Docker não aparece.
5. **Landing page/posicionamento comercial**: sem registro além de "comercializado como software proprietário" e da fase L pendente.
6. **Visão "ecossistema de automação empresarial" (14/11) contra "O Spock não é plataforma de automação" (12/11)**: os documentos de produto não foram reconciliados.
7. **Identidade**: a orientação de 15/11 (sem Dr. Spock) contradiz o pedido de 11/11 e o ícone/textos atuais; o dono precisa decidir se o ícone "olhar lógico" e os comentários dos SVG mudam (D-12).
8. **Nome oficial**: "SPOCK" (prompts recentes) ou "Spock Studio" (produto)? Sem decisão registrada.
9. **Terraform**: "liberado sem trava" (dono) contra D-02 "aguarda aprovação" e trava preparada no PR #109; estado do código não conferido.
10. **Anthropic como segundo provedor** sem confirmação explícita do dono (`SPOCK_IMPLEMENTATION_ROADMAP.md:806`).
11. **Documentos citados pelo dono e inexistentes**: `MCP_QUALITY_ASSESSMENT.md`, `SPOCK_CURRENT_STATE.md`, `SPOCK_AI_ENGINEERING_CAPABILITIES.md`, `SPOCK_ARCHITECTURE_RECOMMENDATIONS.md`, `SPOCK_EVOLUTION_ROADMAP.md`, `ARCHITECTURE_REFERENCE_CATALOG.md` "Não existem neste repositório nem em outra pasta da máquina" (`REMEDIATION_REPORT.md:14`); talvez gerados como Artifact na sessão [24a23bc2] de 11-14T17:44Z (não verificado).
12. **ADR-0001 desatualizada**: a tabela final ainda marca D8 "Pendente" (`:137`), embora feita em `d8a661e`; D4 descreve "só OpenAI", hoje superada.
13. **Documento de validação real versionado**: a memória dizia "relatório sem commit (repo público, cita Jira da empresa)", mas `docs/MARILIZ_REAL_WORLD_VALIDATION.md` está no git desde `196e82b`; checagem rápida não achou ids de conta nem site real, mas não revisei o texto inteiro.
14. **PRs #58, #76 e #81** não aparecem como merges no log (fechados sem merge?); não consultei o GitHub.
15. **O repositório do produto não tem `CLAUDE.md` nem `.claude/`**: o Spock configura Harness para outros projetos, mas não aplica o próprio Harness a si mesmo (constatado por `git ls-files`; pode ser intencional).
16. **J1 (Conexões)** da ordem H→I1→J1… não tem commit com esse nome; não verifiquei se foi absorvida por F/I1.
17. **Critério de continuidade** (usuário externo antes das Fases 3+) nunca foi registrado como cumprido nem formalmente revogado.
