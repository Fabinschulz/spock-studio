# Modelo canônico do Harness

> Parte do estudo [Engineering Intelligence Hub](README.md) (2026-11-16). Parte V do pedido. ADR:
> [0004](../architecture/adr/0004-adaptadores-multi-ide.md).

## 1. O que existe

O Harness já tem quase tudo de um modelo canônico, mas preso a uma IDE:

| Elemento | Hoje | Amarrado ao Claude Code? |
| --- | --- | --- |
| Declaração | `mariliz.yaml` (`project.name`, `aiEnvironment`, `components`, `evals`, `knowledge`) | `aiEnvironment` aceita um valor só |
| Catálogo de componentes | `Assets/catalog/components.json`, agentes, regras, skill, hook | Sim: os caminhos de destino são `.claude/…` |
| Lock | `mariliz.lock` com caminho, componente, modo e sha256 | Não (genérico) |
| Plano, aplicação, reversão | `PlanBuilder`, `ApplyExecution`, `RevertService` | Não (genérico) |
| Escopo gravável | `.claude/`, `.mariliz/`, `mariliz.yaml`, `mariliz.lock` | Sim |
| Política dos agentes | `mariliz-policy.py` (PreToolUse), lida para exibição | Sim |
| Evals do Harness | 14 no catálogo | Várias conferem caminhos do Claude Code |
| Proposta do Harness Engineer | `.mariliz/proposal.json`, dado não confiável | Não |
| Modelos por finalidade e teto | `model.purpose`, `model.budget` | Não |

A separação certa já está feita entre **o que** (componente) e **como gravar** (plano). Falta separar **o que** de
**onde, para cada IDE**.

## 2. Modelo proposto

```yaml
# mariliz.yaml (trecho proposto; o que já existe continua igual)
project:
  id: spk-01HZX…              # novo (ADR-0007)
  name: orders-api
aiEnvironments: [claude-code, cursor, codex]   # novo: lista; aiEnvironment (singular) continua aceito
components:
  include: [spock.bootstrap, spock.mcp, spock.rules.general, spock.rules.stack, spock.agents.reviewer, …]
  exclude: [spock.spec-structure]
policy:
  confirmCommands: ["git push origin main"]     # C-18: comandos que pedem confirmação
  destinations: …                               # política por destino (ADR-0007)
knowledge:
  maxFiles: 4000                                # C-08
```

Cada componente do catálogo passa a ter **conteúdo canônico** e **projeções**:

| Campo | Exemplo (`spock.agents.reviewer`) |
| --- | --- |
| `id`, `version` | `spock.agents.reviewer`, 3 |
| `kind` | `agent` |
| `content` | Texto canônico: papel, entradas, saídas, ferramentas permitidas (vocabulário neutro: `read`, `search`, `edit`, `shell:read`) |
| `projections.claude-code` | `.claude/agents/code-reviewer.md`, frontmatter com `tools: Read, Grep, Glob` |
| `projections.cursor` | `.cursor/agents/code-reviewer.md`, `readonly: true` |
| `projections.codex` | `.codex/agents/code-reviewer.toml`, `developer_instructions` |
| `projections.copilot` | `.github/agents/code-reviewer.agent.md` |
| `unsupported` | IDEs sem o recurso → limitação explícita no plano, nunca arquivo inventado |
| `evals` | `EV-AGENT-REVIEWER` por projeção |

Tipos de componente: `bootstrap`, `rule`, `agent`, `skill`, `hook`, `mcp`, `policy`, `eval`, `workflow` (do SPOCK),
`judge`.

## 3. Matriz de capacidades no próprio produto

O SPOCK guarda a matriz de [IDE_COMPATIBILITY_MATRIX.md](IDE_COMPATIBILITY_MATRIX.md) como dado versionado
(`Assets/catalog/environments.json`): por IDE, por versão mínima, quais tipos de componente são suportados e com que
mecanismo. O plano consulta essa tabela; a tela mostra "Cursor não tem hook por prompt: o contexto vivo depende da
instrução". Nenhuma capacidade é marcada como suportada sem teste de conformidade do adaptador.

## 4. Agentes e orquestração

| Capacidade pedida | Proposta | Quem executa |
| --- | --- | --- |
| Definir planos | Workflow de engenharia (existe) | SPOCK |
| Contrato de tarefa | Pacote de tarefa ([DELIVERY_VERIFICATION.md](DELIVERY_VERIFICATION.md)) | SPOCK gera |
| Disponibilizar contexto | Context Engine + MCP | SPOCK |
| Delegar | O pacote vai para a IDE (pela pessoa, pelo MCP `task_get`, ou pela CLI em modo leitura que já existe) | IDE, com agentes nativos |
| Receber resultado | Leitura tipada: diff, testes, arquivos de validação | SPOCK |
| Validar evidências e acionar juízes | Verificador determinístico + juiz (existe) | SPOCK |
| Identificar pendências | Verificador | SPOCK |
| Agentes nativos da IDE | Recebem as definições projetadas; executam | IDE |

O SPOCK **não** cria um agente próprio de implementação nesta etapa (pendência D5 continua com o dono). O ciclo pedido →
código → revisão → verificação (C-11) fecha pela verificação, não por um executor novo.

## 5. Atualização controlada

| Situação | Comportamento |
| --- | --- |
| Versão nova do componente no SPOCK | Plano com diff por IDE; a pessoa aprova uma vez para todas as projeções |
| Pessoa editou a projeção de uma IDE | Conflito só naquela projeção |
| IDE removida de `aiEnvironments` | Plano remove as projeções intactas dela; preserva as editadas como conflito |
| Proposta do Harness Engineer | Continua como dado não confiável; só altera conteúdo canônico, nunca uma projeção solta, e não toca hooks nem permissões (regra existente) |

## 6. Observabilidade e custos de IA

Existente e reaproveitado: tokens por execução, teto por finalidade (com P0-2 corrigido), finalidade por etapa (P2-2),
trilha de auditoria. O que o hub acrescenta: tokens do bootstrap e do hook medidos e exibidos por IDE
([QUALITY_AND_OBSERVABILITY.md](QUALITY_AND_OBSERVABILITY.md)). Custo em dinheiro continua fora (D4).
