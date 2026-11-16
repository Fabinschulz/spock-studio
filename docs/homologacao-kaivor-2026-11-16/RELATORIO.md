# Homologação do Spock no Kaivor (2026-11-15/16)

O Spock Studio foi usado para montar o Harness de engenharia de IA do Kaivor e, ao mesmo tempo, posto à prova como
produto. O Kaivor é o clone local de um projeto de terceiro, usado só como ambiente de validação. Tudo o que foi
gravado nele está na branch local `spock/homologacao-harness`, sem commit nem push. As correções do Spock estão na
branch `fix/homologacao-kaivor`, também sem commit.

Este relatório cobre:

- **B.** A validação do Spock.
- **C.** As pendências do Spock.
- **Correções feitas.** O que foi corrigido no Spock durante a homologação.

As entregas no Kaivor (A) e as pendências do Kaivor (D) estão em
`kaivor/docs/reports/spock-ai-harness-homologation-2026-11-16.md`.

## Como foi executado

- **Interface:**
  - O Spock foi operado pelo app desktop, em build de desenvolvimento do master com `tauri dev`.
  - A janela foi conduzida pelo CDP do WebView2: cliques, digitação e leitura da tela.
  - O diálogo nativo de pasta foi conduzido por automação de interface do Windows.
  - Nenhuma tela foi substituída por chamada direta ao motor.
- **Servidor MCP:** foi testado como uma IDE o usaria, com um cliente MCP mínimo por stdio:
  - executável: `target\debug\mariliz-runtime.exe mcp --project <id>`, exatamente como a tela Agentes de IA indica;
  - operações: `initialize`, `tools/list` e `tools/call`.
- **Agentes do Spock:** `harness-engineer`, `code-reviewer` e `verifier` rodaram no Claude Code. Cada um rodou como
  agente independente, só com a própria definição gravada no Kaivor, as regras de `.claude/rules/` e o `AGENTS.md`.
  - **Por que fora do Spock:** o Spock não executa esses agentes. Ele os escreve, e quem os roda é o ambiente de IA.
  - **Por que como subagentes:** a tela Agentes de IA não encontrou a CLI `claude` neste computador (ver C-10).
- **Pasta de dados:**
  - **Problema:** processos abertos a partir do app Claude (MSIX) veem o `%LOCALAPPDATA%` virtualizado; escrever ali
    vai para `Packages\Claude_...\LocalCache`.
  - **Solução adotada:** usei `MARILIZ_DATA_DIR` numa pasta própria, com cópia de `workspaces.json`.
- **Sem chave de modelo:** não havia chave de modelo em Minha IA, e credenciais não passam pelo chat. Tudo o que depende de
  modelo ficou NÃO VALIDADO.

## Resultado dos testes (B)

### Placar

Foram 26 capacidades avaliadas por operação real. A coluna "Inicial" é o resultado com o master como estava; a coluna
"Final" é o resultado depois das correções desta branch.

| Classificação | Inicial | Final |
| --- | --- | --- |
| APROVADO | 10 | 18 |
| PARCIAL | 8 | 3 |
| REPROVADO | 3 | 0 |
| NÃO VALIDADO | 5 | 5 |

### Capacidades

| # | Capacidade | Operação real | Inicial | Final | Evidência |
| --- | --- | --- | --- | --- | --- |
| 1 | Descoberta do projeto | Visão geral e "Analisar projeto" no Kaivor | APROVADO | APROVADO | Electron 42, React 19, Vite 8, TS 6, .NET 8 e Node; build, test e lint; GitHub Actions |
| 2 | Inventário do Harness existente | "Ver o que existe" | PARCIAL | APROVADO | Antes só via `.claude/`. Ignorava o `AGENTS.md`, a skill duplicada em `.agents/skills` e o MCP `context7` do Codex (C-01) |
| 3 | Perfil tecnológico | Visão geral; `project_briefing` pelo MCP | PARCIAL | PARCIAL | Node 20.19 vem de `engines`, mas o `.nvmrc` diz 22; o React aparece duas vezes; o perfil gravado não atualiza sozinho (C-12) |
| 4 | Plano pelas regras-base | Configurar, etapa Plano | PARCIAL | APROVADO | A regra geral, o verificador e o Harness Engineer impunham `.specs/` e `tlc-spec-driven`, contra o SDD do projeto (C-02); comandos de subpasta saíam sem pasta (C-05) |
| 5 | Proposta do Harness Engineer | Agente escreveu `.mariliz/proposal.json`; motor validou | PARCIAL | APROVADO | 18 decisões, 7 regras do projeto, 17 observações validadas. O `keep` tirava componentes do lock (C-04) |
| 6 | Aplicação com aprovação e diff | Revisão e Aplicação | APROVADO | APROVADO | 12 arquivos criados, 4 arquivos do projeto mantidos; o comando de rede ficou desmarcado por padrão |
| 7 | Validação do Harness (EV-*) | Resultado e "Validar de novo" | PARCIAL | APROVADO | Final 13 de 14, com o juiz não executado. Antes, o EV-SPEC-001 ficava bloqueado para sempre (C-03) |
| 8 | Idempotência | Replanejar depois de aplicar | REPROVADO | APROVADO | Antes, o lock sempre pendente (C-03, C-04). Depois, "Nenhum arquivo precisa mudar · 23 preservados" |
| 9 | Reversão | não executada | NÃO VALIDADO | NÃO VALIDADO | Coberta só por testes do repositório |
| 10 | Governança (hook) | 10 pedidos reais de ferramenta ao hook instalado | APROVADO | APROVADO | Nega `rm -rf /`, `curl\|sh`, force push em main, escrita de `.env` e escrita fora do projeto; pede confirmação para `cat .env` e arquivos do Harness; libera código e testes |
| 11 | Context Engineering (regras de conhecimento) | `knowledge.exclude` em `mariliz.yaml`; tela Conhecimento | APROVADO | APROVADO | 148 arquivos excluídos pela declaração; 23 a 29 barrados por possível segredo |
| 12 | RAG: índice e recuperação | "Revisar contexto", Pedido de engenharia, `search_project` | REPROVADO | PARCIAL | Antes, o índice tinha 510 `.md` e 3 arquivos de código, e o arquivo-alvo ficava fora do limite (C-06). Depois converge em 5 rodadas e acha o símbolo com `arquivo:linha`. Perguntas conceituais seguem fracas (C-08) |
| 13 | Servidor MCP do Spock | Cliente MCP stdio: 11 ferramentas; `list_projects`, `project_briefing`, `decisions`, `search_project` | APROVADO | APROVADO | Respostas reais. Cada leitura entra na auditoria com o nome do cliente |
| 14 | Registro do MCP na IDE | `claude mcp add` | NÃO VALIDADO | NÃO VALIDADO | Não há CLI no PATH. A tela só oferece escopo de usuário (C-09) |
| 15 | Workflow (Pedido de engenharia, sem modelo) | Iniciar pedido | REPROVADO | APROVADO | Antes, `LIMIT_TIME` em 4 s (C-07). Depois, "Concluído com limitações": 4 etapas, impacto e 5 evidências |
| 16 | Workflows com modelo, juiz e investigação | - | NÃO VALIDADO | NÃO VALIDADO | Sem chave de modelo |
| 17 | Agentes de IA (`agent.run` por CLI) | Tela Agentes de IA | NÃO VALIDADO | NÃO VALIDADO | O Spock diz corretamente que não há CLI instalada. O Claude Code embutido no app desktop não é reconhecido (C-10) |
| 18 | Skills | `harness-contract` lida pelos 4 agentes | APROVADO | APROVADO | Os 4 relatórios abrem com o bloco de contrato (confidence, evidence, assumptions) |
| 19 | Agentes especializados | Harness Engineer, revisor e verificador | APROVADO | APROVADO | O revisor pediu mudanças (gate de build ausente) com prova por mutação; o verificador deu PASS, com 9 de 9 mutantes detectados |
| 20 | Orquestração entre agentes | Ciclo completo no Kaivor | PARCIAL | PARCIAL | O Spock define papéis e escreve os agentes, mas não coordena o ciclo nem tem agente de implementação (C-11) |
| 21 | Memória (decisões) | Registrar decisão; `decisions` pelo MCP | APROVADO | APROVADO | A decisão chega ao agente marcada como registro, nunca como instrução |
| 22 | Observabilidade | Execuções, Linha do tempo, Auditoria | PARCIAL | APROVADO | A linha do tempo dizia "0 de 3 verificações passaram" para limitações, que não são verificações (C-14) |
| 23 | Persistência entre sessões | Fechar e reabrir o app | APROVADO | APROVADO | Harness, 2 revisões, 4 execuções, a decisão e o índice continuaram lá |
| 24 | Integrações GitHub e Jira | - | NÃO VALIDADO | NÃO VALIDADO | Repositório de terceiro e sem credenciais do dono |
| 25 | Segredos | Scanner do índice; `deny` de `.env` no `settings.json` | APROVADO | APROVADO | Nenhum valor exposto; arquivos com padrão de segredo ficam fora do índice |
| 26 | Reabrir o último projeto | Reinício do app | PARCIAL | APROVADO | Falhava quando a interface pedia antes de o motor existir (C-13) |

## Ciclo de desenvolvimento validado

- **Atividade:** endurecer `dbGridVisibleRowRange` em `src/shared/db-grid-go-to-row.cjs`. Uma janela virtual inválida
  devolvia uma faixa invertida (`{first:5001,last:5000}`).
- **Etapas:**
  1. **Pedido de engenharia no Spock:** contexto e impacto, sem modelo.
  2. **Busca no índice pelo MCP:** achou a função com `arquivo:linha`.
  3. **Desenvolvedor:**
     - carregou o `AGENTS.md` e as 11 regras;
     - achou a spec dona do código e escreveu os testes antes, vendo-os falhar;
     - corrigiu o código e acrescentou o critério 13 à spec.
  4. **Revisor:** pediu mudanças, porque o gate `npm run build` do `AGENTS.md` não tinha rodado. Também apontou que o
     exemplo da spec exagerava o alcance do defeito.
  5. **Verificador:** deu PASS, com build com código 0 e 9 de 9 mutantes detectados. Apontou que o teste não estava em
     `test:unit` e por isso não rodava na CI.
  6. **Correção pelo desenvolvedor:** aplicou os 6 achados.
  7. **Conferência final:** 6 de 6 testes.
- **O que o Spock fez nesse ciclo:** forneceu o contexto (pedido, índice e MCP), as regras, os agentes e a memória. A
  coordenação entre os agentes foi feita pela sessão do Claude Code.

## Correções feitas no Spock (branch `fix/homologacao-kaivor`)

| Id | Prioridade | Problema | Correção | Teste |
| --- | --- | --- | --- | --- |
| C-01 | P1 | O inventário ignorava `AGENTS.md`, `.agents/skills` e os servidores MCP do Codex | `HarnessInventoryReader` lê esses três. Segredo no MCP do Codex vira aviso, e o valor nunca sai | `DiscoveryTests.Inventario_inclui_agents_md_...` |
| C-02 | P1 | A regra geral, o verificador e o Harness Engineer impunham o ciclo `.specs/`, mesmo com a estrutura de especificação recusada | Textos condicionais: o projeto segue o próprio processo e o `AGENTS.md` vence em conflito. O verificador só grava `validation.md` com a estrutura instalada | Ficha e catálogo (`HarnessCatalogTests`) |
| C-03 | P1 | A estrutura de especificação não podia ser dispensada; o EV-SPEC-001 e a idempotência nunca passavam | `components.exclude` escrito pela pessoa em `mariliz.yaml` dispensa a estrutura: o plano não instala e a validação passa com essa evidência. A tela continua sem oferecer isso, como fixa o teste do dono | `ConfigureJourneyTests.Especificacao_dispensada_...` |
| C-04 | P1 | Um `keep` da proposta do Harness Engineer tirava do `mariliz.lock` componentes já gerenciados | `ComponentSelector` trata `keep` de um componente do lock como desejado, com o mesmo conteúdo | `ConfigureJourneyTests.Proposta_que_mantem_...` |
| C-05 | P2 | Comandos de pacote em subpasta saíam sem pasta (três `npm test` iguais) | `NodeDetector` gera `npm --prefix`, `pnpm --dir` e `yarn`/`bun --cwd` | `DiscoveryTests.Comando_de_pacote_fora_da_raiz_...` |
| C-06 | P1 | O índice punha `.cjs`/`.mjs` no fim da fila e testes antes do código, e o limite de 2000 cortava o código-fonte | `.cjs/.mjs/.cts/.mts` entram com o código; testes vão depois do código do mesmo tipo | `KnowledgeSourcesTests` (2 testes novos) |
| C-07 | P1 | Pedido de engenharia sem modelo falhava com `LIMIT_TIME`: o motor pede 10 s, o runtime cortava em 2 s | Teto do runtime sem modelo igual ao do motor (10 s) | `test_budget` |
| C-13 | P2 | O último projeto não reabria quando a interface pedia antes de o motor existir | O host espera o motor sair de "starting" (até 20 s) antes de autorizar | `engine.rs` `autorizar_espera_o_motor_subir_...` |
| C-14 | P3 | A linha do tempo contava limitações como verificações ("0 de 3 passaram") | Limitações não contam; as não executadas aparecem à parte | `ProjectTimelineTests.Limitacoes_de_um_pedido_...` |
| C-15 | P3 | Marca antiga ("Mariliz Studio", "Mariliz policy") nas regras e no hook | "Spock Studio" / "Spock policy" | `AgentPolicyTests` |
| C-16 | P3 | O pedido ao Harness Engineer e o revisor não citavam `AGENTS.md` | Pedido e agente atualizados | - |

Gates locais (`scripts/verify.py`, rodados com o app fechado): 11 de 11 ok.

- **Motor:** 1479 testes .NET aprovados, 27 ignorados.
- **Inteligência:** ruff limpo e 192 testes Python.
- **Interface:** tipos, lint, formatação, 845 testes e build.
- **Host:** formatação, clippy e testes.

## Pendências do Spock (C)

| Id | Prioridade | Funcionalidade | Observado | Impacto | Causa provável | Recomendação | Resolvido quando |
| --- | --- | --- | --- | --- | --- | --- | --- |
| C-08 | P1 | RAG | O índice converge 1,5 milhão de caracteres por rodada (5 rodadas no Kaivor); limite fixo de 2000 arquivos (186 ficaram fora mesmo com exclusões); busca conceitual em português fraca, sem embeddings | Em projeto grande, o agente não acha o código até várias execuções | Orçamento pensado para execução de workflow, não para indexação | Ação "Indexar tudo" em segundo plano; limite configurável em `mariliz.yaml`; embeddings atrás de avaliação | Projeto de 2.400 arquivos pesquisável depois de 1 ação, com a meta de recall da avaliação |
| C-11 | P1 | Agentes e orquestração | O catálogo tem só harness-engineer, revisor e verificador; os agentes citam "the implementation agent of the project", que não existe; nada coordena o ciclo pedido → código → revisão → verificação | O ciclo depende de quem opera o ambiente de IA | Escopo da fase | Agente de implementação (e, se fizer falta, de planejamento e diagnóstico) e um fluxo que passe o resultado de um ao outro com estado | Um pedido roda o ciclo completo com os agentes do catálogo e deixa o diário de cada etapa |
| C-17 | P1 | Modelo real | Nenhum fluxo com modelo foi validado nesta homologação (sem chave) | Juiz, investigação e "Da ideia ao projeto" seguem sem prova real | - | Rodar `scripts/live-validation.py` com as chaves do dono | Relatório real com custo e resultado |
| C-09 | P2 | Ligar o Spock à IDE | A tela só oferece `claude mcp add --scope user` (CLI); não há escopo de projeto (`.mcp.json`) nem repasse de `MARILIZ_DATA_DIR`; a jornada Configurar não instala o MCP | Projeto sem a CLI não liga o Spock à IDE | - | Opção "neste projeto" que proponha `.mcp.json` no plano, com diff e aprovação | `.mcp.json` aplicado e validado por um EV |
| C-10 | P2 | Agentes de IA | Não reconhece o Claude Code embutido no app desktop | "Pedir ao agente" fica indisponível | Detecção só pela CLI no PATH | Detectar a instalação do app ou explicar como instalar a CLI | A tela encontra o Claude Code do app |
| C-18 | P2 | Governança | A política do hook é fixa; o Kaivor precisava pedir confirmação para `git push origin main` (dispara deploy de produção), e a proposta não pode tocar em hooks | Regra crítica do projeto só existe como texto | Escolha de segurança: o agente não propõe hooks | Lista de comandos que pedem confirmação declarada pela pessoa em `mariliz.yaml` | `git push origin main` pede confirmação no Kaivor |
| C-19 | P2 | Dados | Aberto a partir de um app MSIX, o `%LOCALAPPDATA%` é virtualizado, e o Spock não avisa | Dados "somem" para o app aberto fora do Claude | Ambiente | Aviso no diagnóstico quando o caminho real difere | Diagnóstico mostra o aviso |
| C-20 | P2 | Testes | `WorkflowRuntimeTests.Runtime_real_responde_dentro_dos_schemas_v2` falha sob carga e passa isolado; `cargo clippy` falha com o app aberto (binário preso) | CI local instável | Prazos curtos; arquivo travado | Prazo do teste vindo de constante; `verify.py` avisar sobre o app aberto | Três rodadas seguidas verdes sob carga |
| C-12 | P3 | Perfil | Node pela faixa de `engines`, não pelo `.nvmrc`; React listado duas vezes; perfil e briefing do MCP antigos até "Atualizar perfil" | Informação menos precisa ao agente | - | Ler `.nvmrc`/`.node-version`; agrupar; refazer o perfil depois de aplicar o Harness | Perfil igual ao do repositório sem ação manual |
| C-21 | P3 | Configurar | A etapa 2 não pré-seleciona o ambiente declarado; diz que a estrutura "será instalada" mesmo dispensada; a revisão cita "validações da especificação" | Ruído para a pessoa | - | Ler a declaração na tela | Telas coerentes com `mariliz.yaml` |
| C-22 | P3 | Marca | Cabeçalhos de `mariliz.yaml`/`mariliz.lock` e o texto do Harness Engineer ainda dizem "Mariliz" | Marca inconsistente no projeto do cliente | Renomeação parcial | Trocar os textos e regenerar as fixtures | Nenhum "Mariliz" visível nos arquivos gravados |
| C-23 | P3 | Observabilidade | "Revisar contexto" indexa, mas não aparece em Execuções | A indexação não é rastreável | - | Registrar a indexação como execução leve | Rodada de indexação na central |
| C-24 | P3 | Regras de stack | `stack-javascript` pede strict e named exports, o que conflita com o Kaivor (o Harness Engineer apontou) | Regra genérica contra a realidade do projeto | - | Ler `tsconfig`/eslint antes de afirmar o padrão | A regra não contradiz a configuração do projeto |

## Parecer

- **O Spock configurou o Harness do Kaivor?** Sim, depois das correções C-01 a C-04: resultado "Concluído", com 13
  de 14 verificações e idempotente.
- **Operou de ponta a ponta?** Na parte determinística, sim:
  - descoberta, plano, proposta, aplicação, validação e revisões;
  - índice e MCP;
  - memória, auditoria e persistência.

  A parte com modelo não foi provada.
- **Os agentes trabalharam coordenados?** Cada um cumpriu o próprio papel com evidência, e o revisor e o verificador
  pegaram lacunas reais. A coordenação não é do Spock.
- **O conhecimento foi usado corretamente?** Depois de C-06 e de 5 rodadas de indexação, sim; antes, não.
- **Ferramentas e integrações funcionaram?** O MCP do Spock e o hook, sim. GitHub e Jira, a IDE e a CLI de agentes
  não foram validados.
- **O ambiente está pronto para desenvolvimento assistido por IA?** Sim, para Claude Code com supervisão. Faltam C-08,
  C-09, C-11 e C-18 para autonomia.
- **Maturidade:** intermediária.
  - A fundação determinística é sólida: aprovação, diff, lock, validação, auditoria e segredos.
  - Defeitos de integração só aparecem em projeto real: 7 P1 encontrados, todos corrigidos aqui.
  - As capacidades que dependem de modelo e de orquestração ainda não foram provadas.
