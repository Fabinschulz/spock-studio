# Connector Hub

> Parte do estudo [Engineering Intelligence Hub](README.md) (2026-11-16). Parte III do pedido. ADR:
> [0005](../architecture/adr/0005-mcp-connector-hub.md).

## 1. Inventário real

| Categoria | Implementado | Validado com serviço real | Não existe |
| --- | --- | --- | --- |
| Desenvolvimento | GitHub (issues e PRs como itens), Git local (`status`) | GitHub com repositório real | Branches, commits, diff, reviews, checks; GitLab; Bitbucket |
| Gestão de projetos | Jira Cloud (issues, changelog, links, fixVersions) | Só nos erros (credencial inválida, site fora do padrão) | `description`, critérios, comentários, sprints; Jira Server/Data Center; Azure DevOps; Linear |
| Bancos de dados | — | — | Todos |
| Cloud e IaC | AWS, Azure, GCP (leitura); OpenTofu e Terraform (validate, plan, apply aprovado, state, drift, custo de plano) | AWS por SSO; IaC só com recursos locais | Kubernetes, Docker como conexão |
| Documentação | Arquivos do repositório no índice; decisões | Sim | Wikis, Confluence, documentos corporativos |
| MCP de terceiros | Inventário de `.mcp.json` e `.codex/config.toml` (sem valores de segredo) | — | Cliente MCP, teste, grants, auditoria de uso |
| Modelos de IA | OpenAI, Anthropic, Gemini (chave no cofre) | Só recusa de chave inválida | — |

O desenho de domínio é bom e não precisa mudar: **conexão** (credencial, no cofre) ≠ **vínculo** (conexão + projeto) ≠
**recorte** (`DataScope`) ≠ **política** (`ContextualizationPolicy`). "Uma conexão ativa não dá acesso a agentes"
(`ConnectionReach.cs:29`).

## 2. Catálogo central

`IntegrationConnection` + `ProjectIntegration` já são o catálogo. Faltam campos para o hub:

| Campo pedido | Hoje | Proposta |
| --- | --- | --- |
| Identidade, tipo, provedor, estado | Sim | — |
| Autenticação | Tipo implícito no provedor | `authKind` explícito (`token`, `oauth`, `sso-profile`, `none`) |
| Associação com projeto | Sim (vínculo) | Lista de projetos permitidos **na conexão** (INT-24a), opcional |
| Escopo | `DataScope` | — |
| Permissões | Implícitas na credencial | `reach` medido no teste (`ConnectionReach`) e exibido |
| Política de contextualização | Um booleano | **Por destino** (ADR-0007): `management`, `knowledge`, `workflow`, `assistant`, `ide` |
| Operações autorizadas | Só leitura por decisão | `operations[]` com `read` padrão; `write` só com contrato futuro |
| Última sincronização, saúde | Sim | Exposto no bootstrap e em cada resposta |
| Origem dos dados | `MetricSource`, `@external/` | `nativeId` em toda resposta MCP |

O usuário cadastra uma vez no SPOCK e vincula a projetos; as IDEs **nunca** recebem a credencial nem uma cópia do
cadastro.

## 3. SPOCK como gateway

```text
Claude Code / Cursor / Codex
            │  stdio, projeto vinculado
            ▼
      SPOCK MCP (Mcp/)
            │  resolve projeto, aplica política por destino "ide"
            ▼
   Context Engine / serviços do motor
            │
   ┌────────┼──────────────┬──────────────┬───────────────┐
   ▼        ▼              ▼              ▼               ▼
 Dados    Índice      Cálculos de     Leitura cloud   MCP externo (P2,
sincron.  (Python)    gestão          (SDK, agentAccess)  cliente do SPOCK)
(Jira,
GitHub)
```

O que fica central: autenticação, seleção de projeto, política, auditoria, limites e redação de segredos. A IDE recebe
**capacidades**, não acesso.

Regras:
1. **Nada de proxy indiscriminado.** O SPOCK não repassa `tools/list` de um servidor externo para a IDE. Cada capacidade
   externa exposta é uma ferramenta do SPOCK com nome, schema, impacto e política próprios, ou não é exposta.
2. **Ligação direta é exceção explícita.** Se a pessoa quiser um MCP de terceiro direto na IDE, o SPOCK mostra no
   inventário e no bootstrap que aquela conexão está fora da governança central (sem auditoria nem política do SPOCK).
3. **Dados sincronizados antes de chamadas ao vivo.** As respostas de gestão vêm do que foi sincronizado, com data;
   chamada ao vivo ao provedor só pela sincronização controlada.

## 4. Jira e Azure DevOps

| Necessidade do pedido | Jira hoje | Proposta | Azure DevOps |
| --- | --- | --- | --- |
| Projetos, itens, épicos, features | Itens e `parent` | Usar `parent` na gestão (hierarquia) | Work items com `System.Parent` e tipos (Epic, Feature, User Story, Task, Bug) |
| Sprints e iterações | Não lidos | Ler sprint (campo do Jira Software) | `System.IterationPath` e iterações da equipe |
| Status, responsáveis, prioridades, datas | Sim (sem prioridade) | Acrescentar `priority` | `System.State`, `System.AssignedTo`, `Microsoft.VSTS.Common.Priority`, datas |
| Dependências | Só "is blocked by" | Tipos de link configuráveis | Relações `Dependency-Forward/Reverse` |
| Critérios de aceite | **Não** | `description` e campo personalizado configurável (nome do campo por projeto) | `Microsoft.VSTS.Common.AcceptanceCriteria` (campo nativo) |
| Comentários autorizados | Não | Leitura opcional, sob política `knowledge`, tratados como não confiáveis | Comentários do work item |
| PRs, builds, pipelines, releases, testes | — | GitHub (P1-3) | Repos, Pipelines e Test Plans do Azure DevOps (P2, por partes) |
| Autenticação | E-mail + token | OAuth 2.0 (3LO) pendente (A3) | PAT no cofre; Entra ID depois |

Ordem recomendada: **validar o Jira de ponta a ponta e ler critérios antes do Azure DevOps.** A regra "dois conectores
comprovados antes de outro" (`SPOCK_PRODUCT_EVOLUTION.md:329`) continua sensata: hoje só o GitHub está comprovado. O
conector Azure DevOps reaproveita `IIntegrationConnector`, `WorkItem`, o checkpoint incremental e o transporte só GET.

Preservar a origem: `WorkItem` guarda provedor, chave nativa e URL; nenhum id do SPOCK substitui o do provedor nas
respostas.

## 5. Bancos de dados

Escopo recomendado: **contexto técnico, não cliente de consulta.**

| Nível | O que expõe | Risco | Fase |
| --- | --- | --- | --- |
| 1. Do repositório | Migrações, modelos do ORM, scripts SQL já no código | Nenhum novo | P1 (detector, sem conexão) |
| 2. Metadados | Schemas, tabelas, colunas, chaves, índices, views; procedures só por lista | Revela estrutura | P2: conexão só leitura, usuário com permissão só de catálogo, cofre |
| 3. Consultas aprovadas | Consultas parametrizadas declaradas em `mariliz.yaml`, com teto de linhas e tempo | Revela dados | P3, com decisão |
| 4. Consulta livre | — | Alto | Fora |

Regras para os níveis 2 e 3: credencial só no cofre; conexão marcada por ambiente (produção nunca por padrão, R-16);
leitura com timeout e teto de linhas; mascaramento de colunas marcadas como sensíveis; auditoria por consulta; nenhum
resultado entra no índice.

## 6. Cliente MCP (P2)

O inventário existente (`McpInventory`) é o ponto de partida. Ciclo proposto:

| Etapa | Descrição |
| --- | --- |
| Cadastrar | Servidor stdio ou HTTP, com origem verificável e versão fixada (regra já em `rules/general.md:46`) |
| Validar | Conectar numa sessão isolada; ambiente filtrado (R-10) |
| Inventariar | `tools/list`, `resources/list`, schemas e anotações |
| Classificar | Cada ferramenta: leitura, escrita, destrutiva, mundo aberto. As anotações do servidor são **dica não confiável** (a spec 2026-07-28 diz isso): a classificação é da pessoa |
| Permitir | Por ferramenta e por projeto; padrão negado |
| Expor | Como ferramenta do SPOCK (`ext_<servidor>_<ferramenta>`), só as permitidas, com resultado redigido e rotulado como externo |
| Auditar | Cada chamada em `audit.ndjson` |
| Revogar | Remove a permissão e a ferramenta (`tools/list_changed`) |

Antes do cliente MCP, decidir se ele é requisito (pendência da auditoria, P3-5). O estudo recomenda P2, depois do SPOCK
Connect, porque é a forma de "configurar uma vez" que o pedido descreve.
