# Capacidades de AI Engineering do Spock

> Parte do estudo [Plataforma de engenharia com IA](README.md), 2026-11-17, sobre `cd0103c`. Avalia se o Spock tem os
> mecanismos para atuar como plataforma de engenharia assistida por IA: o que existe, o que falta e o que impede o
> Harness de ser específico por projeto. Complementa [AI_ENGINEERING_AUDIT.md](../product-audit/AI_ENGINEERING_AUDIT.md)
> (2026-11-15) com o que mudou desde então.

## 1. Resposta curta

O Spock tem **a espinha de governança** de uma plataforma de AI Engineering: execuções com diário, aprovação humana por
interrupção, ações propostas que só o motor executa, orçamento de tokens, evidência citada por id, Harness aplicado por
plano com lock e reversão, e um servidor MCP com escopo. Faltam **o conhecimento e a especialização**: agentes por papel
de engenharia, ferramentas que os agentes possam chamar, Harness que varie por framework, recuperação seletiva de
referências e cliente MCP. Hoje ele prepara um projeto para IA de forma correta e segura, mas **genérica**.

## 2. Matriz de capacidades

Estado: Implementado · Parcial · Ausente · Com falhas.

| Capacidade | Estado | O que existe | Lacuna que impede o objetivo |
| --- | --- | --- | --- |
| Agentes especializados | Parcial | Python: `planning`, `architecture`, `judge`, `investigator`. Harness: `.md` de `harness-engineer`, `code-reviewer`, `verifier` | Nenhum agente de implementação, teste, especificação ou modernização; `architecture` escolhe entre 3 áreas e 2 riscos fixos |
| Skills | Parcial | `Assets/skills/harness-contract/SKILL.md`; estrutura de especificação externa (TLC) | Uma skill própria; nenhuma por stack; sem versão por componente |
| Rules | Parcial | `general.md`, `stack-dotnet.md`, `stack-javascript.md`, mais `repo-commands` e `architecture` geradas | React, Angular, Next e Node recebem o mesmo texto; Python, Java, Go, Rust e Flutter ficam "sem perfil" |
| Hooks | Parcial | `mariliz-policy.py` (PreToolUse) e `SessionStart` → `spock context session`; desde o C3 (#139-#142) também `.codex/hooks.json` e o hook de sessão do Cursor | Política por regex de texto; o Cursor só tem o hook de sessão |
| MCP servers | Parcial | Spock como servidor (12 ferramentas + cloud), `.mcp.json` gerado pelo Harness, inventário de MCPs do projeto | Spock não é cliente MCP; só 1 ferramenta com saída tipada |
| Ferramentas para agentes | Ausente | A única ação é `record-decision`, executada pelo motor após aprovação | Nenhum agente faz tool calling; a recuperação acontece antes do agente |
| Instruções | Parcial | Prompts `_TASK` no código; regras comuns de segurança em `agents/prompts.py:25-34` (conteúdo do projeto não confiável) | Sem registro nem versão de prompt; nenhum prompt varia por stack |
| Contexto de projeto | Parcial | Context Engine (`bootstrap`), perfil de stacks, decisões, prévia de contexto; desde o W0-1 o agente recebe o trecho inteiro | `context_builder` vazio; só a intenção `bootstrap` |
| Provedores de IA | Implementado | Proxy único no motor; OpenAI, Anthropic, Google; chave no cofre; modelo por finalidade | Finalidades grossas (3); nenhum provedor local; nunca validado com chave real |
| Workflows | Parcial | `engineering.request`, `management.investigation` (LangGraph) | Engenharia linear, sem replanejamento; nenhum workflow de análise de arquitetura, modernização ou criação de projeto |
| Execuções | Implementado | 10 tipos de execução, `ai.ack.v3`, telas `/runs` e `/execution/:id` | Investigação sem pausa |
| Checkpoints | Parcial | `SqliteSaver` por projeto, `durability="exit"` | Só na pausa; queda no meio perde a execução |
| Aprovações humanas | Implementado | `interrupt()`, `APPROVAL_MISMATCH`, padrão "não executar" | Uma ação aprovável só |
| Memória | Parcial | `@decision/` e `@external/` no índice, isolamento por projeto | Nada entre projetos; nenhuma memória da organização (onde um catálogo de referência viveria) |
| RAG | Com falhas | FTS5 + símbolos + RRF, chunking por linguagem, índice incremental | Sem filtro por metadado; frontmatter ignorado; sem semântica; recall@5 0,25 |
| Evals | Parcial | 5 datasets, juiz por execução gravado em JSON, 14 checks do Harness | Recall real fora do CI; nada com modelo real; sem histórico de métricas |
| Evidências | Implementado | `chunkId` obrigatório e conferido; `context.retrieved`; auditoria | Grounding por id, não por conteúdo |
| Histórico | Parcial | Diário, linha do tempo, revisões do Harness, `mariliz.lock`/`spock.lock` | Sem métricas ao longo do tempo |
| Observabilidade | Parcial | `ai.event.v1`, uso de tokens por agente e execução | Sem OpenTelemetry, sem latência por ferramenta, custo sempre 0 |

## 3. Harness específico por projeto

O pedido exige que o Harness não seja genérico. Situação hoje:

| Elemento do Harness | Varia por stack? | Evidência |
| --- | --- | --- |
| Regras | Por família (.NET ou JS/TS) | `HarnessComposition.cs:216-254`, `RuleContent.ForStack` |
| Agentes | Não | 3 agentes fixos |
| Skills | Não | 1 skill fixa |
| Hooks | Não | Política igual para todos |
| Bloco do `CLAUDE.md` | Não (aponta para `spock context`) | `HarnessComposition` |
| Comandos do repositório | Sim (gerados da descoberta) | regra `repo-commands` |
| Arquitetura | Só a declarada pela jornada de ideia | `ArchitectureDocuments.cs` |

O que um Harness específico precisaria, usando o [catálogo](ARCHITECTURE_REFERENCE_CATALOG.md) como fonte:

| Projeto | Regras | Skill | Agentes | Hooks / checks |
| --- | --- | --- | --- | --- |
| React | Camadas `app → features → shared`, contrato Zod na fronteira, TanStack Query, `@mariliz/ui-kit` quando compatível (REF-R1..R12, §4) | Skill de frontend React com anti-padrões da §3.3 | Verificador comum + checklist React | Lint de camadas; axe |
| Angular | Standalone, `inject()`, `CanActivateFn`, signals, interceptors funcionais (REF-A1..A11) | Skill Angular com a lista "não copiar" da §5.3 | idem | `strictTemplates`; teardown de assinaturas |
| .NET | Camadas verificadas por teste, modelo de erro único, migração fora da réplica, resiliência por verbo (§6) | Skill de backend .NET | idem + especialista de integração quando houver integração | Teste de fronteira de projeto |
| Fullstack | As duas pontas + contrato gerado (§8) | As duas skills + skill de contrato | idem | Diff do contrato gerado no CI |
| Legado | As do projeto **como ele é**, mais o plano incremental (§10.8) | Skill gerada do código existente, com "inconsistência conhecida" | Agente de caracterização antes de mudar | Testes de caracterização |

Isso exige três coisas que não existem: **detecção de framework fina** (inclusive .NET), **fonte única de ids de stack** e
**geração de regra a partir de um catálogo**, não de dois arquivos fixos.

### Versionamento e rastreabilidade do Harness

Já existe e é bom: `mariliz.lock`/`spock.lock` com sha256 por arquivo e por região gerenciada, revisões do Harness,
reversão, plano com diff aprovado. Falta: versão por componente (o catálogo tem só `"version": 1` no arquivo e três fontes
de verdade divergentes) e registro de **qual referência** gerou cada regra (id `REF-*` e data da análise), para que a
atualização do catálogo mostre que regras ficaram desatualizadas.

## 4. Conhecimento arquitetural para os agentes

Avaliação dos mecanismos pedidos para disponibilizar o catálogo aos agentes:

| Mecanismo | Existe hoje? | Situação |
| --- | --- | --- |
| Contexto inicial dos agentes | Parcial | Context Engine com `bootstrap` (identidade, políticas, conexões, Harness, avisos); nenhuma seção de referências |
| Recuperação seletiva | Não | Busca única por texto; o perfil de stacks não filtra nem pondera |
| Agentic RAG | Parcial | Só na investigação (até 4 buscas); engenharia faz uma busca só |
| Indexação de Markdown | Sim | Cada `##` vira trecho com o caminho de títulos |
| Recuperação por tecnologia | Não | Sem filtro por metadado; frontmatter ignorado |
| Recuperação por tipo de projeto | Não | Idem |
| Recuperação por responsabilidade arquitetural | Não | Idem |
| Versionamento do conhecimento | Parcial | Hash e versão por documento no índice; nenhum conceito de versão de referência |
| Atualização das referências | Não | Não há fonte compartilhada entre projetos; o índice é por `projectId` |
| Evidências nas decisões | Sim | `chunkId` obrigatório no plano; decisões registradas viram `@decision/` |

Teste local (sonda temporária do diagnóstico): um catálogo Markdown indexado é encontrado por uma pergunta com
"React" e "estado" (top 1 correto), mas o agente receberia ~100 caracteres da seção e nada impediria uma seção .NET de
ser trazida para um projeto React se as palavras coincidissem. E o `search_project` do MCP descartaria um prefixo virtual
novo como `@reference/`.

O desenho proposto está em [SPOCK_ARCHITECTURE_RECOMMENDATIONS.md, R2](SPOCK_ARCHITECTURE_RECOMMENDATIONS.md#r2-conhecimento-arquitetural-recuperável).

## 5. Capacidades por cenário do pedido

| Cenário | O que o Spock faz hoje | O que falta |
| --- | --- | --- |
| Projeto existente: descobrir estrutura e tecnologias | Stacks, frameworks (exceto .NET), CI, ambientes de IA, inventário do Harness | Frameworks .NET; versões e fim de suporte |
| Compreender arquitetura e limites entre camadas | Nada | Análise de dependências entre módulos/projetos; comparação com o declarado |
| Dependências, segurança, testes | Varredura de segredos e correções (`SecurityFixPlanner`); checks do Harness | Análise de dependências e vulnerabilidades; cobertura de testes |
| Code smells e débito técnico | Nada | Métricas de tamanho, complexidade e acoplamento |
| Modernização incremental | Nada | Workflow com caracterização, plano por passos e verificação |
| Aplicar Harness e AI Engineering | Harness por família de stack, projetado para Claude Code, Cursor e Codex | Harness por framework |
| Histórico de decisões e evidências | Decisões, auditoria, linha do tempo | Ligação decisão → referência (`REF-*`) |
| Novo projeto: requisitos → arquitetura | Jornada "Da ideia ao projeto" (especificação EARS, arquitetura, backlog) | Perguntas de requisitos não funcionais; escolha de stack com critérios (§10 do catálogo) e trade-offs explícitos |
| Novo projeto: estrutura e padrões | Bootstrap IaC | Esqueleto por stack a partir do catálogo |

## 6. Avaliação contra as diretrizes de qualidade

| Diretriz | Situação | Observação |
| --- | --- | --- |
| Clean Architecture | Boa nas referências de projeto | Core anêmico; SDK MCP e I/O residual na Application |
| Segurança | Forte | Escopo, cofre, redação, filtro de segredos em processos filhos, política de edição |
| Idempotência | Forte no apply e na IaC | `interruptId` e `eventId` determinísticos |
| Resiliência | Boa nos conectores | Execução em andamento não sobrevive a queda |
| Observabilidade | Fraca | Sem telemetria técnica |
| Testabilidade | Forte | ~2.700 testes no total; intermitência sem CI |
| YAGNI / sem abstração desnecessária | Boa | O contrato morto `ai.agent.result.v1` e as finalidades sem agente são capacidade presumida |
| Contextos indiscriminados | Bom | Contexto por orçamento de tokens; o risco agora é o oposto (contexto curto demais) |
