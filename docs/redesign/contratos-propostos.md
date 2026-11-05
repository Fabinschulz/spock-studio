# Contratos mínimos propostos entre o motor .NET e a interface

Contexto: o redesign pede que a interface mostre a atividade **real** do motor. Em [proposta-de-redesign.md](proposta-de-redesign.md#9-limitações-da-arquitetura-atual) estão as lacunas; aqui, o menor contrato que resolve cada uma. **Estado:** os itens 1, 2 e 3 estão implementados (motor, `contracts` e interface); os demais seguem propostos. O protótipo exibe os propostos no modo "Contrato proposto (demonstração)".

| Item | Estado |
| --- | --- |
| 1 · `at` | feito |
| 2 · `run.plan` | feito (só na aplicação) |
| 3 · `eval.started` / `eval.completed` | feito (aplicação e validação avulsa) |
| 4 · progresso de análise e planejamento | proposto |
| 5 · estados `analyzing` / `awaiting_approval` | proposto |
| 6 · `agent.state` | proposto; depende de decisão de produto |
| 7 · `kind` em `file.applied` | proposto |

Regras que valem para todos:

- Mesmo envelope dos eventos atuais (`v`, `kind: "event"`, `runId`, `type`, `seq`, `data`), mesmo canal (`engine_event`) e mesmos snapshots (`run.snapshot` devolve os eventos).
- Campos novos são **opcionais** e a interface ignora tipos que não conhece (o `applyEvent` atual já faz isso: `default: return run`). Compatível com versões antigas.
- Cada evento novo entra em `contracts` (JSON Schema + fixture _golden_) e no Zod de `shared/schema`.
- A interface nunca deduz estado de um evento ausente: sem evento, mostra "o motor não informa".

## 1. Carimbo de tempo — `at`

| Campo | Tipo | Observação |
| --- | --- | --- |
| `at` | string (ISO 8601 UTC) | Em **todo** evento. `RunHandle.Publish` já centraliza a publicação (`RunHandle.cs`) e o `IClock` já existe. |

Resolve: durações medidas no cliente na recepção (imprecisas com _polling_ e reinício da interface).

**Como ficou:** `at` é um campo do **envelope** (ao lado de `seq`), não de `data`: `YYYY-MM-DDTHH:mm:ss.fffZ`, sempre em UTC. `RunHandle.Publish` lê o `IClock` uma vez por evento e usa o mesmo instante no evento enviado e no gravado (o retrato e o histórico em disco também o trazem). Opcional no schema: eventos antigos continuam válidos. A interface usa o `at` quando existe e o instante de chegada quando não.

## 2. Declaração das etapas — `run.plan`

```json
{ "type": "run.plan", "data": { "steps": [ { "id": "precheck", "title": "Verificar pré-condições" } ] } }
```

Publicado **uma vez**, antes do primeiro `step.started`, com a lista ordenada das etapas que a execução fará (inclui uma etapa `command:<id>` por comando aprovado). Resolve: a interface hoje precisa conhecer a ordem e os títulos (strings em C#) para mostrar "aguardando". Fonte natural: `ApplyExecution.RunAsync` já conhece a sequência.

**Como ficou:** `ApplySteps` guarda o id e o título de cada etapa num só lugar; é dele que saem o `run.plan` e os `step.started`, então o anunciado e o executado não se separam (há teste que compara as duas listas). O evento é publicado no começo de `RunAsync`, só com os comandos aprovados que pertencem ao plano. Só a aplicação o publica: validação e reversão têm uma etapa só ou nenhuma lista a anunciar.

## 3. Um evento por Eval — `eval.started` / `eval.completed`

```json
{ "type": "eval.started",   "data": { "id": "EV-AGENT-001", "name": "Agentes", "kind": "deterministic" } }
{ "type": "eval.completed", "data": { "id": "EV-AGENT-001", "status": "passed" } }
```

`status` ∈ `passed | failed | blocked | not_run` (os mesmos de `EvalResult`). Publicados por `EvalRunner.RunAsync` ao redor de `RunCheckAsync`. Resolve: a validação aparece como uma etapa de ~5 s e os 14 resultados chegam juntos. O `kind: "model"` (juiz) identifica o único Eval que usa um modelo.

**Como ficou:** `EvalRunner` recebe um `IEvalProgress` opcional; a aplicação e a validação avulsa passam um que publica na execução. `kind` e `status` saem com os mesmos valores do resultado final (testado contra o relatório). Um Eval cancelado no meio fica só com o `eval.started`.

## 4. Progresso de análise e planejamento

Hoje `project.discover` e `harness.plan` são chamadas únicas. Duas opções, da menor à maior:

- **A (mínima):** manter a chamada e, durante ela, publicar eventos com um `runId` efêmero devolvido no pedido (`discover.probe`: `{ "probe": "stacks" | "tooling" | "ai_environments" | "inventory" | "specification" | "tools" | "git", "state": "started" | "completed" }`; `plan.stage`: `{ "stage": "discovery" | "declared_config" | "composition" | "plan", "state": "started" | "completed" }`). O motor já executa esses blocos em sequência, nesta ordem (`ProjectDiscoverer.DiscoverAsync` e `ProjectPlanner.ReplanAsync`).
- **B:** transformar ambos em _runs_ (como `harness.apply`), com `run.snapshot` e cancelamento.

A interface só mostra o que esses eventos informarem (por exemplo "Procurando comandos de build e teste"), nunca percentuais.

## 5. Estados de execução já definidos e não emitidos

`RunStates.Analyzing` e `RunStates.AwaitingApproval` existem (`RunStates.cs`) mas nenhum código os publica. Se a opção 4-B for adotada, `analyzing` passa a ser emitido na análise; `awaiting_approval` só faz sentido se a aplicação passar a pausar esperando decisão **dentro** de uma execução (hoje a aprovação é a etapa Revisão, antes de aplicar).

## 6. Atividade de agentes — `agent.state` (depende de decisão de produto)

Só é possível se o **motor passar a orquestrar** os agentes (por exemplo, um adaptador que invoque o ambiente de IA da pessoa em modo não interativo) ou se existir um canal de eventos vindo desse ambiente. Hoje o Harness Engineer roda fora do Mariliz e o motor só lê `.mariliz/proposal.json`.

```json
{
  "type": "agent.state",
  "data": {
    "agent": "harness-engineer",
    "name": "Harness Engineer",
    "state": "working",
    "activity": "Lendo a estrutura do projeto"
  }
}
```

- `agent`: id de um agente **do catálogo instalado** (harness-engineer, code-reviewer, verifier…). Nenhum nome fora do catálogo.
- `state` ∈ `waiting | working | done | failed`; ausência de evento = "não iniciado".
- `activity`: frase curta produzida pelo motor a partir de algo que ele **observou** (arquivo lido, etapa concluída). Se o motor não souber, omite o campo e a interface mostra apenas o estado.

Enquanto isso não existir, a interface mostra o Harness Engineer como o fato que é: **"Aguardando proposta — roda no seu ambiente de IA"** (derivado de `proposalOrigin` e da limitação `HARNESS_ENGINEER_NOT_INVOKED`).

## 7. Tipo do componente em `file.applied` (opcional)

`{ "path": "...", "op": "create", "kind": "agent" | "skill" | "rule" | "hook" | "config" }`. A interface já consegue o mesmo cruzando `path` com `plan.changes[].kind`; o campo apenas simplifica.

## Compatibilidade e testes

| Item | Como |
| --- | --- |
| Interface antiga com motor novo | ignora os tipos desconhecidos |
| Motor antigo com interface nova | cai no vocabulário atual (`EXPECTED_APPLY_STEPS` local, validação como uma etapa, sem Evals individuais), que é exatamente o modo **Contrato atual** do protótipo |
| Fixtures | um _golden_ por evento em `contracts/fixtures`; `buildOperation` já tem testes que cobrem os dois modos (`src/prototype/domain/operation-model.test.ts`) |
