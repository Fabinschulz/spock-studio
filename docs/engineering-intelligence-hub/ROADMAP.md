# Roadmap de evolução do SPOCK como hub de inteligência

> Parte do estudo [Engineering Intelligence Hub](README.md) (2026-11-16). **Recomendação, não plano aprovado.** Cada
> iniciativa precisa de aprovação do dono, uma por vez, em PR próprio contra `master`, como nas fases anteriores.
> Reconcilia o roadmap da auditoria ([../product-audit/RECOMMENDED_ROADMAP.md](../product-audit/RECOMMENDED_ROADMAP.md)):
> nenhum P0 ou P1 de lá foi descartado; o §7 registra o que mudou de lugar e por quê.

## 1. Visão geral da sequência

```text
P0  Integridade (pré-requisito do hub)  H0-1 … H0-8
        │
P1  Context Foundation     F1 identidade estável → F2 Context Engine → F3 MCP v2 → F4 política por destino
        │                                   │
P1  SPOCK Connect          C1 modo bloco → C2 Claude Code → C3 Cursor + Codex → C4 diagnóstico → C5 homologação
        │
P1  Enterprise Intelligence  E1 catálogo + ferramentas de gestão → E2 Jira completo → E3 frescor
        │
P1/P2 Delivery Verification  D1 Git/GitHub tipados → D2 pacote de tarefa → D3 verificador ; D4 arquitetura existente
        │
P2  Qualidade do RAG · Copilot/Gemini · Azure DevOps · cliente MCP · metadados de banco · pacotes
P2/P3 Portfólio na IDE · Modelo C · identidade · escrita controlada
```

Maior valor por esforço, nesta ordem: **H0-1/H0-2** (fecham o risco que o hub amplifica), **F1+F2+F3+C2** (o cenário 1,
2 e 3 no Claude Code, a ferramenta que o dono já usa), **E1** (gestão na IDE com dados que já existem), **C3**, **D1-D3**.

## 2. P0: integridade

Formato compacto: cada linha tem problema, origem, situação, solução, reuso, dependências, risco, complexidade, valor e
critério de aceite.

| Id | Problema | Origem | Situação | Solução | Reutiliza | Depende | Risco | Compl. | Valor | Aceite |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| H0-1 | MCP sem vínculo expõe todos os projetos | R-11, P1-6 da auditoria | Feito (#120) | Recusar iniciar sem `--project`/`--project-dir`; erro tipado; `list_projects` só do escopo | `McpOptions`, `McpProjectResolver` | — | Quebrar configurações "Todos" já copiadas; mitigação: mensagem com o comando novo | Baixa | Alto | Teste que falha hoje: servidor sem argumento não lista projeto nenhum |
| H0-2 | Composição do MCP diverge (sem política de edição, sem histórico de riscos, state sem nuvem) | P2-10 da auditoria; MCP-2 neste estudo | Feito (#120) | Fábrica comum usada por `Program.cs` e `McpEntry.cs` | `LocalEngine` | — | Regressão no sidecar; mitigação: testes de composição | Baixa-média | Alto | Teste: edição comercial bloqueia Terraform também pelo MCP; serviços do MCP iguais aos do sidecar |
| H0-3 | Teto de tokens cortado em silêncio no Python | P0-2, R-05 | Feito: o Python usa o teto que o motor envia e recusa um acima da faixa | Remover `MAX_TOKENS` fixo; validar o teto recebido contra o intervalo do motor | `ModelBudgets`, `WorkflowRequests` | — | — | Baixa | Médio | Teste de contrato com 50.000 tokens aplicado de fato |
| H0-4 | IaC com AssumeRole usa o perfil de origem | P0-1, R-01 | Feito: o motor assume o papel e passa as credenciais temporárias só ao processo da IaC; validado em conta real em 16/11 | Credenciais temporárias do STS no motor, ou perfil efêmero com `role_arn` | `AwsCredentials` (leitura já assume) | — | Credencial em log; redação existe | Baixa-média | Alto | `sts get-caller-identity` pelo processo da IaC devolve o ARN do papel |
| H0-5 | Processos filhos herdam segredos do motor | R-10, P1-6 | Feito: a regra do terminal (nomes com cara de segredo ficam de fora) vale para todo filho do motor, com exceção nomeada por ferramenta | Filtro no `SystemProcessRunner` e no Python | Filtro do terminal | — | Ferramenta que precisava de variável herdada; lista por ferramenta | Baixa | Alto (pré-requisito de hooks e cliente MCP) | Teste: variável `SPOCK_TEST_SECRET` não chega ao filho |
| H0-6 | Decisões de identidade e licença tratadas como aprovadas; edição não passada no CI | P0-3, R-02 | Aberto | Decisões do dono; depois, CI de release com `SpockEdition` obrigatório | — | Dono | Mudar identidade sem aprovação | Baixa | Alto | Build de release falha sem edição |
| H0-7 | Nada com modelo rodou com chave real | P0-4, C-17 | Aberto | `scripts/live-validation.py` com orçamento pequeno | Testes `*LiveTests` | Chaves do dono | Custo pequeno | Baixa | Alto | Relatório com fluxos, tokens, latência e falhas |
| H0-8 | Fronteiras sem ADR | P1-1 da auditoria; este estudo | Propostas escritas | Aprovar ADR-0002 a 0008 e escrever as duas da auditoria (execução e IA) | — | Dono | — | Baixa | Médio | ADRs com status "aceita" ou "rejeitada" |

## 3. P1: Context Foundation

### F1 Identidade estável do projeto
- **Problema:** o id é o hash do caminho; clone, worktree e outra máquina viram outro projeto; configuração por caminho absoluto.
- **Origem:** §6.6 do pedido; `WorkspaceAuthorizer.cs:78-82`.
- **Capacidade:** `project.id` no `mariliz.yaml`, registro local `stableId → caminhos`, resolução por `--project-dir`.
- **Reutiliza:** `WorkspaceAuthorizer`, `FileWorkspaceRegistry`, `DeclaredConfig`, plano com diff (o id entra por plano aprovado).
- **Mudanças:** campo novo na declaração; migração que preserva os dados em `projects/<p-hash>/`.
- **Dependências:** H0-1. **Riscos:** fork com o mesmo id (aviso pelo remoto). **Complexidade:** média. **Valor:** alto.
- **Aceite:** o mesmo repositório em duas pastas resolve para o mesmo projeto, cada uma com estado Git próprio; pasta sem id recusa.
- **Métrica:** zero resoluções ambíguas nos testes de cenário.
- **Estado:** feito. `project.id: spk-<ULID>` entra no `mariliz.yaml` pelo plano (no arquivo novo ou como edição pontual
  aprovada); o id proposto fica no registro local até ser aplicado. O MCP aceita `--project spk-…` (toda cópia autorizada
  com esse id; a pasta da IDE escolhe a cópia) e `--project-dir` (sobe até o `mariliz.yaml`; sem id, recusa). O
  `p-<hash>` segue como chave dos dados de cada cópia: nada foi migrado. Ficam para depois o aviso de remoto Git
  diferente (fork), o id na tela e o comando da tela de Agentes com `--project-dir` (C2).

### F2 Context Engine e modelo canônico
- **Problema:** cada consumidor monta contexto do seu jeito; sem orçamento comum, proveniência tipada nem frescor.
- **Capacidade:** serviço `.NET` com intenções `bootstrap`, `development`, `management`, `infrastructure`, `delivery`.
- **Reutiliza:** `ProjectBriefing`, `ContextReviewService`, `ManagementService`, `DecisionMemory`, rota de busca do Python.
- **Dependências:** F1. **Riscos:** virar segundo motor; mitigação: o engine só compõe, não calcula. **Complexidade:** média.
- **Aceite:** `bootstrap` ≤ 1.500 tokens em três projetos reais; cada item com `source`, `classification`, `observedAt`, `freshness`.
- **Métrica:** tokens e latência por intenção.
- **Estado:** entrega 1 feita: `ContextEngine` (`Mariliz.Application/Context/`) com o modelo de item (origem, classificação,
  data, frescor), orçamento com `omitted[]`, `contextVersion` e a intenção `bootstrap`; `mariliz context <pasta>` na CLI.
  Medido em três pastas reais (Kaivor, o próprio SPOCK e uma worktree dele): 143 a 153 tokens, menos de 1 s com o
  processo inteiro. As demais intenções (`development`, `management`, `infrastructure`, `investigation`, `delivery`)
  entram nas próximas entregas; pedir uma delas hoje é erro `CONTEXT_INTENT_UNKNOWN`.

### F3 MCP v2
- **Problema:** saída só texto; sem bootstrap; sem proveniência; lista de ferramentas fixa; protocolo 2025-06-18 com a spec atual em 2026-07-28.
- **Capacidade:** `spock_context_bootstrap`, `structuredContent` + texto, erros tipados, `tools/list_changed`, auditoria em todas, plano de versão do protocolo.
- **Reutiliza:** `Mcp/` inteiro.
- **Decisão pendente:** manter implementação própria ou adotar o SDK oficial C# (DD-5).
- **Dependências:** H0-1, H0-2, F2. **Complexidade:** média. **Valor:** alto.
- **Aceite:** fixtures de contrato por ferramenta em `contracts/schemas/mcp/`; Claude Code e Cursor exibem a resposta (Cursor descarta resultado só estruturado: sempre mandar texto junto).
- **Estado:** DD-5 decidida (ADR-0009): o protocolo passa a ser o SDK oficial (`ModelContextProtocol.Core` 2.4.0); o
  servidor próprio fica registrado na ADR para restauração. Entrega 2: `spock_context_bootstrap` (primeira ferramenta da
  lista), com texto e `structuredContent` juntos, `outputSchema` e o contrato em
  `contracts/schemas/mcp/spock_context_bootstrap.output.schema.json`; linhas com cara de segredo saem redigidas também
  no JSON. Faltam erros tipados, `tools/list_changed` e o contrato das demais ferramentas.

### F4 Política por destino
- **Problema:** um booleano por vínculo (RT-50).
- **Capacidade:** `management`, `knowledge`, `workflow`, `assistant`, `ide` (com níveis).
- **Reutiliza:** `ContextualizationPolicy`, consumidores existentes. **Dependências:** — . **Complexidade:** baixa-média.
- **Aceite:** testes por destino; `ide: summary` como padrão; migração de `FeedsKnowledge`.

## 4. P1: SPOCK Connect

| Id | Entrega | Reutiliza | Depende | Aceite |
| --- | --- | --- | --- | --- |
| C1 | Modo `block` no plano (Markdown, JSON, TOML), escopo gravável dos adaptadores, `aiEnvironments` como lista | `PlanBuilder`, `ManagedLockFile`, `SettingsMerger`, `ManagedScope` | F1 | Conteúdo fora do bloco intacto em 100% dos testes; edição dentro vira conflito; replano sem mudança |
| C2 | Adaptador Claude Code: bloco no `CLAUDE.md`, `.mcp.json`, `SessionStart` e `UserPromptSubmit` chamando `spock context session/prompt`, lançador `spock` | Catálogo `Assets/`, hook atual, `IdeConnection` | C1, F2, F3, DD-6 | **Cenários 1, 2 e 3** num projeto real |
| C3 | Adaptadores Cursor (`.cursor/rules/spock.mdc`, `.cursor/mcp.json`, `sessionStart`) e Codex (bloco no `AGENTS.md`, `.codex/config.toml`, hooks) | C1, `HarnessInventoryReader` (já lê AGENTS.md e Codex) | C2 | **Cenários 4 e 5**; limitação "sem hook por prompt" exibida para o Cursor |
| C4 | Diagnóstico de IDE na tela; evals `EV-CTX-001..003`, `EV-CTX-SEC`; eventos `ide.bootstrap`, `ide.denied` | Auditoria, evals | C2 | Tela mostra última consulta, versão do contexto e divergências por IDE |
| C5 | Homologação multi-IDE com roteiro (como a de 16/11) | — | C3, H0-7 | **Cenário 6** e relatório com tokens e latência por IDE |

**Estado do C1:** feito. `ManagedRegion` gerencia um trecho em Markdown (marcadores `spock:begin`/`spock:end`), uma
chave própria em JSON (sem comentários, que a regravação apagaria) e uma tabela própria em TOML; formato que não dá
para editar com segurança vira conflito. O plano e o lock comparam só o trecho (modos `block`, `json-key`,
`toml-table`, campo `region` no lock), o fim de linha do arquivo da pessoa é mantido, e o escopo gravável inclui
`CLAUDE.md`, `AGENTS.md`, `.mcp.json`, `.cursor/mcp.json`, `.cursor/hooks.json`, `.cursor/rules/`, `.codex/config.toml`
e `.codex/hooks.json`. `aiEnvironments` aceita lista. Tirar só o trecho quando um componente é dispensado fica para o
C2, que é o primeiro a compor trechos.

**Estado do C2:** DD-6 decidida em 2026-11-17. Entrega 1 feita: o motor aceita `version` e `context session` (o que o
hook de sessão chama; pasta fora do Spock ou erro viram aviso com código 0) e recusa argumento desconhecido em vez de subir
como sidecar; o lançador `spock` (`desktop/launcher`, Rust, sem dependências) acha o `mariliz-runtime` ao lado ou na pasta
de cima e repassa argumentos e stdio; o instalador do Windows copia o lançador para `bin\` e põe só essa pasta no PATH do
usuário (e tira na desinstalação); no Linux o `.deb` leva `spock` para `/usr/bin` (não verificado num pacote). Entrega 2
feita: a descoberta detecta o lançador (`spock version`); o Harness do Claude Code grava o bloco no `CLAUDE.md`
(`mariliz.ide.claude-bootstrap`), a chave `spock` no `.mcp.json` (`mariliz.ide.mcp`, `spock mcp --project-dir .`) e o hook
`SessionStart` (`mariliz.hook.session`, `spock context session`) no mesmo `.claude/settings.json` do hook de política; sem o
lançador, entra só o bloco e o plano diz por quê, e numa máquina sem ele nada do que a equipe versionou é apagado;
dispensar um componente tira só o trecho dele, se ninguém o editou. Faltam: a tela de Agentes oferecer
`spock mcp --project-dir`, o hook por prompt (`context prompt`, depende da intenção `development`) e a homologação do
cenário 1 num Claude Code de verdade.

Riscos do bloco: aprovação de servidor de projeto e projeto confiável são da pessoa (o SPOCK explica, não contorna);
formatos mudam rápido (teste de conformidade por adaptador e revisão da matriz a cada entrega).

## 5. P1: Enterprise Intelligence

| Id | Entrega | Reutiliza | Depende | Aceite |
| --- | --- | --- | --- | --- |
| E1 | Catálogo de métricas versionado; `project_status`, `project_work_items`, `project_risks`, `project_milestones`, `project_changes` | `ManagementService`, `MetricSource`, `RiskRules`, `ProjectTimeline` | F3, F4 | **Cenário 3** com dados do GitHub; cada métrica com definição, fórmula, fonte, período, `syncedAt`, limitações |
| E2 | Jira de ponta a ponta validado; `description`, critérios por campo configurável, prioridade, sprint; `work_item_context` | `JiraConnector`, `SyncService` | E1, credencial de teste do dono | Ciclo completo com Jira real; critérios na IDE |
| E3 | Frescor por fonte; sincronização incremental com teto antes de responder (se DD-7 permitir) | `SyncService`, `PortfolioSignals` | E1 | **Cenário 9**: com o Jira fora, a resposta traz a data da última sincronização completa e a limitação |

## 6. P1/P2: Delivery Verification

| Id | Entrega | Origem | Depende | Aceite |
| --- | --- | --- | --- | --- |
| D1 | Git tipado (diff, log, branch, worktrees) e GitHub (PR com arquivos, reviews, checks), só leitura; `git_changes`, `pull_request_status` | P1-3 | H0-5 | Testes com repositório real |
| D2 | Pacote de tarefa a partir de item sincronizado; classificação por `issuetype` | P1-4 | E2 | Pedido com origem e critérios gravados |
| D3 | Verificador determinístico, execução `delivery`, `delivery_check` no MCP, regra de risco "entrega com pendência" | P1-2 | D1, D2 | **Cenário 10**: critério não atendido vira pendência com a evidência que falta |
| D4 | Detector de arquitetura existente; `project_architecture`, `project_rules` | P1-5 | F2 | Reconhece o próprio SPOCK e duas referências de stacks diferentes |

## 7. P2 e P2/P3

| Id | Iniciativa | Origem | Observação |
| --- | --- | --- | --- |
| Q1 | Qualidade do RAG: dicionário pt/en, reformulação no fluxo de engenharia, arquivo inteiro, orçamento de indexação | P2-3, C-08 | Meta Recall@5 ≥ 0,6; embeddings (D3) só depois, com medição |
| A1 | Adaptadores Copilot e Gemini | Este estudo | Gemini depois de confirmar o produto usado (aviso do Antigravity) |
| A2 | Pacotes de usuário (plugin/extensão) para Claude Code, Codex, Cursor, Gemini | §11.4 do pedido | Sem marketplace próprio |
| I1 | Conector Azure DevOps (work items, critérios nativos, iterações) | §7.4 do pedido | Depois de E2 validado |
| I2 | Cliente MCP com classificação, permissão por ferramenta e auditoria | P3-5 da auditoria, antecipado | Depende de H0-5 |
| I3 | Metadados de banco (nível 2) e contexto de migrações (nível 1) | §7.3 do pedido | Nível 3 só com decisão |
| G1 | `confirmCommands` declarados (C-18), regras por stack (P2-6), leitura de tsconfig/eslint (C-24) | Homologação, auditoria | — |
| G2 | Itens P2 da auditoria mantidos: docs de decisão (P2-1), modelo por etapa (P2-2), contratos (P2-5), retomada após queda (P2-8), IaC nas execuções (P2-9) | Auditoria | Sem mudança |
| G3 | P1-7 supply chain e validação Azure/GCP; P1-8 onboarding e gestão de projetos | Auditoria | Mantidos; fora do caminho crítico do hub |
| M1 | `portfolio_overview` na IDE | §8.4 do pedido | **Cenário 11** com dados reais |
| M2 | Modelo C: `spock-org.yaml` com política e portfólio | [DEPLOYMENT_MODELS.md](DEPLOYMENT_MODELS.md) | Depende de DD-1 |
| M3 | Identidade de pessoa e papéis | DD-2 | **Cenário 12** completo só com isto |
| W1 | Escrita controlada em Jira, GitHub, Azure DevOps | P3-3 da auditoria | Depois de D3 |

## 8. Reconciliação com o roadmap da auditoria

| Item anterior | Agora | Motivo |
| --- | --- | --- |
| P0-1, P0-2, P0-3, P0-4 | H0-4, H0-3, H0-6, H0-7 | Mantidos |
| P1-1 ADRs de execução e IA | H0-8, junto com as ADRs 0002-0008 | Mesmo pacote de decisões |
| P1-2 elo de entrega | D3 | Mantido; agora alimenta também a IDE |
| P1-3, P1-4, P1-5 | D1, D2 (+E2), D4 | Mantidos |
| P1-6 (MCP sem projeto, ambiente) | H0-1, H0-5 (promovidos a P0) | O hub amplia exatamente esses dois riscos |
| P2-4 política por destino | F4 (promovido a P1) | A IDE é um destino novo e o de maior risco |
| P2-10 composição única | H0-2 (promovido a P0) | Deixou de ser cosmético: divergência com efeito de licença |
| P3-5 cliente MCP e plugins | I2, A2 (P2) | O pedido torna o "configurar uma vez" requisito; continua depois do Connect |
| P3-1 a P3-4, P3-6 a P3-8 | Sem mudança | — |

Nada foi descartado. Nenhuma iniciativa P0 ou P1 anterior saiu do caminho.

## 9. Cenários de aceite

| # | Cenário | Entregas | Critério objetivo |
| --- | --- | --- | --- |
| 1 | Nova sessão no Claude Code | F1, F2, F3, C2 | Sessão nova recebe o bloco e o resumo do hook; `ide.bootstrap` registrado com `contextVersion` igual ao do lock |
| 2 | Novo prompt sobre arquitetura | C2, D4, Q1 | A resposta cita trechos de `search_project`/`project_architecture` com caminho e `indexedAt`; vale também numa sessão nova sem histórico |
| 3 | Consulta gerencial no Claude Code | E1 | Números iguais aos da tela, com id da métrica, fonte e `syncedAt` |
| 4 | Nova sessão no Cursor | C3 | Contexto equivalente sem nenhum arquivo do Claude Code no projeto |
| 5 | Nova sessão no Codex | C3 | Idem, com `AGENTS.md`, `.agents/skills` e MCP do Codex |
| 6 | Regra alterada no SPOCK | C1, C5 | `contextVersion` muda; plano com diff para cada IDE; nada gravado sem aprovação; replano limpo |
| 7 | Nova conexão (Azure DevOps ou MCP de banco) | I1, I2, F4 | Catálogo atualizado; a IDE vê só as ferramentas permitidas; o resto ausente de `tools/list` |
| 8 | Projeto não autorizado | H0-1, F1 | `PROJECT_NOT_BOUND`/mesma resposta de inexistente; nenhum dado vaza |
| 9 | Jira indisponível | E3 | Resposta com a data da última sincronização completa e a limitação; nunca "atual" |
| 10 | Entrega concluída | D3 | Pendências com a evidência que falta; entrega completa fecha sem pendência |
| 11 | Portfólio pelo gestor | M1 | Projetos atrasados e em risco com dados reais e fonte |
| 12 | Públicos diferentes | F4, M3 | Modelo A: cada um só vê o que a credencial e os vínculos dele permitem; papéis dentro da mesma credencial exigem M3 |
