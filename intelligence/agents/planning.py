"""Planning Agent: o primeiro agente com modelo.

Escreve o plano ``engineering.plan.v1`` a partir do pedido, das stacks detectadas e dos trechos recuperados. A saída só
vale se passar em ``plan_from_model`` (formato, ids únicos, sem comando nem caminho, citação só de trechos fornecidos e,
havendo trechos, ao menos uma citação). Se não valer, o grafo fica com o plano determinístico.
"""

from __future__ import annotations

from agents import prompts
from agents.runner import Model, Outcome, ask
from contracts.engineering import PLAN_SCHEMA, Rejected, known_chunks, plan_from_model

AGENT_ID = "planning"
AGENT_VERSION = "1"
MAX_OUTPUT_TOKENS = 1024

_TASK = (
    "Você é o agente de planejamento de um assistente de engenharia de software. Escreva um plano curto e verificável "
    "para o pedido, em português, com 2 a 8 etapas. Formato exato, em JSON: "
    f'{{"schema":"{PLAN_SCHEMA}","steps":[{{"id":"slug-em-minusculas","title":"texto de uma linha","evidence":["id-do-trecho"]}}]}}. '
    "O 'id' de cada etapa é único e usa só letras minúsculas, números e hífen. O 'title' tem no máximo 120 caracteres. "
    "Em 'evidence' cite os ids entre colchetes dos trechos do projeto que sustentam a etapa; cite só ids que aparecem "
    "no conteúdo. Se houver trechos, ao menos uma etapa precisa citar um. Não invente fatos que o conteúdo não mostre."
)


def messages(goal: str, stacks: list[str], evidence: list[dict]) -> list[dict]:
    stack_line = ", ".join(stacks) if stacks else "nenhuma detectada"
    user = f"Stacks detectadas: {stack_line}\n\n{prompts.request_block(goal)}\n\n{prompts.project_block(evidence)}"
    return [prompts.system(_TASK), {"role": "user", "content": user}]


def plan(model: Model, state: dict, can_spend) -> Outcome:
    known = known_chunks(state.get("evidence"))

    def parse(document: dict) -> dict:
        return plan_from_model(document, known)

    return ask(
        model,
        agent_id=AGENT_ID,
        purpose="plan",
        messages=messages(state["goal"], list(state["stacks"]), list(state.get("evidence") or [])),
        max_tokens=MAX_OUTPUT_TOKENS,
        response_schema=PLAN_SCHEMA,
        parse=parse,
        can_spend=can_spend,
    )


__all__ = ["AGENT_ID", "AGENT_VERSION", "MAX_OUTPUT_TOKENS", "Rejected", "messages", "plan"]
