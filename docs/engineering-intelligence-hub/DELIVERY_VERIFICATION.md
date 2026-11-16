# Verificação de entrega

> Parte do estudo [Engineering Intelligence Hub](README.md) (2026-11-16). Retoma o P1-2 da auditoria ("elo de entrega"),
> que ela chamou de "a iniciativa de maior valor para a visão", e o adapta ao modelo em que a IDE executa.

## 1. Por que é o centro do valor

A auditoria concluiu que o SPOCK "planeja sob governança e entrega a execução para fora". A nova direção aceita isso
explicitamente: a IDE executa. Então o que o SPOCK precisa fazer melhor que a IDE é **conferir**: o que foi pedido, o que
mudou, o que foi testado, o que ficou de fora. É também o que leva o estado real à Gestão (cenário 10).

Hoje não existe: pacote de tarefa, leitura de critérios, leitura de diff, detecção de pendências (`CURRENT_STATE_REASSESSMENT.md`).

## 2. Fluxo

```text
 Item (Jira / Azure DevOps / GitHub) ──► Pacote de tarefa (motor)
                                            objetivo, critérios, escopo provável,
                                            testes esperados, regras aplicáveis
                                                     │
                     IDE lê por MCP (`work_item_context`) e implementa
                                                     │
                                  `delivery_check` (MCP ou tela)
                                                     │
          ┌──────────────────────────┬───────────────┴──────────────┬─────────────────────┐
          ▼                          ▼                              ▼                     ▼
  Diff da branch (Git)     Testes: comando detectado       Validações do projeto     Critérios
  arquivos, escopo         e resultado informado ou         (.specs/…/validation.md  contra evidência
                           rodado com aprovação             se existir)
          └──────────────────────────┴───────────────┬──────────────┴─────────────────────┘
                                                     ▼
                         Verificador determinístico (.NET) + juiz opcional
                                                     ▼
                     Execução `delivery` com pendências e evidências
                                                     ▼
                Gestão: risco "entrega com pendência", linha do tempo, item ligado
```

## 3. Pacote de tarefa

| Campo | Fonte | Obrigatório |
| --- | --- | --- |
| `workItem` | Chave nativa + provedor | Sim (ou "pedido livre" marcado) |
| `objective` | Título + resumo da descrição | Sim |
| `acceptanceCriteria[]` | Campo de critérios (Azure DevOps nativo; Jira configurável); senão, extração da descrição marcada `inferred` e mostrada para a pessoa confirmar | Sim, ou limitação "sem critérios" |
| `scope.likelyPaths[]` | Busca no índice + histórico de arquivos do item (quando houver) | Não |
| `expectedTests[]` | Comandos detectados no perfil | Não |
| `rules[]` | Regras do Harness para os caminhos prováveis | Sim |
| `classification` | `issuetype` por regra; sem ele, finalidade `classify` (existe no contrato) marcada `inferred` | Sim |

Guardado em `projects/<id>/tasks/<task>.json`, versionado pelo motor, sem texto livre do agente.

## 4. Verificador

Determinístico, no .NET (evita segundo motor e mantém a regra "regras só no .NET").

| Verificação | Evidência | Resultado possível |
| --- | --- | --- |
| Houve mudança? | `git diff` da branch contra a base | `no-change` vira pendência |
| Mudança fora do escopo provável | Arquivos alterados fora de `likelyPaths` e fora de testes | Aviso, não bloqueio |
| Regras do Harness | Evals existentes sobre os arquivos alterados | Pendência por regra violada |
| Testes | Comando detectado: resultado informado pela IDE (texto não confiável) ou rodado pelo SPOCK **com aprovação** e ambiente filtrado | `passed`, `failed`, `not-run` |
| Teste novo para código novo | Arquivos de teste alterados junto com código | Aviso |
| Critério de aceite | Cada critério precisa de uma evidência ligada (teste, arquivo, validação). Ligação por id (`AC-1`) no teste ou na validação, ou confirmação da pessoa | `met`, `unmet`, `unverifiable` |
| Validação do projeto | `.specs/features/<f>/validation.md` quando a estrutura de especificação está instalada (o verificador do Harness já a escreve) | Lida como dado |
| Juiz (opcional) | Juiz por modelo existente, só sobre o resultado | Opinião anotada; nunca muda o veredito determinístico |

Saída: `pending[]` (o que falta, com a evidência ausente), `verified[]`, `unverifiable[]` (o que o SPOCK não consegue
provar, dito em voz alta).

## 5. Dependências

| Depende de | Por quê |
| --- | --- |
| Leitura tipada de Git (P1-3) | Diff, branch, commits |
| Critérios do Jira (INT-07) | Sem eles, critérios ficam `inferred` |
| Identidade estável do projeto | Ligar a branch e a cópia de trabalho ao projeto |
| Filtro de ambiente (R-10) | Rodar testes com segurança |

## 6. Critério de aceite da iniciativa

Num projeto real: uma entrega com um critério não atendido aparece como pendência com a evidência que falta; uma entrega
completa fecha sem pendência; um teste falho aparece com a saída; e o gestor vê, na Gestão e pela IDE, que o item tem
entrega com pendência.
