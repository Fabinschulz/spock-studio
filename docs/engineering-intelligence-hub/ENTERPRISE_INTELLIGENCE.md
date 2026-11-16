# Inteligência de projetos e portfólio

> Parte do estudo [Engineering Intelligence Hub](README.md) (2026-11-16). Reúne o que o pedido chamou de
> ENTERPRISE_PROJECT_INTELLIGENCE, MANAGEMENT_DATA_MODEL e MANAGEMENT_METRICS_CATALOG. O desenho anterior da Gestão está em
> [../plataforma/evolucao-engineering-management.md](../plataforma/evolucao-engineering-management.md) e continua valendo.

## 1. Princípio

A gestão do SPOCK é **calculada no motor, a partir de dados sincronizados, com a fonte e a data de cada número**. Essa
regra já está implementada e é o que torna seguro levar a gestão para a IDE: o modelo da IDE não precisa calcular nada,
só ler números prontos e citá-los.

O SPOCK não é ERP nem cópia concorrente do Jira ou do Azure DevOps. Ele lê da fonte oficial, guarda o que leu com data,
calcula e explica. Nada é criado no provedor.

## 2. Modelo de domínio

### 2.1 O que existe

| Conceito | Representação | Onde |
| --- | --- | --- |
| Projeto | `AuthorizedProject(ProjectId, DisplayName, DisplayPath)` | `Workspace/AuthorizedProject.cs` |
| Portfólio | Projeção dos projetos autorizados na máquina, com sinais | `Management/Portfolio.cs`, `PortfolioSignals.cs` |
| Item de trabalho | `WorkItem` com tipo, estado, categoria, datas, transições, `BlockedBy`, marcos, responsáveis | `Integrations/WorkItem.cs:6-23` |
| Fonte | `ProjectIntegration` + `MetricSource` (provedor, recurso, `LastSyncedAt`, `Complete`) | `Integrations/ProjectIntegration.cs`, `FlowMetrics.cs:35-41` |
| Métricas | `ProjectMetrics`, `KindFlow`, `NotCalculated` | `FlowMetrics.cs` |
| Previsão | `ProjectForecast`, cenários P15/P50/P85, premissas e limitações | `Forecast.cs`, `ForecastCalculator.cs` |
| Marco | `ProjectMilestones` (milestone do GitHub, fixVersion do Jira) | `Milestones.cs` |
| Risco | `ProjectRisk` por regra, com histórico | `Risks.cs`, `RiskHistory.cs` |
| Decisão gerencial | Imutável, com o retrato dos fatos | `Decisions.cs` |
| Execução ligada a item | `RunLink(IntegrationId, Kind, ExternalId)` | `Execution/RunLinkService.cs` |

Não existem: organização, equipe, papel, dependência entre projetos, roadmap ou fase como entidade, planejado por período.

### 2.2 Hierarquia proposta, avaliada contra o existente

```text
Organization            ── N (só no Modelo C; ver DEPLOYMENT_MODELS.md)
  └── Portfolio         ── A: hoje projeção da máquina; vira definição nomeada (lista de projetos + filtros)
        └── Project     ── R: AuthorizedProject + id estável (ADR-0004)
              ├── Sources          R: ProjectIntegration (Jira, GitHub, depois Azure DevOps)
              ├── WorkItems        R: WorkItem, com chave nativa e origem
              ├── Milestones       R: fixVersion / milestone
              ├── Plan             N: fases e roadmap só quando a fonte tiver (épicos, iterações do Azure DevOps, sprints do Jira)
              ├── Risks            R: regras + histórico
              ├── Dependencies     A: BlockedBy hoje; ligação entre itens de projetos diferentes depois
              ├── Decisions        R
              └── Engineering      R: perfil, Harness, execuções, entregas verificadas (DELIVERY_VERIFICATION.md)
```

Regras de modelagem:
- **Sem entidade duplicada.** Fase e roadmap só existem se vierem da fonte (épico, iteração, sprint, versão). O SPOCK não
  inventa um planejamento paralelo.
- **Chave nativa sempre preservada** (`PROJ-123`, `owner/repo#45`, `AB#678`), junto com o provedor e a data de leitura.
- **Responsável é dado, não indicador.** `Assignees` aparece no item; nenhum número é agregado por pessoa (decisão
  vigente).

## 3. Engenharia e Gestão compartilhando o mesmo estado

| Fato | Produzido por | Lido pela Gestão como | Lido pela IDE como |
| --- | --- | --- | --- |
| Item `PROJ-123` em andamento | Sincronização do Jira | WIP, cycle time | `work_item_context` |
| Execução de engenharia ligada a `PROJ-123` | Motor (`RunLink`) | "Três pedidos de engenharia, um com ressalva" | Histórico do item |
| Entrega verificada com pendência | Verificador de entrega (P1/P2) | Risco "entrega com pendência" (regra nova) | `delivery_check` |
| Decisão "adiar o marco 2" | Gestor no desktop | Linha do tempo, relatório | `decisions` e briefing |
| Harness alterado | Plano aprovado | Linha do tempo ("ritmo mudou depois da configuração") | `harness_status` |

Não há "visão de engenharia" e "visão de gestão" do mesmo item; há um item e projeções.

## 4. Catálogo de indicadores

Hoje as definições estão espalhadas (`DeliveryRules.cs`, `flow-section.tsx`, `delivery-labels.ts`). A proposta é um
**catálogo único versionado** (`contracts/management/metrics.json`), lido pelo motor para montar a resposta, pela tela
para os textos de ajuda e pelo MCP para o `outputSchema`. Cada entrada tem os sete campos pedidos.

| Id | Definição | Fórmula (código atual) | Fonte | Período | Limitações conhecidas | Existe? |
| --- | --- | --- | --- | --- | --- | --- |
| `flow.delivered` | Itens entregues | Categoria `done`, `ClosedAt` presente, fora `not_planned`/`duplicate`, PR fechado sem merge não conta (`DeliveryRules.cs:11-15`) | Jira, GitHub | Janela escolhida | Depende do mapeamento de status do provedor | Sim |
| `flow.wip` | Em andamento agora | Contagem com categoria `inProgress` (`FlowCalculator.cs:25`) | Jira, GitHub | Instante da sincronização | GitHub não tem "em andamento" nativo | Sim |
| `flow.throughput` | Entregues por semana | Entregues por janela de 7 dias; média = total/semanas (`FlowCalculator.cs:33-49`) | Jira, GitHub | 6–12 semanas | Semanas sem sincronização completa distorcem | Sim |
| `flow.leadtime` | Criação até entrega | Mediana e P85 de `ClosedAt - CreatedAt`, mínimo 5 amostras (`:51-64`) | Jira, GitHub | Janela | Itens importados com data de criação artificial | Sim |
| `flow.cycletime` | Início até entrega | Primeira transição para "em andamento" até `ClosedAt`; excluídos contados (`:70-104`) | Jira (changelog) | Janela | GitHub sem transições: não calcula | Sim |
| `flow.blocked` | Bloqueados | Rótulo `blocked`/`bloqueado` ou link "is blocked by" (`:107-119`) | Jira; GitHub só por rótulo | Instante | Link só do tipo padrão "Blocks" | Sim |
| `flow.overdue` | Vencidos | Aberto com `DueDate` anterior a hoje (`:121-127`) | Jira | Instante | GitHub sem prazo por item | Sim |
| `forecast.delivery` | Previsão de conclusão | Monte Carlo, 10.000 rodadas, semente dos dados, P15/P50/P85 (`ForecastCalculator.cs`) | Histórico de vazão | 6–12 semanas | Recusa com menos de 6 semanas | Sim |
| `milestone.outlook` | Situação do marco | Prazo comparado a P50/P85 (`MilestoneCalculator.cs:54-67`) | fixVersion, milestone | Até o prazo | Sem ordem nem dependência entre itens | Sim |
| `risk.*` | Seis regras | `overdue`, `blocked`, `stalled-wip` (14 d), `review-queue` (7 d), `scope-growth` (1,5×), `throughput-drop` (`RiskRules.cs`, `TrendRules.cs`) | Dados sincronizados | Janela | Limiares fixos, não configuráveis | Sim |
| `portfolio.signal.*` | Sinais do portfólio | `no-sources`, `never-synced`, `sync-incomplete`, `connection-problem`, `stale` (>7 d), `overdue` (`PortfolioSignals.cs`) | Estado das fontes | Instante | — | Sim |
| `flow.scopechange` | Mudança de escopo | — | — | — | Exige histórico do escopo | Não (`notCalculated`) |
| `flow.predictability` | Planejado contra entregue | — | Sprints/iterações | — | Exige planejado por período | Não |
| `delivery.pending` | Entregas com pendência | Contagem de verificações de entrega com pendência aberta | Verificador de entrega | Janela | Só para itens com verificação | Proposta (P2) |
| `infra.drift` | Recursos com drift | Último drift por workspace | IaC | Instante do drift | Só projetos com IaC | Dado existe; não é indicador de gestão |
| `cost.*` | Custo | — | — | — | O motor não conhece preço de uso; só estimativa de plano | Fora (decisão D4) |

Cada resposta de métrica traz, além do valor: `syncedAt` da fonte mais antiga usada, `complete` (a sincronização
terminou?), `freshness` e `limitations[]`. Se a fonte está velha, o valor vem com `freshness: "stale"`; se a última
sincronização falhou, o valor é o da última completa, **com a data dela**, nunca apresentado como atual (cenário 9).

## 5. Assistente de gestão: na tela e na IDE

| Aspecto | Na tela (existe) | Na IDE (proposta) |
| --- | --- | --- |
| Quem redige | Modelo configurado no SPOCK | Modelo da IDE |
| Quem calcula | Motor | Motor (mesmas ferramentas) |
| Verificação | `AnswerVerifier` rejeita afirmação sem fato citado ou número fora dos fatos | Não há como verificar o texto da IDE. A proteção vira **contrato de dados**: os números chegam estruturados, com id; a instrução persistente pede citar o id e a data; as ferramentas devolvem `limitations` que o modelo é instruído a repetir |
| Separação | observado, calculado, inferido, recomendado, indisponível | Mesmo vocabulário nos campos da resposta MCP (`facts[]`, `calculations[]`, `limitations[]`) |
| Quando faltar dado | "Indisponível" com motivo | `notCalculated[]` com motivo; a instrução manda dizer "o SPOCK não tem esse dado" |

**Opção para perguntas abertas na IDE:** uma ferramenta `management_answer` que roda o assistente verificado do próprio
SPOCK e devolve a resposta já conferida. Custa tokens do modelo do SPOCK e duplica o trabalho do modelo da IDE.
Recomendação: começar só com dados estruturados; avaliar `management_answer` depois da validação com modelo real (P0-4).

## 6. Escopo e permissões

No Modelo A (local por pessoa):

| Pergunta | Quem responde |
| --- | --- |
| A pessoa pode ver este projeto? | O motor: o projeto foi autorizado na máquina **e** o MCP foi vinculado a ele |
| Pode ver estes itens do Jira? | O Jira, pela credencial da pessoa, no momento da sincronização |
| Estes itens podem ir para a IDE? | A política por destino do vínculo (`management`, `knowledge`, `ide`) — hoje um booleano só (RT-50) |
| Pode ver o portfólio? | Só os projetos autorizados na máquina dela; `portfolio_overview` nunca lê outro registro |

O que isso garante: um desenvolvedor com credencial restrita não vê o portfólio inteiro, porque o SPOCK dele não tem
esses dados. O que não garante: diferenciar papéis **dentro** de uma mesma credencial. Isso exige identidade e papéis
(DD-2) e só faz sentido no Modelo C ou B.

Dados financeiros e informações pessoais: o SPOCK não sincroniza custo de pessoa nem dados de RH. Responsáveis aparecem
por login do provedor. Nenhuma ferramenta MCP agrega por pessoa.

## 7. Perguntas da IDE e a ferramenta que responde

| Pergunta | Ferramenta | Dado atual | Lacuna |
| --- | --- | --- | --- |
| Qual o status deste projeto? | `project_status` | Sim | Saída estruturada |
| Quais atividades estão em andamento? | `project_work_items` (estado = em andamento) | Sim | Títulos dependem da política |
| Quais entregas estão atrasadas? | `project_work_items` (vencidos) + `project_milestones` | Sim (Jira) | GitHub sem prazo por item |
| Quais issues estão bloqueadas? | `project_risks` / `project_work_items` | Sim | — |
| Critérios de aceite desta tarefa? | `work_item_context` | **Não**: `description` não é lida (INT-07) | Ler `description` e campo de critérios |
| Qual o próximo marco? | `project_milestones` | Sim | — |
| Quais os riscos atuais? | `project_risks` | Sim | — |
| O que mudou desde a semana passada? | `project_changes` | Sim (linha do tempo) | — |
| Qual PR está ligado à entrega? | `work_item_context` | Parcial: ligação manual de execução | Convenção de branch e "Closes #n" (P1-3) |
| O que falta para concluir a fase? | `project_milestones` + `delivery_check` | Parcial | Verificação de entrega |
