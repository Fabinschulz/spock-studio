# Remediação das pendências (2026-11-15)

Fecha a fase 1 do pedido "remediação das pendências e ecossistema de conectores cloud": levantar tudo o que ficou aberto
nas validações anteriores, cruzar com o código atual, corrigir o que é crítico e revalidar com operação real. Só depois
disso começam os conectores cloud ([CLOUD_CONNECTORS_ARCHITECTURE.md](CLOUD_CONNECTORS_ARCHITECTURE.md)).

## 1. Fontes consultadas

| Fonte | Situação |
| --- | --- |
| [MARILIZ_REAL_WORLD_VALIDATION.md](../MARILIZ_REAL_WORLD_VALIDATION.md) (validação real de 2026-11-14) | Fonte principal: achados C1, A1 a A4, M1 a M4, B1 a B3 e bloqueios |
| [docs/validacao-real.md](../validacao-real.md) | Testes ao vivo e o que cada um prova |
| [MARILIZ_CURRENT_STATE.md](MARILIZ_CURRENT_STATE.md) | Retrato de antes das fases A a G; vários "Parcial" já foram resolvidos. Usado só como lista de suspeitas, conferidas no código |
| `MCP_QUALITY_ASSESSMENT.md`, `SPOCK_CURRENT_STATE.md`, `SPOCK_AI_ENGINEERING_CAPABILITIES.md`, `SPOCK_ARCHITECTURE_RECOMMENDATIONS.md`, `SPOCK_EVOLUTION_ROADMAP.md`, `ARCHITECTURE_REFERENCE_CATALOG.md` | **Não existem** neste repositório nem em outra pasta da máquina (busca por nome em toda a Área de Trabalho). Os temas equivalentes estão em `MARILIZ_CURRENT_STATE.md`, `SPOCK_IMPLEMENTATION_ROADMAP.md`, `SPOCK_PRODUCT_EVOLUTION.md` e `SPOCK_DESIGN_DIRECTION.md` |
| Histórico do Git (PRs #90 a #93) | Correções feitas depois da validação, conferidas uma a uma abaixo |
| Gates da CI rodados na máquina (`scripts/verify.py`) | Linha de base em `master` 8c4621a |

## 2. Linha de base dos testes (master 8c4621a, antes desta remediação)

`python scripts/verify.py`: os 11 gates passaram.

| Gate | Resultado |
| --- | --- |
| Motor .NET | 1294 testes, 0 falhas, 15 ignorados; 5 testes de integração do executável |
| Inteligência | ruff limpo; 192 testes |
| Interface | tipos, lint, formatação e build ok; 89 arquivos, 808 testes |
| Host Rust | formatação e clippy ok; 45 testes |

Os 15 ignorados são os testes ao vivo (`LiveValidationTests`, `ManagementEvalLiveTests`, `IdeaLiveTests`), que só rodam
com credenciais reais. Não são falha: são dependência externa (seção 4).

## 3. Inventário das pendências

Tipos: **F** falha comprovada, **I** pendência de implementação, **A** problema arquitetural, **L** limitação de acesso,
**E** dependência externa, **N** inconclusivo.

| Id | Pendência | Tipo | Prioridade | Situação em 2026-11-15 |
| --- | --- | --- | --- | --- |
| C1 | `chunk_id` repetido apagava o índice inteiro (RAG morto em projetos .NET) | F | P0 | Corrigido no #90 e revalidado (seção 5) |
| A1 | Falha do Python invisível (stderr descartado, `KnowledgeError` engolido) | F | P1 | Corrigido no #90 (`AiRuntimeDiagnosticsTests`) |
| A2 | Aprovação pendente perdida ao reiniciar o motor; `RUN_NOT_FOUND` enganoso | F | P1 | Corrigido no #90 e revalidado |
| A3 | Jira só aceita token de API, não OAuth | A | P1 | **Bloqueado** por decisão do dono e dependência externa (seção 4) |
| A4 | Descoberta sem Python, Rust, Java, Go e frameworks | I | P1 | Corrigido no #91 e revalidado |
| M1 | Busca dominada por testes | F | P2 | Melhorado no #91 (recall@5 de 0,08 para 0,33 em `intelligence/evaluations/project_recall.py`); segue parcial, plano na seção 6 |
| M2 | Ação aprovada que gravou arquivo aparecia como "nenhuma mudança" | F | P2 | Corrigido no #90 e #93 (botão Desfazer) e revalidado |
| M3 | MCP sem escopo por cliente | A | P1 | Corrigido no #92 e revalidado |
| M4 | Sem modelo, a decisão gravada inventava áreas afetadas | F | P2 | Corrigido no #90 e revalidado |
| B1 | Rejeição registrada como aprovação na auditoria | F | P3 | Corrigido no #90 e revalidado |
| B2 | Nome do projeto vem da pasta, não do manifesto | I | P3 | Corrigido no #98 (`ProjectNameReader`); o id não muda |
| B3 | Barra de estado dizia "inteligência parada" durante uma execução | F | P3 | Corrigido no #90 (`use-engine-status.test.ts`) |
| R1 | MCP com escopo de um projeto exigia `project` quando a pasta atual não é de projeto (o caso do Claude Desktop) | F | P1 | **Novo na revalidação; corrigido nesta entrega** |
| R2 | `--project` com id que não está aberto no Spock subia calado | F | P3 | **Novo; corrigido nesta entrega** (aviso no stderr) |
| R3 | Linha só com o BOM do UTF-8 virava erro `-32700` no MCP | F | P3 | **Novo; corrigido nesta entrega** |
| R4 | `build-runtime.ps1` falhou ao empacotar a inteligência na validação | N | P2 | Não reproduzido em `master`: `package-intelligence.py bundle` gerou 26 MB de dependências (orçamento 30 MB). Encerrado como não reproduzido |
| E1 | Jira nunca validado com serviço real pelo Spock | E | P1 | Bloqueado: falta projeto de teste e token de API (o site da empresa só tem projetos de produção) |
| E2 | Agentes com modelo nunca validados com modelo real | E | P1 | Bloqueado: não há chave de API no ambiente |
| E3 | Cofre do Linux e do macOS nunca rodou em máquina real | E | P2 | Bloqueado: falta máquina; `UnixVaultRealityTests` pronto |
| E4 | MCPs do ambiente do Claude: `aws-mcp` (`CONNECTION_CLOSED`), Datadog (`ENOTFOUND`), Atlassian no endpoint V1 obsoleto | L | n/a | Fora do Spock: configuração do Claude Desktop do usuário. A AWS foi lida pelo CLI com SSO por código de dispositivo |
| D1 | Seis documentos citados no pedido não existem | I | P3 | Registrado na seção 1; os equivalentes existentes estão listados |
| D2 | `MARILIZ_CURRENT_STATE.md` desatualizado | I | P3 | Regenerado em 2026-11-15 sobre o `master` 7198c8d |

Nenhum P0 ou P1 fica aberto sem classificação: os P1 restantes (A3, E1, E2) dependem de decisão do dono ou de
credencial que não existe no ambiente.

## 4. Itens bloqueados por dependência externa

| Id | O que falta | Quem resolve | Como revalidar depois |
| --- | --- | --- | --- |
| A3 | Decisão de produto sobre OAuth 2.0 (3LO) do Atlassian e registro de um app no console de desenvolvedor (client id, URL de retorno local) | Dono do produto | Teste ao vivo do conector com OAuth num site de teste |
| E1 | Projeto Jira de teste (ou site gratuito separado) e token de API | Dono | `scripts/live-validation.py` com `MARILIZ_LIVE_JIRA_*` |
| E2 | Chave de API com limite de gasto (OpenAI, Anthropic ou Gemini) | Dono | `scripts/live-validation.py` com `MARILIZ_LIVE_*_KEY` |
| E3 | Uma máquina Linux e uma macOS | Dono | `UnixVaultRealityTests` na máquina |

## 5. Revalidação real (2026-11-15)

Mesmos roteiros da validação de 2026-11-14 (`docs/validacao-real-2026-11-14/*.py`, que ficam fora do controle de versão
por citarem identificadores da empresa), contra o motor compilado de `master` 8c4621a numa cópia isolada, com
`MARILIZ_DATA_DIR` novo e o Python/LangGraph do `requirements.lock`. **Só leitura nos serviços externos**: nenhuma
escrita no GitHub, no Jira ou na AWS; as escritas aprovadas aconteceram só na cópia local do projeto de demonstração.

| Achado | Operação real | Resultado |
| --- | --- | --- |
| C1 | Workflow e `engineering.knowledge` no próprio Spock (1425 arquivos) | Índice com 1425 documentos numa execução, sem patch; antes: 0 |
| M1 | 5 perguntas de arquitetura sobre o Spock | 3 de 5 com o arquivo certo (antes da correção: 0 de 5, e 3 de 5 com a correção pontual). Continua P2 |
| A2 | Aprovação pendente, motor encerrado, `workflow.resume` em outro processo | `awaiting_approval` depois do reinício; retomada concluída e arquivo gravado (antes: `cancelled` e `RUN_NOT_FOUND`) |
| B1 | Rejeitar todas as ações | Auditoria: "Ações propostas por agentes recusadas · nada foi executado" |
| M2 | `run.detail` da execução aprovada | `changesApplied: true`, `canRevert: true`, 1 operação |
| M4 | Decisão gravada sem modelo | "Não classificadas: sem o modelo, o Spock não sabe dizer quais áreas o pedido afeta" |
| A4 | Descoberta do Spock e do demo | Python 3.12, Rust 1.90, Tauri, Vite, LangGraph, LangChain; no demo, Fastify, TypeScript, Zod e Vitest |
| M3 | MCP com `--project` | Só o projeto do escopo aparece; outro projeto responde como inexistente |
| GitHub | Conexão, vínculo, sincronização completa, erros de token, repositório e formato | Iguais à validação anterior; 5 itens lidos |

Na revalidação do M3 apareceu o R1. Com `--project` de um só projeto e a pasta atual fora de qualquer projeto (como o
Claude Desktop inicia o servidor), `project_briefing` sem `project` respondia "a pasta atual não é de um projeto
disponível". Também apareceram o R2 e o R3.

## 6. Correções desta entrega

| Id | Mudança | Onde | Teste |
| --- | --- | --- | --- |
| R1 | Com escopo de um projeto só e a pasta atual fora de qualquer projeto do Spock, a ferramenta usa esse projeto. Numa pasta de **outro** projeto do Spock continua sendo erro, para a IDE nunca receber o contexto errado | `runtime/Mariliz.Application/Mcp/McpProjectResolver.cs` | `McpScopeTests.Fora_de_qualquer_projeto_o_escopo_de_um_so_vale_sem_o_argumento` e o teste existente da pasta de outro projeto |
| R2 | Aviso no stderr para cada id de `--project` que não está aberto no Spock; o servidor segue, porque o projeto pode ser aberto depois | `runtime/Mariliz.Runtime/McpEntry.cs` | Rodado no executável: o aviso aparece e a saída é 0 |
| R3 | Linha que só tem o BOM é ignorada como linha vazia | `runtime/Mariliz.Application/Mcp/McpServer.cs` | `McpServerTests.Linha_so_com_o_bom_e_ignorada_como_linha_vazia` |

## 7. P2 e P3 abertos: diagnóstico e plano

| Id | Diagnóstico | Plano |
| --- | --- | --- |
| M1 | A busca é lexical e por símbolos; as perguntas em português não casam com identificadores em inglês | Busca semântica (o `requirements.lock` já traz `sqlite-vec`), medida pelo mesmo `project_recall.py` antes de virar padrão |
| B2 | O nome exibido vem da pasta em `WorkspaceAuthorizer` | Ler o nome do manifesto detectado (`package.json`, `.sln`, `pyproject.toml`) só como nome de exibição, sem mudar o id |
| D2 | O retrato atual é de antes das fases A a G | Regenerar `MARILIZ_CURRENT_STATE.md` a partir do código, com a mesma legenda |
| R4 | Não reproduzido | Nada a fazer; reabrir se voltar a falhar, com o log do `package-intelligence.py` |

## 8. Critério para avançar

Os P0 e P1 estão corrigidos ou bloqueados por dependência externa com dono e forma de revalidar. A fase 2 (conectores
cloud) pode começar.
