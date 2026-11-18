# Avaliação dos MCPs disponíveis

> Parte do estudo [Plataforma de engenharia com IA](README.md), 2026-11-17. Validação feita **só com leituras**:
> nenhuma criação, edição, transição, comentário ou exclusão foi feita em Jira, GitHub, AWS ou outro serviço. Os
> identificadores de conta, e-mails de pessoas e nomes de projetos do site Atlassian foram omitidos de propósito.

## 1. Resumo

Dos 24 servidores MCP configurados nesta sessão do Claude Desktop, **9 respondem a leituras reais** (Atlassian Rovo,
Datadog, Postman, Miro, Claude Docs, Claude Data, Meetings, pdf-viewer, scheduled-tasks). O **GitHub não tem MCP
autenticado**: a leitura do GitHub foi comprovada pelo `gh` CLI. O **AWS MCP está quebrado** nas duas formas em que
aparece (o conector remoto pede autenticação para `run_script`; o servidor local `aws-mcp` fecha a conexão). Nove
servidores de plugin aguardam login e três falham por configuração.

Do ponto de vista de desenho, o problema que mais pesa para agentes é o mesmo em todos os servidores grandes:
**ferramentas de leitura e de escrita convivem no mesmo servidor, sem modo somente leitura declarado**. A proteção
contra escrita depende de política externa (Datadog), de escopo do token (Jira tem `write:jira-work`) ou da disciplina
do agente. O segundo é o **excesso de payload** (Jira devolve URLs de avatar e `self` em cada nó; 19 projetos custaram
~14 mil caracteres).

| MCP | Conectividade | Autenticação | Leitura | Contratos | Organização | Segurança | Erros | Uso por agentes |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Atlassian Rovo (Jira) | Aprovado | Aprovado | Aprovado (Jira); Confluence e Compass falharam | Requer melhorias | Requer melhorias | Requer melhorias | Requer melhorias | Requer melhorias |
| GitHub (`gh` CLI) | Aprovado | Aprovado | Aprovado | Adequado | Adequado | Requer melhorias | Adequado | Adequado |
| GitHub (plugin MCP) | Inconclusivo | Falhou (needs_auth) | Inconclusivo | — | — | — | — | — |
| AWS MCP (conector remoto) | Aprovado | Falhou para `run_script` | Parcial (só `list_regions`) | Requer melhorias | Requer melhorias | Requer melhorias | Adequado | Requer melhorias |
| `aws-mcp` (stdio local) | Falhou | Inconclusivo | Falhou | Inconclusivo | — | Requer melhorias | — | — |
| Datadog (conector) | Aprovado | Aprovado | Aprovado | Adequado | Requer melhorias | Adequado | Adequado | Requer melhorias |
| Postman | Aprovado | Aprovado | Aprovado | Requer melhorias | Adequado | Requer melhorias | Inconclusivo | Adequado |
| Miro | Aprovado | Aprovado | Aprovado | Requer melhorias | Requer melhorias | Inconclusivo | Inconclusivo | Requer melhorias |
| Claude Docs | Aprovado | Aprovado | Aprovado (guia) | Adequado | Adequado | Adequado | Inconclusivo | Adequado |
| Claude Data | Aprovado | Aprovado | Aprovado (lista vazia) | Adequado | Adequado | Adequado | Inconclusivo | Adequado |
| Meetings | Aprovado | Aprovado | Aprovado (lista vazia) | Adequado | Adequado | Adequado | Adequado | Adequado |
| pdf-viewer, scheduled-tasks | Aprovado | Não se aplica | Aprovado (listas vazias) | Adequado | Adequado | Adequado | Inconclusivo | Adequado |
| MCP do próprio Spock (código + testes) | Aprovado | Não se aplica (stdio); escopo obrigatório | Aprovado | Requer melhorias | Requer melhorias | Adequado, com uma falha (§5) | Requer melhorias | Requer melhorias |

## 2. Inventário

Levantado por `session_connectors_status` em 2026-11-17, conferido com as chamadas da §3.

| Servidor | Tipo | Estado | Ferramentas |
| --- | --- | --- | --- |
| Atlassian Rovo | conector claude.ai | conectado | 41 (27 de leitura, 14 de escrita pelo nome) |
| Datadog | conector claude.ai | conectado | 501 (escrita bloqueada pela organização, segundo a instrução do servidor) |
| Postman | conector claude.ai | conectado | 41 no conjunto "minimal" (20 de leitura, 21 de escrita); 234 no "full" |
| Miro | conector claude.ai | conectado | 46 |
| AWS MCP | conector claude.ai | conectado, mas `run_script` exige autenticação | 8 |
| Claude Docs | conector claude.ai | conectado | 8 |
| Claude Data | conector claude.ai | conectado | 11 |
| Meetings | conector claude.ai | conectado | 5 (todas de leitura) |
| pdf-viewer | extensão do desktop | conectado | 3 |
| scheduled-tasks | interno do app | conectado | 6 |
| `aws-mcp` | configuração do usuário (stdio, `uvx mcp-proxy-for-aws@latest`) | falhou: `Connection closed` | 0 |
| `plugin:datadog:mcp` | plugin | falhou: `ENOTFOUND` (URL de exemplo `not-setup`) | 0 |
| `plugin:engineering:gmail`, `google calendar` | plugin | falhou: sem URL configurada | 0 |
| `plugin:engineering:{github, atlassian, datadog, slack, linear, asana, notion, pagerduty}`, `plugin:vercel:vercel` | plugin | aguarda login (`/mcp`) | 0 |

Ferramentas fora do MCP usadas como alternativa: `gh` 2.x autenticado (escopos `repo`, `workflow`, `read:org`, `gist`)
e Git local.

## 3. Operações de leitura testadas

| Servidor | Operação | Resultado |
| --- | --- | --- |
| Atlassian | `atlassianUserInfo` | Conta ativa, locale pt-BR |
| Atlassian | `getAccessibleAtlassianResources` | 1 site; escopos `read:jira-work` **e `write:jira-work`**; nenhum escopo de Confluence |
| Atlassian | `getVisibleJiraProjects` (`action=browse`) | 19 projetos, paginação `startAt`/`isLast` correta |
| Atlassian | `searchJiraIssuesUsingJql` (`assignee = currentUser()`, 5 itens, campos restritos) | 5 issues, `pageInfo.endCursor` para a próxima página |
| Atlassian | `searchJiraIssuesUsingJql` com `searchResultMode=count` | Total devolvido (156) sem trazer issues |
| Atlassian | `getJiraIssue` com `updateHistory=false` | Issue devolvida com campos pedidos |
| Atlassian | `getJiraIssue` de chave inexistente | 404 com mensagem "não existe ou você não tem permissão" e cabeçalhos de limite (400/398) |
| Atlassian | `searchJiraIssuesUsingJql` com JQL inválido (`statusx = 1`) | **Lista vazia, sem erro** |
| Atlassian | `getConfluenceSpaces` | 404 (o site não tem Confluence) |
| Atlassian | `getCompassComponents` | 403 `ProductNotFound` (o site não tem Compass) |
| Datadog | `search_datadog_monitors` (`max_tokens=1500`) | 11 monitores com aviso de truncamento e `start_at` da próxima página |
| Datadog | `search_datadog_monitors` com consulta malformada | 400 "Query parsing error: Make sure there is no missing parentheses" |
| Postman | `getAuthenticatedUser`, `getWorkspaces`, `getEnabledTools` | Usuário, 2 workspaces, mapa dos conjuntos de ferramentas |
| Miro | `user_who_am_i`, `board_search_boards` | Identidade e 2 quadros |
| AWS MCP | `list_regions` | 37 regiões (não precisa de credencial) |
| AWS MCP | `run_script` com `sts:GetCallerIdentity` | **"This connector requires authentication"** |
| Claude Docs | `guide(topic.index)` | Guia devolvido |
| Claude Data | `list_certified_queries` | Lista vazia |
| Meetings | `list_meetings` | Lista vazia, com orientação de onde mais procurar |
| scheduled-tasks, pdf-viewer | `list_scheduled_tasks`, `list_pdfs` | Listas vazias |
| GitHub (`gh`) | `auth status`, `api user`, `repo view` (7 repositórios), branches, commits, `rate_limit`, repositório inexistente | Tudo respondeu; limite 5.000/h; 404 claro |

Ferramentas deliberadamente **não** chamadas: `search` do Atlassian (consome créditos Rovo, efeito de custo),
`execute_code` do Datadog (executa código arbitrário), qualquer ferramenta com verbo de escrita, `getTransitionsForJiraIssue`
(é leitura, mas não acrescentava evidência).

## 4. Qualidade do desenho, por servidor

### 4.1 Atlassian Rovo (Jira)

**Funciona** para Jira: identidade, projetos, busca JQL, issue por chave, contagem. Confluence e Compass aparecem como
ferramentas, mas o site não tem esses produtos: 13 ferramentas expostas que nunca vão funcionar aqui.

Problemas encontrados:

- **JQL inválido devolve lista vazia.** `statusx = 1` num projeto inexistente voltou como "nenhum resultado". O agente
  não distingue "não há issues" de "a consulta está errada" e tende a concluir o falso. É o achado mais grave do
  servidor para uso por agentes.
- **Payload inchado.** Cada projeto e cada issue trazem quatro URLs de avatar, `self`, `expand` e um bloco `context`
  com feature flags internos. A lista de 19 projetos custou ~14 mil caracteres; com `expandIssueTypes` seria muito
  mais. Falta um modo compacto (chave, nome, tipo, status).
- **Padrão de `getVisibleJiraProjects` é `action=create`.** Listar projetos filtrando por "onde posso criar" como
  padrão é surpreendente e mistura leitura com intenção de escrita. Para diagnóstico use `browse`.
- **Paginação inconsistente.** `startAt`/`maxResults` (projetos), `nextPageToken` (JQL), `start`/`limit`
  (Confluence), `after` (Compass). Cada ferramenta exige que o agente aprenda um modelo diferente.
- **Efeitos colaterais escondidos em leituras.** `getJiraIssue` tem `updateHistory` (registra a issue como vista
  recentemente) e `search` consome créditos Rovo. Nenhum dos dois é escrita de dado, mas ambos mudam estado ou custo.
- **Escopo de escrita no token.** O site concede `write:jira-work`; não há como pedir uma sessão só de leitura. As 14
  ferramentas de escrita (`createJiraIssue`, `editJiraIssue`, `transitionJiraIssue`, `addCommentToJiraIssue`...)
  ficam ao alcance de qualquer agente.
- **Erro mistura causas.** O 404 diz "não existe ou você não tem permissão": correto por segurança, mas o agente não
  sabe se deve tentar outra chave ou pedir acesso. Ponto positivo: o erro traz o limite de requisições restante.
- **Respostas em idioma do usuário.** A mensagem de erro veio em português; bom para pessoas, ruim para agentes que
  casam texto. Um `code` estável faria falta.

### 4.2 GitHub

O plugin MCP `plugin:engineering:github` aguarda login, então **não há MCP do GitHub funcionando nesta sessão**. O `gh`
CLI cobre a leitura com sobra: JSON com `--json`/`--jq`, paginação nativa, 404 claro e limite visível. O risco é de
segurança, não de contrato: o token tem `repo` e `workflow`, ou seja, escreve em todos os repositórios e workflows do
usuário. Para um agente de leitura, o ideal é um token fino (fine-grained) só de leitura nos repositórios necessários.

### 4.3 AWS

- **Conector remoto (AWS MCP):** 8 ferramentas. `list_regions`, `search_documentation` e afins funcionam sem
  credencial; `run_script` exige autenticação que não está feita. A ferramenta central, `run_script`, executa Python
  arbitrário com `call_boto3`: **leitura e escrita na mesma ferramenta**, controladas só pelo IAM da credencial. Para
  agentes é poderosa e perigosa: não há como declarar "somente leitura" na chamada. O contrato pede que o script
  devolva `result` e lista `api_calls` executadas, o que ajuda a auditar.
- **`aws-mcp` local:** `uvx mcp-proxy-for-aws@latest` fecha a conexão ao iniciar. Causa provável: credencial do perfil
  configurado expirada (SSO) — não confirmada, porque renovar a sessão exige login do usuário. Usar `@latest` sem versão
  fixa baixa código novo a cada início, o que é risco de cadeia de suprimentos.
- **Duplicidade:** os dois caminhos fazem a mesma coisa. Manter os dois confunde o agente sobre qual usar.

### 4.4 Datadog

O servidor mais bem desenhado para agentes entre os testados:

- `max_tokens` por chamada e **truncamento explícito** com instrução de como pedir a próxima página.
- Erro de consulta com status e causa ("missing parentheses").
- Campo `telemetry.intent` obrigatório: o servidor sabe por que foi chamado (boa observabilidade do lado deles).
- Escrita bloqueada pela organização, segundo a instrução do próprio servidor.

Problemas: **501 ferramentas** num só servidor é demais para seleção por agente (o próprio servidor compensa com
"skills" de domínio e recomenda `execute_code`, que roda código arbitrário). As ferramentas de escrita continuam
listadas mesmo bloqueadas. As respostas trazem e-mails de pessoas nos monitores: dado pessoal que o agente não pediu.

### 4.5 Postman

Contrato claro, mas **formato inconsistente**: `getAuthenticatedUser` devolve JSON e `getWorkspaces` devolve tabela
Markdown. `getEnabledTools` é uma boa ideia (o agente descobre o que falta). O perfil devolvido inclui e-mail pessoal e
papéis `admin`/`billing`: a conexão tem poder de administrador da equipe. Metade das 41 ferramentas escreve.

### 4.6 Miro

Funciona, mas com sinais de contrato frágil: `board_search_boards` está marcada como "Deprecated and scheduled for
removal" e continua exposta; `invocation_source` é descrito como "You must populate this field" e ao mesmo tempo é
`nullable` com padrão `null`. Campos `owner`, `modified_at`, `created_at` voltaram `null`.

### 4.7 Claude Docs, Claude Data, Meetings, pdf-viewer, scheduled-tasks

Servidores pequenos e coesos. Meetings é exemplar no tratamento de vazio: a resposta diz por que pode estar vazia e
onde mais procurar. Claude Data e Meetings marcam conteúdo de terceiros como dado, não instrução.

## 5. MCP do próprio Spock

O código-fonte do servidor MCP do Spock está neste repositório, então a análise vai além do comportamento observável:
código lido e testes do MCP executados localmente (100 de 100). Inventário completo das ferramentas em
[SPOCK_CURRENT_STATE.md §5](SPOCK_CURRENT_STATE.md#5-servidor-mcp-do-spock).

| Critério | Resultado |
| --- | --- |
| Conectividade | Aprovado (testes pelo fio: negociação de versão, JSON-RPC, BOM) |
| Autenticação | Não se aplica (stdio local); escopo de projeto obrigatório: aprovado |
| Operações de leitura | Aprovado |
| Contratos | Requer melhorias: só `spock_context_bootstrap` tem `structuredContent` e `outputSchema`; sem cursor; schema do argumento `project` declarado 4 vezes |
| Organização | Requer melhorias: nomes genéricos sem prefixo (`decisions`, `list_projects`); a tela de Agentes anuncia 5 de 12+ ferramentas |
| Segurança | Adequada, com uma falha: `iac_validate` roda `init`, baixa providers e grava histórico, mas declara `readOnlyHint` |
| Tratamento de erros | Requer melhorias: `isError` + texto em português, sem `code` estável |
| Usabilidade por agentes | Requer melhorias: respostas em prosa e tabelas Markdown que o modelo precisa interpretar |

Comparado aos servidores externos, o do Spock acerta onde eles erram (somente leitura por desenho, escopo obrigatório,
redação de segredos, resposta vazia explicada) e erra onde o Datadog acerta (saída estruturada, truncamento com
instrução de continuação, telemetria por chamada).

## 6. Problemas transversais

| Id | Problema | Onde | Impacto para agentes |
| --- | --- | --- | --- |
| MCP-T1 | Leitura e escrita no mesmo servidor, sem modo somente leitura | Atlassian, Postman, Miro, AWS, Datadog | O agente precisa se policiar; um erro de interpretação escreve em sistema real |
| MCP-T2 | Erro silencioso: consulta inválida vira resultado vazio | Atlassian (JQL) | Conclusões falsas ("não há bugs abertos") |
| MCP-T3 | Payload sem modo compacto | Atlassian | Contexto gasto com URLs de avatar; menos espaço para raciocínio |
| MCP-T4 | Paginação com quatro convenções | Atlassian | Mais instruções por ferramenta; erros de paginação |
| MCP-T5 | Ferramentas expostas para produtos que o site não tem | Atlassian (Confluence, Compass) | Escolha de ferramenta errada, chamadas desperdiçadas |
| MCP-T6 | Catálogo grande demais | Datadog (501), Postman full (234) | Seleção pior; dependência de "skills" para guiar |
| MCP-T7 | Dados pessoais sem pedido | Datadog (e-mails em monitores), Postman (perfil) | Vazamento para relatórios e memória do agente |
| MCP-T8 | Credenciais com mais poder que o uso | `gh` (`repo`, `workflow`), Jira (`write:jira-work`), Postman (`admin`) | Um agente comprometido escreve onde só precisava ler |
| MCP-T9 | Dependência sem versão fixa | `aws-mcp` (`@latest`) | Supply chain; comportamento muda entre sessões |
| MCP-T10 | Efeito colateral em leitura | Atlassian (`updateHistory`, créditos Rovo) | Custo e rastro não esperados |

## 7. Recomendações

Não alteram nenhum MCP nesta etapa; são o que o Spock deve exigir ou compensar quando consumir esses servidores.

1. **Política de leitura no lado do cliente.** Quando o Spock virar cliente MCP (hoje não é, ver
   [SPOCK_CURRENT_STATE.md](SPOCK_CURRENT_STATE.md)), ele deve classificar cada ferramenta como leitura ou escrita
   (anotação `readOnlyHint` do protocolo quando existir, nome e lista explícita quando não) e só liberar escrita com
   aprovação humana. É o mesmo princípio da política de edição que o motor já aplica a arquivos.
2. **Credenciais mínimas.** Token fino do GitHub só de leitura para análise; conta de serviço ou token do Jira sem
   `write:jira-work` para diagnóstico; perfil AWS com política `ReadOnlyAccess` (ou menor) e sem perfis de ambientes de trabalho
   para testes. Registrar no Spock qual credencial cada conexão usa e o escopo dela.
3. **Validar antes de confiar em vazio.** Para JQL, rodar a consulta com `searchResultMode=count` e tratar zero como
   suspeito quando a consulta tem campos não padrão; preferir os conectores tipados do próprio Spock para Jira, que
   validam a consulta.
4. **Projeção de campos sempre.** Pedir `fields` explícitos no Jira e `max_tokens` no Datadog; nunca a resposta padrão.
5. **Escolher um caminho AWS.** Ficar com o conector remoto (auditável por `api_calls`) e remover o `aws-mcp` local, ou
   fixar a versão do proxy (`mcp-proxy-for-aws@<versão>`) se o local for necessário. Renovar a autenticação é ação do
   usuário (`/mcp`).
6. **Autenticar só o que será usado.** Os nove plugins pendentes ampliam a superfície sem uso hoje; os três com falha
   de configuração (Gmail, Google Calendar, plugin do Datadog) deveriam ser removidos ou configurados.
7. **Redação de dados pessoais** nos relatórios que o Spock gerar a partir de MCPs de terceiros (e-mails, nomes,
   telefones), como o motor já faz com segredos.
