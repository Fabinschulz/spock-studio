# Auditoria de integrações

> Parte da auditoria estratégica do SPOCK (2026-11-15). Navegação e método em [README.md](README.md).
> Somente leitura: nenhum código foi alterado. Legenda de estados: A implementado e validado, B parcial, C só documentado,
> D não implementado, E substituído por melhoria válida, F substituído sem justificativa, G regressão, H conflito entre
> documento e código, I decisão pendente, J não se aplica.

Extraído da auditoria de engenharia de software; IDs INT-xx.

## 4. Integrações (INT)

### 4.1 Jira

| ID | Item | Class. | Evidência |
| --- | --- | --- | --- |
| INT-01 | Issues (leitura) | **B** | `JiraConnector.cs:65-79`, campos `JiraConnector.cs:16`. Validado só nos erros contra o Atlassian real; ciclo completo nunca rodou (`MARILIZ_CURRENT_STATE.md:145`; `docs/validacao-real.md` "Jira não rodado"). |
| INT-02 | Épicos / hierarquia | **B** | `parent` lido (`JiraConnector.cs:121`), sem uso em gestão (nenhum consumidor de `Parent` em `Management/`). |
| INT-03 | Roadmaps, fases, sprints | **B** | `fixVersions` como marcos (`JiraConnector.cs:143-151`, `Management/MilestoneCalculator.cs`). Sprints e estimativas não são lidos (`docs/plataforma/evolucao-engineering-management.md:194`). |
| INT-04 | Status e histórico | **B** | Categoria do status e transições do changelog (`JiraConnector.cs:109-114,157-166`); histórico pode vir truncado (`evolucao…md:193`). |
| INT-05 | Classificação (tipo) | **B** | Lido, não usado (HC-04). |
| INT-06 | Dependências | **B** | Só "is blocked by" do tipo padrão "Blocks" (`JiraConnector.cs:128-155`). |
| INT-07 | Critérios de aceite | **D** | Sem `description` nem campos personalizados; os documentos externos não levam descrição nem comentários (`Integrations/ExternalKnowledge.cs:24`). |
| INT-08 | Evidência, comentários, anexos, worklog | **D** | Não lidos. |
| INT-09 | Escrita (criar, editar, transicionar, comentar) | **D** / **I** | Transporte só GET; decisão "só leitura". |
| INT-10 | Aprovação para escrita + "Desfazer" | **J** para Jira | O fluxo aprovação + Desfazer existe, mas para ações **locais**: o M2 (`docs/MARILIZ_REAL_WORLD_VALIDATION.md:303`) foi corrigido contando a ação executada como mudança reversível (`Execution/RunKinds.cs:18`, `Execution/RunJournal.cs:57`, `ProjectActionExecutor.cs`). Não há escrita externa a aprovar ou desfazer. Se a escrita no Jira vier, o vocabulário `ImpactKinds.Write` já existe (`Integrations/ConnectionReach.cs:5-13`). |
| INT-11 | Autenticação e segredo | **B** / **I** | E-mail + token de API em Basic (`JiraConnector.cs:168-178`), site restrito a `*.atlassian.net` (`JiraConnector.cs:180`), Server/Data Center fora. Cofre DPAPI validado; Keychain/Secret Service não rodados (`MARILIZ_CURRENT_STATE.md:146`). OAuth 2.0 (3LO) pendente (A3, `MARILIZ_REAL_WORLD_VALIDATION.md:288`). |
| INT-12 | Escopo por projeto | **A** | O recurso é a chave do projeto e o JQL é montado pelo recorte (`JiraConnector.cs:85-94`); vínculo encontrado só pelo projeto dono (`Integrations/ProjectIntegrationService.cs:21-35`). Testes `IntegrationHubTests`. |

### 4.2 GitHub e Git

| ID | Item | Class. | Evidência |
| --- | --- | --- | --- |
| INT-13 | Issues e PRs (leitura) | **A** | `GitHubConnector.cs:54-95`; validado com repositório real (`MARILIZ_CURRENT_STATE.md:144`). |
| INT-14 | Branches e commits | **D** | Nenhuma chamada. |
| INT-15 | Reviews e comentários | **D** | Nenhuma chamada. |
| INT-16 | Checks / CI | **D** | `CiDetector` só reconhece arquivos de workflow no disco (`Discovery/CiDetector.cs:7`). |
| INT-17 | Ligação demanda ↔ implementação | **B** | Ligação manual de uma execução a até 10 itens sincronizados e caminho inverso (`RunLinkService.cs:26,31-66,92-117`); sem ligação com branch, commit ou PR por convenção (chave no nome da branch, "Closes #n"). Testes `RunLinkTests` (21). |
| INT-18 | Escrita (branch, PR, comentário) | **D** / **I** | Decisão "só leitura". |
| INT-19 | Git local | **B** | SE-15. |

### 4.3 IDE e CLIs de IA

| ID | Item | Class. | Evidência |
| --- | --- | --- | --- |
| INT-20 | IDE via MCP do Spock | **A** | Validado por stdio (`MARILIZ_CURRENT_STATE.md:99`). A UI gera o comando com `--project` do projeto aberto por padrão (`desktop/ui/src/features/agents/components/ide-connection-section.tsx:49-53`). **Risco:** sem o argumento, o servidor expõe **todos** os projetos autorizados (`Mcp/McpProjectResolver.cs:8-9`). Testes `McpScopeTests`. |
| INT-21 | CLIs de IA (Claude Code, Codex, Cursor Agent, Gemini) | **B** | Só leitura, 8.000 caracteres, 10 min, saída redigida (`AgentRunService.cs:23-27`, `AiToolCatalog.cs:17-23`). Nunca validado com CLI instalada (`MARILIZ_CURRENT_STATE.md:98`). A execução não se liga a item de trabalho nem captura diff. |
| INT-22 | Ambientes de IA configuráveis pelo Harness | **B** | Só `claude-code` (`Discovery/AiEnvironments.cs:10`); Cursor/Codex bloqueiam o plano (`HarnessComposition.cs:123-132`). |

### 4.4 Separação de autenticação, vínculo, escopo, permissões e política

| ID | Item | Class. | Evidência |
| --- | --- | --- | --- |
| INT-23 | Autenticação ≠ vínculo ≠ recorte ≠ política | **A** | `IntegrationConnection` (sem credencial), `ProjectIntegration`, `DataScope`, `ContextualizationPolicy` que nasce fechada (`Integrations/ContextualizationPolicy.cs:4-6`, `ProjectIntegration.cs`, `DataScope.cs`). "Uma conexão ativa não dá acesso a agentes" (`ConnectionReach.cs:29`). Testes `IntegrationHubTests`, `ConnectionReachTests`, `ExternalKnowledgeTests`. |
| INT-24 | Uma conexão dá acesso automático a todos os projetos/agentes? | **A** (não dá) com ressalvas | Cada projeto precisa de vínculo explícito; dado externo só vira contexto com `feedsKnowledge` (`ExternalKnowledge.cs:56`). Ressalvas: (a) a mesma credencial pode ser vinculada a qualquer projeto, sem lista de projetos permitidos do lado da conexão; (b) o alcance real é o da conta (token Jira não tem escopo, `JiraConnector.cs:30`; token GitHub refinado não informa escopos, `GitHubConnector.cs:24`); (c) sem identidade de pessoa, quem vincula é sempre o único usuário. |
| INT-25 | Permissões por agente / operações autorizadas | **B** | A política é **um booleano por vínculo** que vale ao mesmo tempo para índice, workflows, investigação e assistente (`MARILIZ_CURRENT_STATE.md:148`); não há permissão por agente ou finalidade. Cloud tem `agentAccess` por vínculo (`Cloud/CloudProjectService.cs:23,42-55`). Ações do workflow limitadas a `file.write` no alvo de decisão (`ActionGovernance.cs:29,37`). Ferramentas do agente externo governadas pelo hook (`Assets/hooks/mariliz-policy.py:7-14`, `Assets/catalog/policy.json`), lidas só para exibição (`Engineering/AgentPolicy.cs`, não editável). |

---


## Outras superfícies de integração

| Superfície | Onde está auditada |
| --- | --- |
| Servidor MCP do SPOCK (stdio, só leitura) e inventário de MCP de terceiros | [AI_ENGINEERING_AUDIT.md](AI_ENGINEERING_AUDIT.md), AI-26 a AI-28 |
| CLIs de IA da pessoa (D-08) | AI-15 e INT-21 |
| Conectores AWS, Azure e GCP e ferramentas MCP de cloud e IaC | [INFRASTRUCTURE_MANAGEMENT_AUDIT.md](INFRASTRUCTURE_MANAGEMENT_AUDIT.md), INF-01 a INF-09 |
| Plugins | Não existe sistema de plugins (AI-29, UX-12) |

### Princípio "MCP e plugins não duplicam regra de negócio"

O servidor MCP (`runtime/Mariliz.Runtime/McpEntry.cs`) reaproveita os serviços da camada Application, mas repete a
composição feita em `Program.cs` (ARCHITECTURE_AUDIT, smell S7). A regra de negócio não está duplicada; a montagem está.
Como não há sistema de plugins, a segunda metade do princípio ainda não se aplica.
