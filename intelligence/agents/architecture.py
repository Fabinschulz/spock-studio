"""Architecture Agent com modelo: áreas afetadas e riscos, com evidência obrigatória.

Só roda quando o pedido pede impacto. A saída é ``engineering.impact.v1`` e passa em ``impact_from_model``: áreas e riscos
do vocabulário fechado, citação só de trechos recuperados, citação obrigatória quando houve trechos e o risco
``sem-evidencia`` obrigatório quando não houve. Se não valer, o grafo fica com o impacto determinístico.
"""

from __future__ import annotations

from agents import prompts
from agents.runner import Model, Outcome, ask
from contracts.engineering import AREAS, IMPACT_SCHEMA, RISKS, impact_from_model, known_chunks

AGENT_ID = "architecture"
MAX_OUTPUT_TOKENS = 1024

_TASK = (
    "Você é o agente de arquitetura de um assistente de engenharia de software. Diga quais áreas do projeto o pedido "
    "afeta e quais riscos existem, só com base no conteúdo fornecido. Formato exato, em JSON: "
    f'{{"schema":"{IMPACT_SCHEMA}","areas":[...],"risks":[...],"evidence":["id-do-trecho"]}}. '
    f"'areas' usa só estes valores: {', '.join(sorted(AREAS))}. 'risks' usa só estes valores: {', '.join(sorted(RISKS))} "
    "(use 'sem-evidencia' se nenhum trecho do projeto foi fornecido e 'sem-stack' se nenhuma stack foi detectada). "
    "'evidence' lista os ids entre colchetes dos trechos que sustentam o impacto; cite só ids que aparecem no conteúdo e, "
    "se houver trechos, cite ao menos um."
)


def messages(goal: str, stacks: list[str], evidence: list[dict]) -> list[dict]:
    stack_line = ", ".join(stacks) if stacks else "nenhuma detectada"
    user = f"Stacks detectadas: {stack_line}\n\n{prompts.request_block(goal)}\n\n{prompts.project_block(evidence)}"
    return [prompts.system(_TASK), {"role": "user", "content": user}]


def assess(model: Model, state: dict, can_spend) -> Outcome:
    known = known_chunks(state.get("evidence"))
    has_stacks = bool(state["stacks"])

    def parse(document: dict) -> dict:
        return impact_from_model(document, known, has_stacks)

    return ask(
        model,
        agent_id=AGENT_ID,
        purpose="architecture",
        messages=messages(state["goal"], list(state["stacks"]), list(state.get("evidence") or [])),
        max_tokens=MAX_OUTPUT_TOKENS,
        response_schema=IMPACT_SCHEMA,
        parse=parse,
        can_spend=can_spend,
    )
