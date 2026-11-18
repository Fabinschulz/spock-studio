# Estado atual do Spock

> Parte do estudo [Plataforma de engenharia com IA](README.md). Diagnóstico de código em `docs/c2-homologacao-cenario-1`
> @ `cd0103c` (2026-11-17), só leitura, com testes locais sem rede. Os estudos anteriores
> ([engineering-intelligence-hub](../engineering-intelligence-hub/README.md),
> [product-audit](../product-audit/README.md)) são citados, não repetidos; onde divergem do código, a §9 aponta.

## 1. Resumo

O Spock é forte onde já foi fundo: **governança de escrita** (plano com diff, lock, aprovação, reversão), **IaC**
(geração, validação, plano, drift, com aprovação), **conectores de leitura** (Jira, GitHub, AWS) e **escopo e redação do
servidor MCP**. É fraco exatamente no que este estudo pede: **não analisa a arquitetura de um projeto existente, não
tem base de referências técnicas, o Harness só distingue famílias de stack, o RAG entrega ao agente fragmentos de ~160
caracteres e o Spock não consome MCPs de terceiros**. Nada com modelo rodou com chave real (H0-7 do roadmap).

| Medida | Valor |
| --- | --- |
| Código .NET de produção | 6 projetos, ~34,7 mil linhas; 97% na Application |
| Python (`intelligence/`) | 4 agentes com modelo, 2 workflows LangGraph |
| UI (`desktop/ui`) | 19 features, 455 arquivos, React 19.3 + `@mariliz/ui-kit` 0.6.0 |
| Testes .NET | 1.626 casos; 1.598 aprovados e 28 ignorados na 2ª rodada; 3 falhas intermitentes na 1ª |
| Testes do MCP | 100 de 100 |
| Testes Python | 197 de 197 |
| Testes da UI | 833 de 850; as 17 falhas são timeouts que mudam a cada rodada |
| Recall real do RAG no próprio repositório | recall@5 = 0,25, MRR = 0,111 (meta documentada: 0,6) |

## 2. Estrutura

```text
runtime/        .NET 10: Core ← Application ← Infrastructure (+ .Cloud) ← Runtime (sidecar, mcp) e Cli
intelligence/   Python 3.12 + LangGraph 1.2.19: agentes, workflows, índice de conhecimento, evals
desktop/        host Tauri (Rust), ui (React), launcher `spock` (Rust)
contracts/      JSON Schemas versionados no nome + fixtures válidas e inválidas
```

| Projeto | Linhas | Observação |
| --- | --- | --- |
| `Mariliz.Core` | 726 | Políticas (`ManagedScope`, `PathGuard`, `SecretScanner`, `StableProjectId`) e modelos; anêmico |
| `Mariliz.Application` | 27.178 | Todo o domínio: Harness, execução, workflows, IaC, gestão, contexto, MCP, conectores |
| `Mariliz.Infrastructure` | 4.636 | Cofre (DPAPI/Keychain/Secret Service), tokenizador, processo Python |
| `Mariliz.Infrastructure.Cloud` | 1.275 | AWS, Azure, GCP |
| `Mariliz.Runtime` / `Mariliz.Cli` | 383 / 511 | Pontos de entrada |

**Dependências.** A direção Core ← Application ← Infrastructure é respeitada nas referências de projeto. Desvios:

- O SDK do MCP e o stdio moram na Application (`Mcp/McpServer.cs:38-46`), contra o espírito da ADR-0009.
- I/O direto residual na Application (`InvestigationMemory.cs:33`, `WorkflowContextBuilder.cs:92`,
  `WorkspaceAuthorizer.cs:106`), contra a regra de [backend-architecture.md](../architecture/backend-architecture.md).
- Sem contêiner de DI: composição manual em quatro lugares (`Runtime/Program.cs`, `McpEntry.cs`, `ContextComposition.cs`,
  `EngineComposition.cs`); o MCP recria hubs a cada chamada (`McpEntry.cs:66-67`).

**Responsabilidades duplicadas.**

| Duplicação | Lugares | Consequência |
| --- | --- | --- |
| Ids de stack | `HarnessComposition`, `ArchitectureDocuments.Profiles`, `TechnologyCatalog`, detectores | Uma stack nova exige 4 edições; pré-requisito para catálogo de referência |
| Catálogo de componentes | `Harness/Catalog.cs`, `Engineering/HarnessCatalog.cs`, `Assets/catalog/components.json` | Versão divergente já no comentário (`0.1.1` × `0.1.0`) |
| Schema do argumento `project` no MCP | 4 declarações com descrições diferentes | Contrato inconsistente entre ferramentas |
| Comando "context" | `Mariliz.Cli` e `Mariliz.Runtime` | Dois pontos de entrada para o mesmo conceito |
| Validação de contratos | .NET (JsonSchema.Net), UI (Zod), Python (à mão) | Três validadores escritos à mão; sem geração de código |

**Arquivos grandes.** Os geradores de IaC (`IacAwsGenerator.cs` 829, `IacGoogleGenerator.cs` 751, `IacAzureGenerator.cs`
723 linhas) e `IacOperationService.cs` (605) concentram 4 dos 6 maiores arquivos. O resto é bem fatiado.

## 3. Capacidades de engenharia

| Capacidade | Estado | Evidência | Lacuna principal |
| --- | --- | --- | --- |
| Registrar projetos | Implementado | `WorkspaceAuthorizer`, `FileWorkspaceRegistry`, `StableProjectId` (`spk-<ULID>`, F1) | — |
| Descobrir tecnologias | Implementado, com lacuna | 10 stacks; ~35 frameworks em `Discovery/Frameworks.cs` | **Nenhum framework .NET** além de ASP.NET Core e testes (EF Core, MediatR, Blazor, MAUI não são vistos) |
| Analisar arquitetura | **Ausente** | `ArchitectureDocuments.cs` só lê a arquitetura *declarada* em `.spock/arquitetura/*.md` | Nada analisa camadas, módulos, direção de dependências ou padrões |
| Consultar referências técnicas | **Ausente** | 3 regras estáticas (`Assets/rules/general.md`, `stack-dotnet.md`, `stack-javascript.md`, 31-52 linhas) | Sem catálogo; o [ARCHITECTURE_REFERENCE_CATALOG.md](ARCHITECTURE_REFERENCE_CATALOG.md) é o primeiro passo |
| Recomendar padrões | Parcial | Regras por família; proposta do Harness Engineer limitada ao catálogo de componentes; `IacBlueprintChecks`; `SecurityFixPlanner` | Sem base de referência para justificar |
| Criar planejamentos técnicos | Parcial | Plano do Harness e plano IaC sólidos; workflow `engineering.request`; jornada "Da ideia ao projeto" (`IdeaArchitectService.cs`) | Prompts da jornada pedem stack ao modelo sem referência ("prefira a solução mais simples"); nunca rodou com modelo real |
| Preparar Harnesses | Implementado só para Claude Code | `HarnessComposition.cs`, `AiEnvironments.Supported = [ClaudeCode]` | Cursor/Codex/`AGENTS.md` estão no escopo gravável, mas nenhum componente os gera (C3); React e Angular recebem a mesma regra |
| Gerenciar agentes | Parcial | 3 agentes no catálogo (`harness-engineer`, `code-reviewer`, `verifier`); CLIs externas em leitura (`AiToolCatalog.cs`); política deny/ask/allow | Sem agente por stack nem por papel de engenharia |
| Executar workflows | Parcial | `WorkflowService`, aprovação, retomada de `awaiting_approval` | Execução em andamento não sobrevive a queda (`ENGINE_RESTARTED`) |
| Consumir ferramentas MCP | **Ausente** | Nenhum cliente MCP em `runtime/` nem `intelligence/`; `McpInventoryService` só lê `.mcp.json` | O Spock é servidor, não cliente |
| Validar resultados | Implementado para Harness e IaC; ausente para entrega | 14 checks (`Validation/Checks/`), `JudgmentVerifier`, verificação pós-apply | [DELIVERY_VERIFICATION.md](../engineering-intelligence-hub/DELIVERY_VERIFICATION.md) sem código |
| Registrar evidências | Implementado com falhas | `audit.ndjson`, diários, linha do tempo, `@decision/` | 7 ferramentas MCP sem auditoria; arquivo sem rotação; leitura carrega tudo |
| Apoiar modernização | **Ausente** | — | Nada de upgrade, fim de suporte (EOL) ou migração |
| Orientar criação de projetos | Parcial | `IdeaArchitectService.cs` (especificação EARS, arquitetura, backlog, `IdeaChecks`), bootstrap IaC | Sem base de referência; decisão de stack fica com o modelo |

## 4. Camada de IA (Python)

Detalhe em [SPOCK_AI_ENGINEERING_CAPABILITIES.md](SPOCK_AI_ENGINEERING_CAPABILITIES.md). Pontos de estado:

- **Agentes:** `planning`, `architecture` (só 3 áreas e 2 riscos possíveis, `contracts/engineering.py:10-11`), `judge`,
  `investigator`. Nenhum chama ferramenta; prompts são constantes no código; só dois têm `AGENT_VERSION`.
- **Workflows:** `engineering.request` (linear, sem laço de replanejamento) e `management.investigation` (laço com teto).
- **RAG:** lexical (FTS5 BM25 + símbolos, RRF). **Achado novo:** o agente recebe ~100-160 caracteres por trecho porque o
  índice corta o `excerpt` em 160 e usa `snippet` de 12 tokens (`knowledge/index.py:103,221`); o `MAX_EXCERPT = 1200`
  nunca é alcançado. Corrige o AI-09 da [auditoria de IA](../product-audit/AI_ENGINEERING_AUDIT.md), que leu o limite
  declarado.
- **Sem filtro por metadado** (linguagem, caminho, tags) e frontmatter não é lido; o índice é estritamente por projeto.
- **Contrato morto:** `ai.agent.result.v1` não é usado por nenhum código.
- **Finalidades sem uso:** `classify` e `rewrite-query` aceitas pelo proxy, sem agente (AI-22 continua aberto).

## 5. Servidor MCP do Spock

Montado sobre o SDK oficial `ModelContextProtocol.Core` 2.4.0, só stdio, com escopo obrigatório (`--project` ou
`--project-dir`; sem argumento sai com código 2). Expõe 12 ferramentas fixas e até 24 de cloud quando o projeto libera.

| Ferramenta | Saída | Auditoria | Observação |
| --- | --- | --- | --- |
| `spock_context_bootstrap` | texto + `structuredContent` + `outputSchema` | sim | Única tipada; contrato em `contracts/schemas/mcp/` |
| `list_projects` | texto | **não** | Nome genérico |
| `project_briefing` | texto (corte em caracteres) | sim | |
| `decisions` | texto | só com resultado | Nome genérico |
| `search_project` | texto | sim | Reconfere cada trecho contra as regras atuais; omite `@external/`; **descarta prefixo virtual novo** |
| `cloud_connections` | texto | **não** | |
| `aws_*` (12), `azure_*` (7), `gcp_*` (5) | tabela Markdown | `cloud.read` | `limit` ≤ 100, "havia mais" sem cursor |
| `iac_status`, `iac_plan_summary`, `iac_history`, `iac_drift_summary` | texto | **não** | `iac_history` fixo em 20 |
| `iac_validate` | texto | grava operação | **Roda `init` e grava histórico, mas anuncia `readOnlyHint`** (MCP-3 aberto) |
| `iac_state` | texto | sim | |
| `iac_blueprint_check` | texto | **não** | Schema `object` sem propriedades |

Avaliação pelos mesmos critérios da [avaliação dos MCPs externos](MCP_QUALITY_ASSESSMENT.md):

| Critério | Resultado |
| --- | --- |
| Conectividade, autenticação, leitura | Aprovado (100 testes pelo fio, incluindo negociação, BOM, escopo e redação) |
| Contratos | Requer melhorias: 1 de 12 ferramentas tipada; sem códigos de erro; sem cursor; briefing em caracteres e bootstrap em tokens |
| Organização | Requer melhorias: só uma ferramenta com prefixo `spock_`; `decisions`, `list_projects`, `search_project` colidem com outros servidores |
| Segurança | Adequada: escopo obrigatório, fora do escopo = inexistente, redação de segredo no texto e no JSON, cloud reconferida a cada chamada. Ressalva: `readOnlyHint` falso em `iac_validate`; cliente autodeclarado |
| Tratamento de erros | Requer melhorias: `isError` + texto, sem `code` estável |
| Usabilidade por agentes | Requer melhorias: descrições boas ("comece por..."), mas o modelo precisa extrair números de prosa em português; a tela de Agentes anuncia 5 ferramentas quando o servidor expõe 12+ (`IdeConnection.cs:22-24`) |
| Observabilidade | Requer melhorias: só auditoria; sem latência, tamanho ou erros por ferramenta |

## 6. Conectores

| Conector | Leitura | Escrita | Paginação | Lacuna |
| --- | --- | --- | --- | --- |
| Jira Cloud | Issues com status, tipo, responsável, labels, datas, pai, links, changelog | Não | `nextPageToken`, 100/página | Sem `description` nem comentários; só token de API (sem OAuth); Server/DC fora |
| GitHub | Issues e PRs | Não | `page` + `Link` | Sem commits, reviews, checks, diff |
| Git local | `git status` | Não | — | Só status |
| AWS | 12 leituras | Não (a IaC aplica com aprovação) | `NextToken` | Validado em conta real |
| Azure / GCP | 7 / 5 leituras | Não | — | **Não validados** em conta real |
| Transporte | Só GET por contrato; HTTPS; 4 tentativas; `retry-after` e `x-ratelimit-reset` | — | — | — |

O transporte dos conectores do Spock é melhor desenhado que o de vários MCPs externos avaliados: nunca escreve,
classifica limite de taxa e indisponibilidade, e guarda credenciais no cofre do sistema.

## 7. Contratos, testes, CI e observabilidade

- **Contratos:** `contracts/schemas/` com `intelligence/` (12), `ai/` (16), `protocol/` (envelope + 6 métodos), `mcp/` (1).
  A UI usa ~100 métodos do motor, com fixtures, mas só 6 têm JSON Schema. `ai.knowledge.search.v1` e
  `ai.context.preview.v1` sem schema.
- **Testes:** além dos números da §1, 6 arquivos de teste *live* que se pulam sem `MARILIZ_LIVE_*`. As 3 falhas .NET e as
  17 da UI são intermitentes (tempo); ninguém as observa continuamente porque o GitHub Actions está sem orçamento.
- **CI:** `.github/workflows/ci.yml` cobre .NET, Python, UI, licenças e instaladores; o build não fixa `SpockEdition`
  (H0-6). Sinal pouco confiável enquanto o orçamento estiver esgotado.
- **Observabilidade:** nenhum `ILogger`, `ActivitySource`, `Meter` ou OpenTelemetry em `runtime/`; Python só `stderr`.
  Há trilha de auditoria, diários, eventos `ai.event.v1` e uso de tokens por execução. Custo em dinheiro sempre 0.

## 8. Riscos para o objetivo deste estudo

| Risco | Por que importa |
| --- | --- |
| Harness genérico por família de stack | O pedido exige Harness específico para React, Angular, .NET; hoje React e Angular recebem o mesmo texto |
| RAG com fragmentos curtos e sem filtro | Mesmo com o catálogo indexado, o agente veria 160 caracteres de uma seção escolhida por coincidência de palavras |
| Nenhuma análise de arquitetura existente | A modernização sem reescrita depende de saber o que existe; hoje o Spock só sabe o que foi declarado |
| Nada validado com modelo real | Planos, arquitetura e juiz nunca foram medidos com saída real |
| Ids de stack em 4 lugares | Cada nova tecnologia no catálogo vira 4 edições e risco de deriva |
| Testes intermitentes sem CI | Regressões passam despercebidas |

## 9. Documentos desatualizados encontrados

| Documento | Afirmação | Hoje |
| --- | --- | --- |
| [MCP_CAPABILITY_CATALOG.md](../engineering-intelligence-hub/MCP_CAPABILITY_CATALOG.md) §1 | JSON-RPC próprio; sem `structuredContent`; MCP-1 e MCP-2 abertos | SDK oficial 2.4.0; bootstrap estruturado; MCP-1 e MCP-2 corrigidos (#120) |
| [CURRENT_STATE_REASSESSMENT.md](../engineering-intelligence-hub/CURRENT_STATE_REASSESSMENT.md) §1 | P0-1, P1-6, P2-10 abertos; só a ADR 0001 | H0-1..H0-5 feitos; ADRs 0001-0009 |
| [backend-architecture.md](../architecture/backend-architecture.md) | 8 pastas na Application; 4 projetos | 24 pastas; `Infrastructure.Cloud` ausente do documento |
| [AI_ENGINEERING_AUDIT.md](../product-audit/AI_ENGINEERING_AUDIT.md) AI-09 | 1.200 caracteres por trecho | Efetivo é 160 |
| [AI_ENGINEERING_AUDIT.md](../product-audit/AI_ENGINEERING_AUDIT.md) AI-18 | Python corta em 20k/40k tokens | Corrigido (64k/120k) |
| `Engineering/McpInventory.cs:27` (comentário) | "o Spock Studio não grava .mcp.json" | Grava a chave `spock` desde o C2 |
| `intelligence/server.py:65` (comentário) | Checkpoint "consistente a cada passo" | `durability="exit"` (AI-07) |
