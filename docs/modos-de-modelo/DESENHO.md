# Modos de modelo do Spock

Status: proposta. DD-MM-1 decidida em 2026-11-23 (modo Sessão primeiro); as outras decisões da seção 9 continuam com o dono.

## 1. Objetivo

O Spock precisa servir a três jeitos de trabalhar, sem que nenhum dependa do outro:

| Modo | Quem chama o modelo | Para quem |
|---|---|---|
| **Chave** | O motor, por HTTP, com a chave guardada no cofre | Quem tem chave de API (o caminho de hoje) |
| **CLI** | O motor, rodando o Claude Code CLI com o login de assinatura da pessoa | Quem usa assinatura e quer as telas do Spock com IA |
| **Sessão** | O agente que a pessoa já usa (Claude Code no terminal ou no app, ou outro cliente MCP) | Quem trabalha dentro do agente e quer o Spock como ferramenta |

Chave continua sendo o caminho completo e auditável. CLI e Sessão existem para que o Spock seja útil sem chave.

## 2. Como é hoje

### 2.1 Um ponto só de chamada ao modelo

Toda chamada passa por `ModelGateway.ChatAsync` (`runtime/Mariliz.Application/Models/ModelGateway.cs`):

- resolve provedor e modelo pela finalidade (`ModelPurposes`: `assistant`, `judge`, `workflow`) e pega a chave no cofre;
- tira segredos das mensagens (`SecretScanner`), limita `max_tokens` a 8.192 e aplica prazo de 30 s;
- chama `IModelChatTransport`, que só aceita destinos da allowlist (`ModelProviders`);
- devolve texto, modelo, tokens de entrada e saída e o hash do prompt.

Quem chama:

| Funcionalidade | Onde | Finalidade |
|---|---|---|
| Desenho de IaC com IA (só AWS) | `IacGenerationService.DraftAsync` | `workflow` |
| Arquiteto de ideias | `IdeaArchitectService` | `workflow` |
| Assistente gerencial (pergunta direta) | `ManagementAssistant` | `assistant` |
| Assistente do portfólio | `PortfolioAssistant` | `assistant` |
| Fluxos de engenharia em Python (classificar, planejar, arquitetura, reformular busca, juiz) | `WorkflowService` abre um `ModelGrant` | `workflow` |
| Investigação em etapas (Python) | `InvestigationService` abre um `ModelGrant` | `assistant` |

Os agentes Python não veem a chave: recebem um endereço em 127.0.0.1 e um token por execução (`LocalModelAccess`). O `ModelBroker` cobra o teto de tokens da execução antes e depois de cada chamada (`ModelBudgets`: 8.000 por fluxo de engenharia, 30.000 por investigação) e guarda um `ModelCallRecord` por chamada.

**Consequência para o desenho:** um modo novo entra atrás de `ModelGateway` e vale para todas as funcionalidades de uma vez, inclusive as de Python, sem mexer nelas.

### 2.2 O que já funciona sem chave

- **Pelo agente da sessão:** o servidor MCP `spock-studio` expõe `list_projects`, `project_briefing`, `search_project`, `spock_context_bootstrap`, `spock_architecture_reference`, `spock_version_check`, as ferramentas `iac_*` (inclusive `iac_blueprint_check`) e as `cloud_*` liberadas. O Harness grava regras em `.claude/rules`, skills por stack, os agentes `spock-harness-engineer`, `spock-code-reviewer` e `spock-verifier`, o hook de política, o `.mcp.json` e o bloco do `CLAUDE.md`. Quem raciocina é o agente da pessoa.
- **No próprio Spock:** análise do projeto, plano e aplicação do Harness, geração de IaC montada à mão, plano e apply, state e leituras de nuvem.
- **Com limitação:** o fluxo de engenharia sem modelo termina `completed_with_limitations` (`JUDGE_NOT_RUN`); a investigação reúne os fatos e declara o que não fez.
- **Não funciona:** assistente gerencial e do portfólio, desenho de IaC com IA e arquiteto de ideias respondem `MODEL_NOT_CONFIGURED`.

## 3. Evidência do CLI com assinatura (2026-11-23)

Teste manual, num PowerShell fora do app, com o Claude Code CLI 2.1.330 e uma pasta de configuração temporária (`CLAUDE_CONFIG_DIR`), para não tocar o login do app:

- `claude auth status --json` depois do login: `loggedIn: true`, `authMethod: claude.ai`, `subscriptionType: team`.
- `claude -p "Responda exclusivamente: SPOCK-LIVE-OK" --model haiku --output-format json`:

| Campo | Valor |
|---|---|
| `result` | `SPOCK-LIVE-OK` |
| `is_error`, `subtype`, `num_turns` | `false`, `success`, 1 |
| Modelo (`modelUsage`) | `claude-haiku-5-5`, `provider: firstParty` |
| `duration_ms` (API) | 1.038 (652) |
| `usage.input_tokens` / `output_tokens` | 2 / 15 |
| `cache_read_input_tokens` / `cache_creation_input_tokens` | 15.848 / 9.286 |
| `total_cost_usd` | 0,002 (`costBasis: list`, preço de tabela; na assinatura sai da franquia) |

Leitura:

1. A assinatura responde pelo CLI sem chave de API.
2. O JSON traz `usage` com os mesmos nomes da API: dá para contar tokens.
3. Cada chamada carrega cerca de 25 mil tokens de contexto do próprio Claude Code (prompt de sistema e ferramentas do agente). `input_tokens` sozinho (2) esconde esse custo.
4. Não há `max_tokens` por chamada nem modo JSON. O CLI é um agente, não a API de mensagens: **não é equivalente** ao modo Chave.
5. Dentro do app, o CLI embutido não herda o login do app: sem login próprio, responde `Not logged in`.

## 4. Modo Chave

Sem mudança. A validação com modelo real é o W2-4 (`scripts/live-validation.py` com `MARILIZ_LIVE_ANTHROPIC_KEY`), que continua sendo a prova do caminho de produção.

## 5. Modo CLI

### 5.1 Onde entra

`ModelProvider` ganha a forma de acesso (`ApiKey` ou `LocalCli`). Um provedor novo, `claude-code` ("Claude Code (assinatura)"), usa `LocalCli`. Para ele, `ModelGateway.ChatAsync` não busca chave: chama um `IModelCliTransport` (Infrastructure) com as mesmas mensagens já sem segredos. Finalidades, broker, `ModelGrant` e os agentes Python não mudam.

### 5.2 Como o motor roda o CLI

- **Executável:** `claude` no PATH ou um caminho que a pessoa escolhe. O Spock não procura sozinho o binário interno do app, que muda de pasta a cada atualização.
- **Argumentos fixos**, sem shell: `-p`, `--output-format json`, `--model <modelo da finalidade>`. O prompt vai por stdin, não pela linha de comando.
- **Sem ferramentas e sem projeto:** o processo roda numa pasta temporária vazia, nunca na pasta do projeto, e com as ferramentas do agente desligadas pela opção que a versão instalada oferecer. O teste de conexão confere essa opção; sem ela, o modo não liga.
- **Assinatura de verdade:** o processo roda sem `ANTHROPIC_API_KEY` e sem `ANTHROPIC_AUTH_TOKEN` no ambiente, para não cair numa chave sem a pessoa saber.
- **Prazo:** o mesmo do gateway; vencido, o processo é encerrado.
- **Resposta:** lê `result`, `is_error`, `usage` e o modelo de `modelUsage`. Saída fora desse formato vira `MODEL_INVALID_RESPONSE`, com teste de contrato sobre uma saída gravada e anonimizada.

### 5.3 Conexão

- "Conectar" lê `claude auth status --json` e guarda só `loggedIn`, `authMethod` e `subscriptionType`. E-mail, organização e ids não são guardados nem mostrados.
- "Testar" faz uma chamada curta, como o `ProbePrompt` de hoje, e mostra tempo e tokens.
- Sem login, o Spock mostra o comando que a pessoa roda (`claude auth login`). O Spock nunca faz login nem lê o arquivo de credencial do CLI.

### 5.4 Orçamento

O teto de hoje não cabe: uma chamada pelo CLI gasta cerca de 25 mil tokens, e o teto padrão do fluxo de engenharia é 8.000 por execução. Proposta:

- contar **tudo** o que o CLI informa (entrada, cache lido, cache gravado e saída), para o número na tela ser o consumo real da franquia;
- tetos próprios do modo CLI por finalidade, com padrão calculado pelo custo de partida medido no "Testar";
- reserva antes da chamada = custo de partida medido + entrada estimada + resposta pedida. Como o CLI não limita a resposta, o acerto depois usa o uso real e, se passar do teto, a execução não faz a próxima chamada.

A tela de modelo diz, ao lado do teto, que cada chamada pelo CLI inclui o contexto do próprio Claude Code.

### 5.5 Registro

`ModelCallRecord` ganha a forma de acesso (`api` ou `cli`) e os tokens de cache. Isso muda o diário de execução e o contrato de eventos: entra com fixture nova e teste de contrato.

## 6. Modo Sessão

### 6.1 Princípio

O Spock não chama modelo. Ele monta o contexto, diz o que espera e confere o resultado; o agente da pessoa raciocina. Não há custo além do que a pessoa já usa, e não há o custo de partida do modo CLI, porque o raciocínio acontece na própria sessão.

### 6.2 Padrão preparar e conferir

Cada funcionalidade que hoje pede modelo ganha duas ferramentas MCP:

1. **`<fluxo>_prepare`**: devolve o contexto (do Context Engine, com orçamento), as referências (`REF-*`), as instruções e o schema da resposta esperada.
2. **`<fluxo>_check`**: recebe o que o agente escreveu e roda as mesmas checagens determinísticas do modo Chave. Exemplos: `IacBlueprintChecks` para o desenho de IaC, as checagens do arquiteto de ideias, "toda evidência citada existe no projeto" para avaliação de arquitetura. Devolve o que passou e o que falta.

O agente repete até passar. O Spock registra o resultado como execução com origem "agente da sessão" e o nome que o cliente MCP deu de si. Esse nome não é identidade conferida, como já vale para as ferramentas de nuvem.

O desenho de IaC já está pela metade nesse padrão: `iac_blueprint_check` existe. Falta o `prepare`, com o prompt de `IacArchitectPrompts` como instrução.

### 6.3 Gravação continua com a pessoa

Nesta proposta, as ferramentas da sessão só leem e conferem. O que precisa gravar arquivo, mexer em nuvem ou registrar decisão vira proposta, que a pessoa aprova no Spock, como já acontece com a proposta do Harness (`.spock/proposal.json`).

### 6.4 Comandos prontos

O servidor MCP pode publicar prompts MCP, que o Claude Code mostra como comandos de barra, por exemplo um comando que avalia a arquitetura do projeto. Hoje o servidor só publica ferramentas. Conferir na versão em uso antes de depender disso.

Não depender de *sampling* do MCP, em que o servidor pede ao cliente que chame o modelo: o suporte nos clientes não foi conferido, e o padrão preparar e conferir não precisa dele.

## 7. Qual modo serve cada funcionalidade

| Funcionalidade | Chave | CLI | Sessão |
|---|---|---|---|
| Assistente gerencial e do portfólio (telas do Spock) | Sim | Sim | Pelo `project_briefing` e pelos fatos de gestão, com o agente respondendo |
| Investigação em etapas | Sim | Sim, com teto próprio | `prepare` com os fatos e `check` das evidências citadas |
| Fluxo de engenharia (classificar, planejar, arquitetura, juiz) | Sim | Sim, com teto próprio | O agente já faz isso; o Spock entrega contexto e confere o plano |
| Desenho de IaC com IA | Sim | Sim | `iac_architecture_prepare` + `iac_blueprint_check` |
| Arquiteto de ideias | Sim | Sim | `prepare` e `check` das etapas |
| Onda 4 (`architecture.assessment`, `modernization.plan`, `project.design`) | Sim | Sim | Sim, e é onde nasce primeiro (ver 8) |
| Evals com juiz | Sim | Não: a comparação entre execuções exige o mesmo caminho do modo Chave | Não |

## 8. Entregas propostas

Uma por PR, a partir do master, sem empilhar.

| Id | Entrega | Depende de | Pronto quando |
|---|---|---|---|
| MM-1 | Forma de acesso no provedor (`ApiKey`/`LocalCli`) e `ModelCallRecord` com forma de acesso e cache, sem mudar comportamento | - | Suítes iguais; contrato do diário com fixture nova |
| MM-2 | Padrão preparar e conferir no MCP, com o desenho de IaC como primeiro caso (`iac_architecture_prepare`) | - | Um agente monta e aprova um desenho só com as ferramentas, sem modelo no motor; teste MCP determinístico |
| MM-3 | Provedor `claude-code` pelo CLI: transporte, conexão, teste, tetos próprios e tela | MM-1 | Assistente, desenho de IaC e fluxo de engenharia rodam pelo CLI; contrato da saída JSON testado |
| MM-4 | Validação real do CLI: `live-validation.py` com um grupo novo que só roda com `MARILIZ_LIVE_CLAUDE_CLI=1`, rodado pela pessoa | MM-3 | Relatório com tokens (inclusive cache), tempo e falhas |
| MM-5 | Onda 4 nos dois caminhos: W4-1 primeiro no modo Sessão (`architecture_assessment_prepare`/`check`), depois no motor | MM-2; W2-4 só para o caminho com chave | Avaliação cita arquivos e `REF-*` que existem, nos dois modos |

Com isso, o W2-4 deixa de travar o produto: ele valida o modo Chave, e MM-2 e MM-5 andam sem chave.

## 9. Decisões do dono

| Id | Pergunta | Opções | Recomendação |
|---|---|---|---|
| DD-MM-1 | Qual modo vem primeiro | (a) Sessão (MM-2); (b) CLI (MM-1 e MM-3) | **Decidida em 2026-11-23: (a).** Maior ganho para quem usa o agente, sem custo novo e sem risco de termos de uso; o CLI vem logo depois |
| DD-MM-2 | Com chave e CLI configurados, qual vale por padrão | (a) chave; (b) CLI; (c) a pessoa escolhe por finalidade, como já escolhe o modelo | (c), com a chave como padrão inicial |
| DD-MM-3 | Como contar o orçamento no modo CLI | (a) tudo, com tetos próprios; (b) só o que não é cache | (a): o número bate com o consumo da franquia |
| DD-MM-4 | Uso da assinatura por outro programa | A pessoa confirma que os termos do plano dela permitem, ao ligar o modo | Aviso e confirmação na tela de conexão; o Spock não decide por ela |
| DD-MM-5 | A Onda 4 nasce no modo Sessão | Sim ou não | Sim: entrega valor sem chave e reaproveita as checagens no motor depois |

## 10. Riscos

| Risco | Efeito | Mitigação |
|---|---|---|
| Opções ou formato do CLI mudam numa atualização | Modo CLI quebra | Versão mínima conferida na conexão; teste de contrato da saída; erro claro em vez de resposta errada |
| Franquia dividida com o uso interativo | Consumo do Spock atrapalha o trabalho da pessoa | Tetos próprios e uso visível por execução |
| Agente da sessão não segue as instruções | Resultado fora do esperado | O `check` é determinístico e decide; nada grava sem aprovação da pessoa |
| Login do CLI vence (cerca de 30 dias no teste) | Chamadas falham | Estado da conexão com a validade lida do `auth status`; aviso antes de vencer |
| O CLI vê o projeto ou usa ferramentas | Vazamento ou ação fora do controle do Spock | Pasta temporária vazia, ferramentas desligadas e conferidas no teste |
