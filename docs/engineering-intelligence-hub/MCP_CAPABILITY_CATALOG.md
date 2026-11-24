# Catálogo de capacidades MCP do SPOCK

> Parte do estudo [Engineering Intelligence Hub](README.md) (2026-11-16). Inventário lido em `master` `bea3302`.
> "Existente" é o que o código faz hoje; "recomendado" é proposta. Nada foi implementado.
>
> **Atualização de 2026-11-18** (`master` `0e00ecc`): a §1 descreve o servidor de `bea3302`. Desde então mudou:
>
> | Aspecto ou problema | Hoje |
> | --- | --- |
> | Implementação e protocolo | SDK oficial `ModelContextProtocol.Core` 2.4.0, handlers de baixo nível, stdio ([ADR-0009](../architecture/adr/0009-sdk-oficial-do-mcp.md)) |
> | Saída | `spock_context_bootstrap` com `structuredContent` e `outputSchema` (contrato em `contracts/schemas/mcp/`); as outras ferramentas seguem só em texto |
> | Instruções do servidor | "Numa sessão nova, comece por `spock_context_bootstrap`" (`McpServer.cs`) |
> | MCP-1 escopo | **Corrigido** (#120): sem `--project` ou `--project-dir` o servidor não sobe (`McpOptions.cs`) |
> | MCP-2 composição | **Corrigido** (#120): `EngineComposition` comum ao sidecar e ao MCP |
> | MCP-3 `iac_validate` | Aberto |
> | MCP-4 cliente autodeclarado | Aberto |
> | MCP-5 auditoria incompleta | Aberto (7 ferramentas sem registro) |
> | MCP-6 só texto | Parcial: só o bootstrap é estruturado |
> | MCP-7 configuração da IDE | **Corrigido**: o Harness grava a chave `spock` no `.mcp.json` (C2, #130) e projeta Cursor e Codex (C3, #139-#142) |
>
> Avaliação completa e atual em [../ai-engineering-platform/MCP_QUALITY_ASSESSMENT.md](../ai-engineering-platform/MCP_QUALITY_ASSESSMENT.md).

## 1. O servidor que existe

| Aspecto | Hoje | Evidência |
| --- | --- | --- |
| Entrada | Mesmo executável do motor com o argumento `mcp`, iniciado pela IDE | `Mariliz.Runtime/Program.cs:29`, `McpEntry.cs:42-76` |
| Implementação | JSON-RPC 2.0 próprio, uma mensagem por linha, sem SDK | `Mcp/McpServer.cs:9-37` |
| Protocolo | `2025-06-18` (aceita `2025-03-26` e `2024-11-05`) | `McpServer.cs:12-13` |
| Métodos | `initialize`, `ping`, `tools/list`, `tools/call`; o resto devolve -32601 | `McpServer.cs:60-67` |
| Resources, prompts | Não existem; `capabilities` só com `tools.listChanged=false` | `McpServer.cs:77` |
| Transporte | Só stdio | `McpEntry.cs` |
| Saída | Só texto (`content[].type = "text"`), redigido por `SecretScanner`; sem `structuredContent` nem `outputSchema` | `McpServer.cs:116-120` |
| Anotações | Todas as ferramentas com `readOnlyHint: true`; `openWorldHint` nas de nuvem e IaC | `McpServer.cs:89` |
| Instruções do servidor | "Comece por `project_briefing` e use `search_project`…" | `McpServer.cs:20-24` |
| Escopo | `--project p-…` (até 20); **sem argumento, todos os projetos autorizados** | `McpOptions.cs:27`, `McpProjectResolver.cs:8-9` |
| Projeto da chamada | Argumento `project`, ou a raiz que contém a pasta atual (prefixo mais longo), ou o único do escopo | `McpProjectResolver.cs:15-34` |
| Auditoria | `ide.context_read` em `audit.ndjson` com ferramenta, caminhos e cliente declarado, sem conteúdo | `ProjectContextTools.cs:34-36,241-255` |
| Lista de ferramentas | Fixa na partida; liberar uma nuvem depois exige reiniciar a IDE | `McpEntry.cs:76`, `CloudMcpTools.cs:8-10` |
| Limites | Em caracteres (`McpText.Fit`, até 20.000), não em tokens | `McpText.cs:8-28` |

### 1.1 Ferramentas existentes

| Ferramenta | Domínio | Parâmetros | Fonte dos dados | Efeito | Auditada |
| --- | --- | --- | --- | --- | --- |
| `list_projects` | Contexto | — | Registro de projetos autorizados | Nenhum | **Não** |
| `project_briefing` | Contexto + gestão | `project?`, `max_chars` (1.000–20.000) | Perfil, estado do Harness, comandos, integrações e última sincronização, fluxo, riscos (5), marcos, decisões (5) | Nenhum | Sim |
| `decisions` | Conhecimento | `project?`, `query?`, `limit` | `DecisionMemory` | Nenhum | Só com resultado |
| `search_project` | Conhecimento | `query`, `project?`, `limit` (1–10) | Índice FTS5 pelo Python; marca arquivo mudado depois da indexação | Nenhum | Sim, com caminhos |
| `cloud_connections` | Infra | `project?` | Vínculos cloud com `agentAccess` | Nenhum | — |
| `aws_*`, `azure_*`, `gcp_*` | Infra | `project?`, `connection?`, `limit`, `region?` | Leitura do provedor | Chamada externa de leitura | Sim (`cloud.read`) |
| `iac_status` | Infra | `project?` | Ferramenta e versão | Nenhum | — |
| `iac_validate` | Infra | `project?` | `fmt` e `validate` numa cópia | **Grava uma operação no histórico** apesar de `readOnlyHint` | Sim (`iac.validate`) |
| `iac_plan_summary`, `iac_history`, `iac_drift_summary`, `iac_state` | Infra | `project?`, `operation?` | Histórico, drift e state gravados | Nenhum | — |
| `iac_blueprint_check` | Infra | `blueprint` | Validação em memória | Nenhum | — |
| `iac_architecture_prepare` | Infra | `project?`, `provider?` | Regras do desenho e a proposta gravada em `.spock/iac-proposal.json` | Nenhum (quem grava a proposta é o agente) | — |

### 1.2 Problemas encontrados no servidor atual

| Id | Problema | Gravidade | Origem |
| --- | --- | --- | --- |
| MCP-1 | Sem `--project`, expõe todos os projetos autorizados (R-11, ainda aberto) | Alta | `McpProjectResolver.cs:8-9` |
| MCP-2 | A composição do `McpEntry` diverge da do `Program.cs`: `IacRuntimeService` sem a política de edição (`iacPolicy`), `ManagementService` sem histórico de riscos, `IacStateService` sem nuvem | Alta para a política de edição (D-02); média para o resto | `McpEntry.cs:46-73` contra `Program.cs:50,76-79` (P2-10 da auditoria, agora com efeito) |
| MCP-3 | `iac_validate` anuncia `readOnlyHint` e grava histórico | Baixa | `IacMcpTools.cs:198-214` |
| MCP-4 | Identidade do cliente declarada, não verificada | Baixa no modelo local | `McpServer.cs:124-131` |
| MCP-5 | `list_projects` e várias ferramentas de IaC sem auditoria | Baixa | Inventário |
| MCP-6 | Resposta só em texto: a IDE não recebe números estruturados, data de sincronização como campo nem proveniência tipada | Média para gestão na IDE | `McpServer.cs:116-120` |
| MCP-7 | Configuração da IDE só por comando de usuário (`claude mcp add --scope user`); nenhum `.mcp.json` do projeto | Média (C-09) | `ide-setup.ts:27-55` |

## 2. Princípios do catálogo recomendado

1. **Agrupar por domínio e intenção**, não por tabela nem por endpoint. Nem uma ferramenta genérica "consultar", nem
   centenas de micro-ferramentas: cada ferramenta responde a uma pergunta que a pessoa faria.
2. **Projeto obrigatório e resolvido no motor.** Sem vínculo válido, toda ferramenta devolve o erro
   `PROJECT_NOT_BOUND` com o que fazer; nunca uma lista de todos ([ADR-0007](../architecture/adr/0007-politicas-de-acesso.md)).
3. **Saída dupla.** `structuredContent` com `outputSchema` (números, datas, ids nativos, proveniência) e texto curto
   legível. O texto é derivado do estruturado, nunca o contrário.
4. **Proveniência em todo item.** `source` (`repo`, `jira`, `github`, `azure-devops`, `decision`, `spock-calc`,
   `inference`), `nativeId`, `observedAt` ou `syncedAt`, `freshness` (`fresh`, `stale`, `unknown`), `classification`
   (descoberto, declarado, aprovado, integração, inferido).
5. **Escrita fora do MCP de leitura.** Ferramentas com efeito ficam num grupo separado, com `destructiveHint` correto,
   e só depois da iniciativa de escrita controlada (P3).
6. **Orçamento em tokens**, coerente com o modelo de orçamento do motor, e não só em caracteres.

## 3. Catálogo recomendado

Legenda: **E** existe, **A** ampliar o existente, **N** nova. Fase: ver [ROADMAP.md](ROADMAP.md).

### 3.1 Bootstrap e contexto técnico

| Ferramenta | Situação | Pergunta que responde | Retorno estruturado | Fase |
| --- | --- | --- | --- | --- |
| `spock_context_bootstrap` | **N** (substitui o uso de `list_projects` como porta de entrada) | "Que projeto é este e o que o SPOCK sabe dele?" | `projectId`, nome, `contextVersion`, perfil resumido (≤ 600 tokens), políticas obrigatórias, conexões disponíveis com estado e última sincronização, ferramentas liberadas, o que buscar sob demanda, limitações | P1 |
| `project_briefing` | **A** | Resumo técnico e gerencial | Igual ao atual mais `structuredContent` e proveniência | P1 |
| `project_architecture` | **N** | "Como este projeto está organizado?" | Camadas, módulos, direção de dependências, evidência por item, classificação (descoberto/aprovado), divergências | P1 (depende de P1-5 da auditoria) |
| `project_rules` | **N** | "Quais regras valem para este arquivo/pasta?" | Regras aplicáveis ao caminho, origem (Harness, decisão, `AGENTS.md`), versão | P1 |
| `search_project` | **A** | Busca de trechos | Trechos com caminho, linhas, hash, `indexedAt`, `stale`, fonte (repo, decisão, externo autorizado) | P1 |
| `decisions` | **E** | Decisões registradas | Mais `structuredContent` | P1 |

### 3.2 Engenharia de IA

| Ferramenta | Situação | Retorno | Fase |
| --- | --- | --- | --- |
| `harness_status` | **N** (dados já existem em `HarnessStatusService`) | Componentes, estado (íntegro, modificado, faltando), versão do contexto gerado por IDE, divergência entre canônico e arquivos | P1 |
| `agent_policy` | **N** (dados em `AgentPolicy`) | Regras deny/ask/allow vigentes para a IDE | P2 |

### 3.3 Gestão

Um conjunto pequeno, cada ferramenta com filtros e proveniência. Os nomes conceituais do pedido foram fundidos onde
seriam redundantes.

| Ferramenta | Situação | Cobre | Retorno | Fase |
| --- | --- | --- | --- | --- |
| `project_status` | **N** (sobre `ManagementService`) | Andamento, saúde, indicadores, atrasados, bloqueados, próximo marco | Métricas do catálogo ([ENTERPRISE_INTELLIGENCE.md](ENTERPRISE_INTELLIGENCE.md) §4) com fórmula, período, fonte, `syncedAt`, `notCalculated` com motivo | P1 |
| `project_work_items` | **N** | Itens por estado, tipo, marco, bloqueio, dependência; filtro por chave | Itens com chave nativa, estado, datas, ligação com execuções e PRs | P1 |
| `project_risks` | **N** (sobre `RiskRules`/`RiskHistory`) | Riscos e bloqueios atuais e a variação | Regra que disparou, evidência, desde quando | P1 |
| `project_milestones` | **N** | Marcos, previsão, comprometimento | Data alvo, previsão Monte Carlo, probabilidade, limitações | P1 |
| `project_changes` | **N** (sobre `ProjectTimeline`) | "O que mudou desde X?" | Eventos com data e fonte | P1 |
| `work_item_context` | **N** | "Quais os critérios de aceite da tarefa em que estou?" | Descrição, critérios, comentários autorizados, ligações (PR, branch, execuções); resolve a chave pelo argumento ou pelo nome da branch | P1 (depende de ler `description` do Jira, INT-07) |
| `portfolio_overview` | **N** (sobre `Portfolio`/`PortfolioSignals`) | Projetos, sinais, maiores riscos | Só os projetos que a credencial e o vínculo permitem | P2 |

`organization_get_overview` não entra: sem organização no modelo de domínio (Modelo A), seria uma ferramenta vazia.

### 3.4 Entrega

| Ferramenta | Situação | Retorno | Fase |
| --- | --- | --- | --- |
| `delivery_check` | **N** | Para um item ou uma branch: critérios contra evidência (diff, testes, validação), pendências, o que não pôde ser verificado | P1/P2 ([DELIVERY_VERIFICATION.md](DELIVERY_VERIFICATION.md)) |
| `git_changes` | **N** (P1-3 da auditoria) | Diff resumido, commits da branch, arquivos fora do escopo declarado | P1 |
| `pull_request_status` | **N** | PR ligado, reviews, checks | P2 |

### 3.5 Infraestrutura (existente)

Mantém-se como está, com três correções: composição comum (MCP-2), `readOnlyHint` coerente (MCP-3) e auditoria em todas
(MCP-5). A lista dinâmica por vínculo passa a usar `notifications/tools/list_changed` onde o cliente suporta, em vez de
pedir reinício.

## 4. Resources e prompts

| Tipo | Proposta | Por que | Ressalva |
| --- | --- | --- | --- |
| Resource `spock://project/{id}/profile` | Perfil técnico como recurso legível | Clientes que deixam a pessoa anexar recursos (`@` no Claude Code) | Suporte varia por cliente ([IDE_COMPATIBILITY_MATRIX.md](IDE_COMPATIBILITY_MATRIX.md)); ferramenta continua sendo o caminho principal |
| Resource `spock://project/{id}/rules` | Regras canônicas | Idem | Idem |
| Prompt `spock-status` | "Como está o projeto?" com as chamadas certas | Vira comando na IDE onde prompts são suportados | Atalho, não requisito |
| Prompt `spock-delivery-check` | Conferir a entrega atual | Idem | Idem |

Ferramentas continuam sendo o contrato principal porque são o único recurso MCP suportado por todos os clientes avaliados.

## 5. Contrato de cada operação

Toda ferramenta nova segue a mesma ficha, versionada em `contracts/schemas/mcp/` com fixtures válidas e inválidas, como
já é regra para o protocolo do motor:

| Campo | Exemplo para `project_status` |
| --- | --- |
| Nome e descrição | `project_status`: "Andamento do projeto com indicadores calculados pelo SPOCK, fonte e data" |
| Schema de entrada | `{ project?: string, period?: "7d" \| "30d" \| "90d", workType?: string }` |
| Schema de saída | `{ projectId, contextVersion, metrics[]: {id, value, unit, formula, period, source, syncedAt, freshness, limitations[]}, notCalculated[], limitations[] }` |
| Escopo | Projeto vinculado; fonte com política `management` liberada |
| Impacto | Leitura local; nenhuma chamada externa |
| Erros | `PROJECT_NOT_BOUND`, `PROJECT_AMBIGUOUS`, `SOURCE_NOT_LINKED`, `SOURCE_STALE` (aviso, não erro), `POLICY_DENIED` |
| Limites | Até N métricas; resposta ≤ 2.000 tokens |
| Auditoria | `ide.context_read` com ferramenta, ids de métricas, sem valores |

## 6. MCP e plugins

O MCP é o contrato de integração. Pacotes de distribuição (plugin do Claude Code, extensão do Gemini CLI, plugin do Codex)
são **embalagem** da mesma coisa: configuração MCP + instruções + skills + hooks. O estudo recomenda não construir
marketplace nem publicar pacote antes de os adaptadores por projeto estarem validados (P2). Ver
[CONTEXT_PERSISTENCE_AND_ADAPTERS.md](CONTEXT_PERSISTENCE_AND_ADAPTERS.md) §7.
