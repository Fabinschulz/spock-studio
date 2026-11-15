# Validação real do Mariliz (Spock Studio)

Identificador da rodada: `MARILIZ-E2E-VALIDATION-2026`. Data: 2026-11-14 (UTC). Código validado: `master` em `9a46e92`,
numa cópia isolada; as alterações não commitadas da branch `feat/harness-da-arquitetura` ficaram de fora e intactas.

Toda operação abaixo foi feita contra o motor real (`Mariliz.Runtime.exe`, protocolo JSON por stdio, o mesmo que o host
Tauri usa), o runtime Python real (LangGraph do `requirements.lock`) e os serviços reais. Nada foi simulado: sem dublês,
sem respostas escritas à mão, sem dados inventados para os serviços. Cada pedido e resposta do motor está gravado em
JSONL, com segredos trocados por `<redigido>` antes de ir para o disco (conferido por busca no fim).

## Resumo

| Fluxo | Resultado |
| --- | --- |
| Projeto: criar, descobrir, perfil, persistir, recuperar | Aprovado (com lacunas de descoberta, ver A4) |
| GitHub: conexão, vínculo, sincronização completa e incremental, merge, idempotência | Aprovado |
| GitHub: erros reais (token inválido, repositório inexistente, formato, rede fora, encerramento no meio) | Aprovado |
| Indicadores, previsão, marcos, riscos, linha do tempo e auditoria sobre dados reais | Aprovado |
| Jira pelo MCP do Claude (leitura) | Aprovado |
| Jira pelo Mariliz (credencial inválida contra o site real) | Aprovado |
| Jira pelo Mariliz (ciclo completo criar, atualizar, transicionar, sincronizar) | Bloqueado |
| Workflow LangGraph sem modelo, RAG local, aprovação humana, rejeição | Aprovado |
| Aprovação humana pendente depois de reiniciar o motor | Falhou (A2) |
| RAG com issues do GitHub (política de contexto aberta) | Aprovado |
| RAG no projeto Spock | Falhou (C1); aprovado depois da correção pontual, com qualidade parcial (M1) |
| Agentes com modelo (assistente, investigação com modelo, fase A, juiz) | Bloqueado (sem chave de modelo) |
| Provedores de modelo reais recusando chave inválida | Aprovado |
| Servidor MCP do Mariliz (stdio) | Aprovado, com lacunas para o Claude Desktop (M3) |
| AWS: conectividade por SSO com código de dispositivo, só leitura | Aprovado |
| AWS pelo Mariliz | Não se aplica (não existe conector AWS) |
| Interface (Tauri): integrações, sincronização pelo botão, pedido com aprovação, auditoria | Aprovado |

## 1. Ambiente

### Ferramentas e conexões encontradas

| Recurso | Disponível | Autenticado | Identidade | Verificação real | Restrições |
| --- | --- | --- | --- | --- | --- |
| Atlassian (MCP do Claude, conector claude.ai) | Sim | Sim (OAuth) | Conta Atlassian do usuário, ativa | `atlassianUserInfo`, `getAccessibleAtlassianResources`, `getVisibleJiraProjects` (19 projetos), JQL com contagem | Escopos `read:jira-work` e `write:jira-work`; sem administração, então não cria projeto. Os 19 projetos são da empresa. O conector usa o endpoint V1, obsoleto (removido em 2027-03-01) |
| GitHub CLI | Sim | Sim (token OAuth no cofre do sistema) | `Fabinschulz` | `gh auth status`, `gh api user` | Escopos `repo`, `workflow`, `read:org`, `gist` |
| Conector GitHub do claude.ai (plugin engineering) | Não | Pede autorização (`/mcp`) | n/a | n/a | Não usado: o GitHub CLI cobre |
| AWS (conector do claude.ai) | Sim | Não: pede conexão nas configurações | n/a | `GetCallerIdentity` recusado: "requires authentication" | n/a |
| AWS (`aws-mcp` local) | Não | n/a | n/a | Falhou ao conectar (`CONNECTION_CLOSED`) | n/a |
| AWS CLI | Sim | Sim, depois do SSO por código de dispositivo (antes: sessões expiradas) | papel SSO "Dev" no perfil SSO da empresa | `sts get-caller-identity`, contagens de S3 e regiões | IAM, Bedrock e Organizations negados; ver 5.4 |
| Datadog (MCP do plugin) | Não | n/a | n/a | Falhou ao conectar (`ENOTFOUND`) | Fora do escopo |
| Chave de modelo (OpenAI, Anthropic, Gemini) | Não | n/a | n/a | Nenhuma variável e nenhuma chave no cofre do Mariliz | O Claude Desktop corporativo não expõe chave de API |
| CLIs de agente (Claude Code, Cursor, Codex, Gemini) | Não instaladas | n/a | n/a | `aitool.list` do próprio motor: `installed: false` nas quatro | n/a |
| Ferramentas locais | Sim | n/a | n/a | .NET 10, Python 3.12, Node 25, pnpm, Rust, Git 2.56 | n/a |

### Como o Mariliz se liga a cada serviço (lido no código e confirmado na execução)

- GitHub e Jira: conectores próprios do motor, **somente leitura** (`impact: read`). O GitHub lê issues e pull requests;
  o Jira lê issues com histórico de status. O Mariliz não cria branch, commit, PR nem issue: essas escritas foram feitas
  pelo GitHub CLI e pelo MCP do Jira, e a validação foi o Mariliz enxergar o resultado.
- Credencial do Jira no Mariliz: e-mail e token de API (Basic). O OAuth do MCP do Claude não é reaproveitável.
- AWS: não existe conector AWS no Mariliz (nenhuma referência no motor além do detector de segredos).
- Modelo: OpenAI, Anthropic e Google com chave própria no cofre (DPAPI no Windows).

## 2. Ambiente temporário criado

| Serviço | Recurso | Identificador | Criado em (UTC) | Estado no fim |
| --- | --- | --- | --- | --- |
| GitHub | Repositório privado | [Fabinschulz/mariliz-e2e-validation-2026](https://github.com/Fabinschulz/mariliz-e2e-validation-2026) | 21:58:02 | Mantido (ver Limpeza) |
| GitHub | Rótulo | `MARILIZ-E2E-VALIDATION-2026` | 21:58 | Mantido |
| GitHub | Milestone | #1 `E2E v0.2`, prazo 2026-11-28 | 21:58 | Aberta |
| GitHub | Issues | #1 (aberta), #2 (fechada pelo PR), #3 (fechada como "not planned"), #5 (aberta, criada para o teste pela interface) | 21:58:17 e 22:15:52 | Como descrito |
| GitHub | Branch, commit, PR | `feat/e2e-due-date-validation`, commit `4c1f9fd`, PR #4 (squash, merge `e15ce50` às 22:00:43) | 21:59 a 22:00 | PR mergeado |
| Local | Projeto de demonstração | API de tarefas em TypeScript (Fastify, Zod, Vitest), ADR e doc de arquitetura | 21:57 | Na pasta de rascunho |
| Local | Cópia do Spock | `git clone --branch master` do repositório local | 21:56 | Na pasta de rascunho |
| Local | Dados do motor | `MARILIZ_DATA_DIR` próprio na pasta de rascunho | 21:59 | Na pasta de rascunho |
| Mariliz | Conexão GitHub | `conn-0527213aec4d` (conta `Fabinschulz`) | 21:59:36 | Nos dados de rascunho |
| Mariliz | Projeto demo, vínculo | `p-1cd8604e2c6f`, `pint-65a638ca029d` | 21:59 | Nos dados de rascunho |
| Mariliz | Projeto Spock | `p-3f82b1beed03` | 22:05 | Nos dados de rascunho |
| Jira | nenhum | n/a | n/a | Nada criado (JQL `labels = MARILIZ-E2E-VALIDATION-2026` devolve 0) |
| AWS | nenhum | n/a | n/a | Nada criado |

Os dados reais do usuário (`%LOCALAPPDATA%\Mariliz`, recentes do host, cofre) não foram lidos para escrita nem alterados.

## 3. Execuções reais

### 5.1 Gerenciamento de projetos: Aprovado

Etapas: `workspace.authorize` → `project.discover` → `engineering.analyze` → vínculo de integração → novo processo do
motor → `workspace.authorize` devolve o mesmo id → perfil, conexão e vínculo recuperados do disco.

Evidências: projeto `p-1cd8604e2c6f`; descoberta encontrou Node.js com `npm run build` e `npm test` a partir do
`package.json`; depois do vínculo, `engineering.analyze` passou a listar a conexão "Repositório:
Fabinschulz/mariliz-e2e-validation-2026" e registrou a mudança no histórico do perfil.

Problemas: Fastify, Zod, Vitest e TypeScript não viram tecnologias (TypeScript só aparece como nota); o nome do projeto é
o da pasta (`demo`), não o do `package.json` (ver A4 e B2).

### 5.3 Integração com GitHub: Aprovado

| Passo | Operação real | Resultado observado no Mariliz |
| --- | --- | --- |
| Conexão | `connection.create` com o token do GitHub CLI (passado direto ao processo, nunca impresso) | `connected`, conta `Fabinschulz`, escopos lidos do cabeçalho real |
| Vínculo | `integration.link` issues e pull requests | Resolve `full_name` pela API |
| Sincronização completa | `sync-012c416fb3af` | 3 issues criadas, 1 página, 360 ms |
| Branch, commit, PR, fechar #3 | `git push`, `gh pr create`, `gh issue close` | n/a |
| Incremental | `sync-090abb948b13` | 1 novo (PR #4, "em andamento"), 1 atualizado (#3, "concluído") |
| Merge do PR | `gh pr merge 4 --squash` (o GitHub fechou a #2) | `sync-e76352e2f75f`: 2 atualizados, 1 sem mudança; PR `merged`, #2 concluída; risco "escopo crescendo" saiu sozinho |
| Idempotência | nova sincronização sem mudança | `sync-729d1eac4560`: 0 criados, 0 atualizados, total continua 4 |
| Persistência | arquivo `integrations/data/.../pint-65a638ca029d.json` | Títulos, estados, rótulos e URLs batem com a API; o token só existe no `.bin` do DPAPI (busca por `gho_`, `ghp_`, `github_pat_` em todos os arquivos: nada) |

Erros reais:

| Situação | Resposta |
| --- | --- |
| Token inválido | `INTEGRATION_AUTH_FAILED`, nenhuma conexão gravada |
| Repositório inexistente | `INTEGRATION_RESOURCE_NOT_FOUND` |
| `sem-barra` | `INTEGRATION_RESOURCE_INVALID` |
| Rede fora (proxy morto no processo do motor) | `connection.test` vira `error` com `INTEGRATION_UNAVAILABLE`; a sincronização desiste em ~15 s (`sync-333cb074790d`) e mantém os 4 itens |
| Motor encerrado no meio da sincronização | Registro fechado como `SYNC_CANCELLED` (`sync-c632b027086a`), sem registro órfão |
| Rede de volta | `connected` de novo e `sync-3a280b91295a` concluída |

Indicadores sobre os dados reais: `management.metrics`, `forecast`, `milestones`, `risks`, `evidence`, `timeline.read`
e `audit.read` responderam com fontes, contagens e motivos de "sem valor" coerentes (por exemplo, previsão indisponível
por falta de 6 semanas de histórico, marco `E2E v0.2` com prazo 2026-11-28). A auditoria registrou criação da conexão,
vínculo, cada leitura e a mudança de política, sem segredo.

### 5.2 Integração com Jira: Bloqueado (ciclo completo), Aprovado (leitura e erros)

- MCP do Claude: projetos visíveis listados e JQL executada (contagem). Não há projeto de teste: os 19 são da empresa,
  e o MCP não cria projeto (sem permissão de administração e sem ferramenta para isso). Pelas restrições da validação,
  nenhuma issue foi criada, alterada ou transicionada em projeto real.
- Mariliz: `connection.create` contra o site real com token inválido devolveu `INTEGRATION_AUTH_FAILED` (resposta real do
  Atlassian); site fora do padrão `https://<nome>.atlassian.net` devolveu `INTEGRATION_SETTINGS_INVALID`; nada foi gravado.
- Bloqueio: o conector do Mariliz exige e-mail e token de API. Para o ciclo completo faltam um projeto Jira de teste
  (ou um site gratuito separado) e um token de API criado pelo usuário em id.atlassian.com.

### 5.4 AWS

Conectividade do ambiente: Aprovado (só leitura). Integração do Mariliz: não se aplica, porque o Mariliz não tem
conector AWS e não há fluxo do produto para validar.

- Conector AWS do claude.ai: pede autenticação. `aws-mcp` local: falhou ao conectar.
- AWS CLI: os três perfis estavam com sessão expirada (`mariliz` por `aws login`; o perfil SSO da empresa e `default` por SSO).
- Alternativa usada quando o MCP não funciona: SSO do IAM Identity Center por código de dispositivo,
  `aws sso login --profile <perfil-sso> --use-device-code --no-browser`. O link e o código foram passados ao usuário, que
  aprovou no navegador; ninguém além dele digitou o código. A primeira tentativa venceu antes da aprovação
  (`invalid_grant: Invalid device code provided`); a segunda terminou com sucesso.
- O perfil SSO da empresa aponta para uma conta da empresa. Pelas regras da validação, só leitura e nenhum recurso
  criado.

| Operação (perfil SSO da empresa, região `sa-east-1`) | Resultado |
| --- | --- |
| `sts get-caller-identity` | Aprovado: papel assumido `AWSReservedSSO_Dev_…` (conjunto de permissões "Dev"), conta mascarada |
| `s3api list-buckets` (só contagem) | Aprovado: 17 buckets |
| `ec2 describe-regions` (só contagem) | Aprovado: 17 regiões habilitadas |
| `iam list-account-aliases` | Negado (`AccessDenied`): o papel não lê IAM |
| `bedrock list-foundation-models` | Negado (`AccessDeniedException`): o Bedrock não serve hoje de provedor de modelo por esta conta |
| `organizations describe-organization` | Negado, como esperado para um papel de desenvolvimento |

As negações vieram da própria AWS com o motivo ("no identity-based policy allows…"), o que confirma autenticação válida
e permissões restritas ao papel Dev.

Recomendação para o produto, se houver conector AWS: aceitar perfis do AWS CLI (SSO do IAM Identity Center com código
de dispositivo, `aws login`), guardar só o nome do perfil e nunca chaves de acesso, e começar só com leitura.

### 5.5 Agentes, RAG e workflows: parcialmente aprovado

Cadeia exercitada: protocolo → .NET → Python/LangGraph (`engineering.request.v1`) → índice SQLite → eventos → diário da
execução em disco → `run.list`/`run.detail` → auditoria.

| Execução | O que aconteceu | Prova independente |
| --- | --- | --- |
| `run-19167f0bd041`, sem modelo | Recuperou 5 trechos reais do projeto (lexical e símbolos) e terminou `completed_with_limitations` (`EMBEDDINGS_NOT_READY`, `JUDGE_NOT_RUN`) | Eventos `context.retrieved` com hash do arquivo |
| `run-819b697e771e`, impacto com aprovação | Parou em `approval.required` com uma ação `file.write`; depois do `workflow.resume` gravou `.mariliz/decisions/ex-2820d9117d3d-impacto.md` | Aprovação gravada às 22:03:08.2648; arquivo escrito às 22:03:08.2999 (depois, nunca antes); conteúdo com o `contentSha256` do diário |
| `run-16f3678d17bf`, rejeição | `workflow.resume` sem ações | O arquivo proposto não existe |
| `run-2851af2f6b2c`, aprovação pendente e motor reiniciado | Virou `cancelled`; `workflow.resume` no novo processo: `RUN_NOT_FOUND` | Falhou (A2) |
| `run-5aefa3a606df`, RAG com GitHub | Com `feedsKnowledge` ligado, os 4 itens do GitHub entraram no índice; o pedido sobre PostgreSQL trouxe a issue #1 em primeiro e a decisão registrada em segundo | Eventos com `@external/github/.../issues/1.md` |
| `inv-1fd07def2879`, investigação sem modelo | Reuniu 8 fatos reais e declarou o que não fez sem modelo | `usedModel: false`, `unavailable` explícito |
| `ask-dce27b62c8f4`, assistente sem modelo | `MODEL_NOT_CONFIGURED`, execução registrada como `failed` | `run.list` |
| `workflow.start` com `useModel` | `MODEL_NOT_CONFIGURED` antes de começar | n/a |
| `decision.record` | `dec-100f668e284c` com 22 fatos do momento | Lida de volta por `decision.list` e pelo MCP |
| `model.save` com chave inválida (Anthropic, OpenAI, Gemini) | `MODEL_AUTH_FAILED` vindo de cada API real | Chave ausente da evidência |
| `agent.run` | Bloqueado: nenhuma CLI de agente instalada | `aitool.list` |

Bloqueado: tudo o que depende de modelo (respostas do assistente, hipóteses da investigação, juiz, fase A). Nenhuma
resposta de IA foi usada como prova de operação.

### 5.6 Orquestração entre integrações: Aprovado no que não exige modelo

Fluxo real: issue criada no GitHub → sincronizada pelo Mariliz → política de contexto aberta → pedido no workflow →
RAG recupera a issue #1 e a decisão registrada → proposta de ação → aprovação humana → arquivo gravado no projeto →
diário, auditoria e linha do tempo registram. O planejamento escrito por modelo ficou bloqueado. O passo "operação
autorizada" é a escrita local aprovada; o Mariliz não tem operação de escrita em serviço externo.

## 4. Projeto Spock como contexto (cópia somente leitura do `master`)

- Descoberta: .NET `net10.0` (alta), React 19.3.0 (média), Node.js 22 (média), comandos `dotnet build/test` e
  `pnpm`/npm, CI do GitHub Actions. **Faltaram Python 3.12/LangGraph e Rust/Tauri**: o motor só tem detectores de .NET,
  Node, Flutter, CI e ambientes de IA (A4).
- Segurança: `security.scan` leu 1443 arquivos e apontou 40 achados, todos em testes e fixtures com segredos de
  exemplo.
- Índice e RAG: **0 de 5** perguntas com contexto. Causa raiz em C1. Com a correção pontual aplicada só na cópia, o índice
  recebeu os 1407 arquivos numa execução e o `context.review` voltou a responder. A recuperação ficou relevante em **3 de
  5** perguntas, com testes e fixtures dominando o topo (M1):

| Pergunta | Relevante? | O que veio |
| --- | --- | --- |
| Como o motor .NET conversa com o runtime Python? | Sim | ADR 0001 (LangGraph), auditoria agentic, teste de reinício do runtime |
| Onde fica o token do GitHub no Windows? | Não | Testes da tela de integrações; `DpapiSecretVault` ficou de fora |
| Quais ferramentas o MCP expõe? | Sim | `IdeConnection.cs`, `ide-setup.ts` |
| Como o Jira pagina e retoma depois de falha? | Não | Testes de governança; `JiraConnector` e `SyncService` ficaram de fora |
| Política de aprovação antes de gravar? | Sim | Testes de `ActionGovernance`, fixture de política |

- A pasta do Spock não foi alterada pela validação (`git status` igual antes e depois; a correção está numa cópia).

## 6.1 Mariliz como MCP do Claude Desktop

Já existe: `Mariliz.Runtime.exe mcp` (fase P) é um servidor MCP stdio, protocolo `2025-06-18`, com três ferramentas
anotadas `readOnlyHint`: `list_projects`, `project_briefing` e `decisions`. Validado de verdade por stdio:

| Caso | Resultado |
| --- | --- |
| `initialize` e `tools/list` | Respondem com instruções e esquemas |
| `list_projects` | Só os projetos autorizados no registro do Mariliz, marcando o da pasta atual |
| `project_briefing` sem `project` na pasta do demo | Tecnologias, comandos, Harness e a decisão registrada |
| `decisions` com `query=PostgreSQL` | A decisão `dec-100f668e284c` |
| Projeto não autorizado (`kaivor`) | Erro claro, sem dado |
| `limit` fora do intervalo, ferramenta inexistente | Erro de argumento e `-32602` |
| Pasta atual fora de projeto, sem `project` | Erro pedindo `project` |

Para o Claude Desktop bastaria registrar `command` e `args: ["mcp"]` em `claude_desktop_config.json` (o motor já descreve
isso em `ide.connection`). Não registrei: é configuração persistente do seu Claude Desktop e pede sua decisão.

Lacunas para servir de camada de contexto (M3):

1. Escopo: qualquer cliente que execute o binário lê **todos** os projetos autorizados pelo argumento `project`. Não há
   autenticação nem lista de projetos por cliente; para o Claude Desktop (sem pasta de projeto) isso é o caso normal.
   Proposta compatível com a arquitetura: `mcp --project <id>` (ou lista) gravado na configuração do cliente, recusando o
   resto; e um registro de auditoria por chamada (já planejado como "auditoria do que a IDE leu").
2. Conteúdo: faltam integrações vinculadas no briefing, busca no índice (RAG com citações de arquivo e trecho), linha do
   tempo e indicadores de gestão. Tudo isso já existe no motor; o servidor MCP só não expõe. A busca deve devolver
   `chunkId`, caminho e hash como evidência, e respeitar a mesma política de contexto das integrações.
3. Seleção de projeto: no Claude Desktop não há pasta atual; o primeiro passo seria `list_projects` ou um projeto fixo
   por configuração.

## 7. Interface (Tauri)

Aprovado. O app real (host Tauri e WebView2 compilados do `master` da cópia, sidecar publicado por
`scripts/build-runtime.ps1`) foi dirigido pelo CDP com cliques e digitação de verdade, sobre os mesmos dados da rodada.
Cadeia: tela → host Rust → motor .NET → GitHub / Python LangGraph → disco → tela.

| Passo na tela | Resultado | Prova |
| --- | --- | --- |
| Início | Projeto demo nos recentes, motor pronto | `ui-01-inicio.png` |
| Integrações | Conexão real (conta, escopos, "conferida hoje às 19:01") e vínculo com "Issues: 3 · Pull requests: 1" | `ui-02-integracoes.png` |
| Issue #5 criada no GitHub e botão Sincronizar | 1ª sincronização (~2 s depois) não viu a #5; a 2ª trouxe "1 novo" e a tela foi para "Issues: 4" | `ui-03-sincronizado.png`; a API de listagem do GitHub é eventualmente consistente; nada se perdeu porque o ponto de retomada segue o `updated_at` dos itens |
| Pedido de engenharia com impacto e pausa para aprovação | Execução em "Aguardando aprovação" com 5 trechos recuperados: issues #1, #5, #2, #3 do GitHub e a decisão registrada | `ui-04-aprovacao.png` |
| Marcar a ação e "Aprovar e executar as ações marcadas" | "Concluído com limitações"; `.mariliz/decisions/ex-fb64dcf3c670-impacto.md` gravado às 22:17:10 | `ui-05-aprovado.png`; a mesma tela diz "Nenhuma alteração no projeto" (M2) |
| Auditoria | "Ação de agente executada" e a rejeição anterior como "aprovadas … 0 executadas" (B1) | `ui-06-auditoria.png` |
| Fechar a janela | Host, motor e Python do venv encerrados, sem processo órfão | Lista de processos depois do fechamento |

Para o projeto aparecer na tela inicial, a entrada foi acrescentada aos recentes do host e o arquivo original foi
restaurado no fim (hash SHA-256 igual ao do backup). O empacotamento do Python embutido falhou no
`build-runtime.ps1` sem `-FetchPythonEmbed`; o app de desenvolvimento usou o Python do venv pelo `MARILIZ_PYTHON`, e o
Python da cópia tinha a correção de C1.

## 8. Problemas identificados

### Crítico

**C1. Um trecho com id repetido apaga o índice de conhecimento inteiro.** Origem:
`intelligence/knowledge/chunking.py` (`_finish`, linhas 82 a 86). O sufixo `~N` é usado para partes de um trecho longo e
para nomes repetidos; uma sobrecarga C# de um método longo gera `Compute~2` duas vezes. O `INSERT` duplicado lança
`IntegrityError`, a transação inteira volta e o motor só registra `RAG_NOT_READY`. No Spock, três arquivos disparam isso
(`ForecastCalculator.cs`, `MilestoneCalculator.cs`, `EngineSession.cs`). Impacto: RAG, revisão de contexto e agentes sem
contexto em qualquer projeto .NET médio; `context.review` responde `AI_RUNTIME_INVALID_RESPONSE`. Correção: escolher o
próximo sufixo livre (patch testado na cópia: 0 colisões no Spock e os 181 testes Python passando) e, além disso, pular o
documento problemático em vez de desfazer o lote inteiro. Patch: `evidence/fix-chunk-anchor.patch` na pasta da rodada.

### Alto

**A1. Falha do runtime Python não aparece em lugar nenhum.** Origem: `LoopbackAiRuntime.TryStartAsync` lê o stderr do
Python sem guardar nada, e `rag_research` engole o `KnowledgeError`. A causa de C1 só apareceu com um lançador de
diagnóstico externo. Recomendação: levar o stderr (já sem conteúdo do projeto, como `_log_failure` faz) ao log de
diagnóstico do host e incluir o código da falha no evento `RAG_NOT_READY`.

**A2. Aprovação humana pendente se perde ao fechar o aplicativo.** Origem: encerramento do motor cancela a execução
interrompida. `run-2851af2f6b2c` ficou `cancelled` e `workflow.resume` devolveu `RUN_NOT_FOUND`, código que diz que a
execução não existe quando ela existe. Impacto: quem fecha o app com uma aprovação aberta perde o trabalho do agente.
Recomendação: manter o checkpoint do LangGraph e o diário para retomar depois do reinício (o diário já tem `interruptId` e
`actionsHash` para conferir integridade), ou, se for decisão de produto, devolver `RUN_NOT_RESUMABLE` com o motivo.

**A3. Jira não aproveita a conexão que a pessoa já tem.** O conector exige e-mail e token de API; o OAuth do Atlassian
(o mesmo do MCP) não é aceito. Impacto: o ciclo completo do Jira ficou bloqueado nesta validação e, em empresas que
desligam tokens de API, o conector não funciona. Recomendação: OAuth 2.0 (3LO) do Atlassian como forma principal.

**A4. Descoberta tecnológica cobre poucas stacks.** Só há detectores de .NET, Node e Flutter. O próprio Spock perdeu
Python/LangGraph e Rust/Tauri; o demo perdeu Fastify, Zod, Vitest e TypeScript como tecnologia. Impacto: perfil, regras do
Harness e briefing do MCP incompletos. Recomendação: detectores de Python (`pyproject.toml`, `requirements*`), Rust
(`Cargo.toml`), Java e Go, e frameworks principais a partir das dependências do `package.json`.

### Médio

**M1. Recuperação favorece testes e fixtures.** No Spock, 2 de 5 perguntas sem o arquivo de produção certo; nomes longos de
testes em português casam com as palavras da pergunta. Recomendação: peso menor para `tests/`, `*.test.*` e `fixtures/`,
e um conjunto de avaliação de recuperação com perguntas reais do Spock.

**M2. Execução que gravou arquivo aprovado aparece como "nenhuma mudança".** `run-819b697e771e` tem a ação `executed`,
mas `changesApplied: false`, `operations: 0` e `canRevert: false`. A auditoria registra a escrita, a lista de execuções
não, e a tela da execução aprovada pela interface diz "Nenhuma alteração no projeto" logo depois de gravar o arquivo. Recomendação: contar ações executadas como mudança e permitir desfazer pelo mesmo caminho do Harness.

**M3. MCP sem escopo por cliente e com pouco contexto.** Ver 6.1.

**M4. Sem modelo, o agente grava decisão de arquitetura pobre.** A decisão aprovada diz "Áreas afetadas: Interface" para
uma mudança de validação de domínio. Recomendação: sem modelo, propor só o registro de evidências, sem classificar
áreas.

### Baixo

**B1. Rejeição registrada como aprovação.** A auditoria mostra "Ações propostas por agentes aprovadas" com "0 executadas"
quando a pessoa rejeitou tudo.

**B2. Nome do projeto vem da pasta.** O demo aparece como `demo` em vez de `e2e-tasks-api`.

**B3. Barra de estado desatualizada.** Com o workflow rodando e aguardando aprovação, o rodapé seguia dizendo "A
inteligência local está parada" (`ui-04-aprovacao.png`).

## 9. Próximas ações, por prioridade

1. Corrigir C1 (patch pronto) e tornar a sincronização do índice tolerante a um documento ruim; incluir no CI um teste com
   sobrecargas longas.
2. A1: diagnóstico do Python visível no log do host.
3. A2: retomar aprovação depois do reinício, ou erro honesto.
4. Desbloquear o Jira: projeto de teste e token de API para rodar `LiveValidationTests`, e planejar OAuth (A3).
5. Desbloquear agentes com modelo: uma chave com limite de gasto para rodar `scripts/live-validation.py` e as evals.
6. A4 e M1: detectores de stack e peso de recuperação, medidos com perguntas reais do Spock.
7. MCP: escopo por projeto, busca no índice com citações e auditoria por chamada antes de registrar no Claude Desktop.
8. AWS: decidir se o produto terá conector AWS; hoje não há.

## 10. Evidências e limpeza

- Tudo em `docs/validacao-real-2026-11-14`, na máquina de quem rodou a validação e **fora do controle de versão** (os
  registros citam identificadores da empresa e o repositório é público): o JSONL de cada cenário (`s1` a `s9`, com
  pedido, resposta, tempo em ms e eventos), as capturas da interface (`ui-01` a `ui-06`), o patch de C1
  (`fix-chunk-anchor.patch`), o log do lançador de diagnóstico do Python (`python-diag.log`) e os roteiros que geraram
  tudo (`engine.py`, `s*.py`, `cdp.mjs`). Busca final nos arquivos: nenhum token do GitHub nem chave em claro.
- Para repetir: `engine.py` sobe o motor com `MARILIZ_DATA_DIR` próprio; cada `s*.py` é um cenário; `cdp.mjs` dirige o
  app aberto com `WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS=--remote-debugging-port=9333`.
- Recursos externos ainda existentes: o repositório privado
  [Fabinschulz/mariliz-e2e-validation-2026](https://github.com/Fabinschulz/mariliz-e2e-validation-2026), criado só para
  esta validação e sem dado preexistente. Não foi apagado: apagar repositório é irreversível e fica para a sua decisão
  (`gh repo delete Fabinschulz/mariliz-e2e-validation-2026`). Nada a limpar no Jira nem na AWS.
- Nenhum dado preexistente foi alterado: os dados do Mariliz do usuário, os recentes do host, o cofre e a branch
  `feat/harness-da-arquitetura` ficaram como estavam.
