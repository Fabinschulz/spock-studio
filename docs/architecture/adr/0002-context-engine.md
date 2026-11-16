# ADR-0002 — Context Engine no motor, sobre o que já existe

**Status:** proposta em 2026-11-16, aguarda o dono.
**Contexto completo:** [../../engineering-intelligence-hub/CONTEXT_ENGINE.md](../../engineering-intelligence-hub/CONTEXT_ENGINE.md).

## Contexto

Workflow, investigação, assistente e MCP montam contexto cada um do seu jeito (`WorkflowContextBuilder`,
`EvidenceBuilder`, `ProjectBriefing`). Não há orçamento comum, nem proveniência tipada por item, nem classificação da
origem (descoberto, declarado, aprovado, integração, calculado, inferido), nem frescor. A nova direção faz do contexto
servido às IDEs a capacidade principal do produto.

## Decisão proposta

1. Um serviço `Context` no `Mariliz.Application` compõe contexto por `(projeto, intenção, orçamento, foco)`, sem modelo.
2. Ele só compõe: os cálculos continuam em `Management`, a busca continua no Python, as regras continuam no Harness.
3. Todo item devolvido carrega `source`, `ref`, `classification`, `observedAt`, `freshness` e `limitations`.
4. Inferência de modelo nunca vira regra nem fato sem aprovação; aparece sempre rotulada.
5. `contextVersion` é o hash das seções persistentes e versiona os arquivos projetados para as IDEs.
6. O RAG continua FTS5 + BM25 + símbolos + RRF; embeddings só com ganho medido (D3).

## Consequências

- Os consumidores atuais migram aos poucos; `ProjectBriefing` vira a intenção `bootstrap`.
- Métodos de protocolo `context.*` novos; contratos com fixtures.
- Custo: um serviço novo; risco de virar um segundo motor, mitigado pela regra "só compõe".

## Alternativas rejeitadas

- Context Engine no Python: política e orçamento são regras, e regras ficam no .NET.
- Carregar todo o contexto em cada prompt: estoura orçamento e mistura dado velho com atual.
- Trocar o índice por banco vetorial agora: sem medição que justifique.
