# ADR-0006 — Gestão na IDE com números do motor

**Status:** proposta em 2026-11-16, aguarda o dono.
**Contexto completo:** [../../engineering-intelligence-hub/ENTERPRISE_INTELLIGENCE.md](../../engineering-intelligence-hub/ENTERPRISE_INTELLIGENCE.md).

## Contexto

A Gestão já é determinística no .NET, com `MetricSource` (provedor, recurso, última sincronização, completude), previsão
com premissas e limitações, riscos por regra e assistente verificado por fatos. Na IDE, quem redige é o modelo da IDE, que
o SPOCK não consegue verificar.

## Decisão proposta

1. As ferramentas de gestão do MCP devolvem números prontos do motor, com id da métrica, fórmula (pelo catálogo), fonte,
   período, `syncedAt`, `freshness` e limitações. O modelo da IDE não calcula.
2. Um catálogo único de métricas versionado (`contracts/management/metrics.json`) alimenta motor, tela e MCP.
3. A proteção contra número inventado na IDE é o contrato de dados e a instrução do bloco ("cite o id e a data; se o
   SPOCK disser indisponível, diga indisponível"), não a verificação do texto.
4. Dado velho nunca é apresentado como atual: vem marcado com a data da última sincronização completa.
5. Nenhum indicador por pessoa (decisão vigente). Nenhuma organização no modelo até a decisão DD-1.
6. Engenharia e Gestão leem os mesmos itens e execuções; não há base paralela.

## Consequências

- A resposta verificada do próprio SPOCK (`management_answer`) fica como opção depois da validação com modelo real.
- Leitura de `description` e critérios do Jira entra no caminho crítico.

## Alternativas rejeitadas

- Deixar o modelo da IDE calcular a partir dos itens: números sem regra comum e sem fonte.
- Gravar status em `CLAUDE.md`: dado congelado apresentado como atual.
