"""O ciclo comum dos agentes com modelo: pedir, validar, pedir de novo uma vez e, se nada servir, desistir.

Quem chama decide o que fazer com a desistência (os dois agentes caem na regra determinística). Aqui só se conta o que
foi gasto e se explica, com um código, por que não deu certo.
"""

from __future__ import annotations

import json
from dataclasses import dataclass
from typing import Callable, Protocol

from agents.model_client import ModelError, ModelReply
from contracts.engineering import Rejected

ATTEMPTS = 2
INVALID = "MODEL_OUTPUT_INVALID"
BUDGET = "MODEL_BUDGET_EXCEEDED"


class Model(Protocol):
    def complete(self, agent_id: str, purpose: str, messages: list[dict], max_tokens: int, response_schema: str | None = None) -> ModelReply: ...


@dataclass
class Outcome:
    """``value`` é a saída validada, ou ``None`` com o ``code`` do motivo. O uso conta mesmo quando a saída foi recusada."""

    value: dict | None = None
    code: str = ""
    calls: int = 0
    input_tokens: int = 0
    output_tokens: int = 0
    model: str = ""
    model_version: str = ""

    @property
    def tokens(self) -> int:
        return self.input_tokens + self.output_tokens


def ask(
    model: Model,
    *,
    agent_id: str,
    purpose: str,
    messages: list[dict],
    max_tokens: int,
    response_schema: str,
    parse: Callable[[dict], dict],
    can_spend: Callable[[int], bool],
) -> Outcome:
    outcome = Outcome()
    conversation = list(messages)
    for _ in range(ATTEMPTS):
        # O motor também cobra, mas não chamar já economiza a ida e volta e deixa o código claro.
        if not can_spend(outcome.tokens + max_tokens):
            outcome.code = BUDGET
            return outcome
        try:
            reply = model.complete(agent_id, purpose, conversation, max_tokens, response_schema)
        except ModelError as ex:
            outcome.code = ex.code
            return outcome
        outcome.calls += 1
        outcome.model, outcome.model_version = reply.model, reply.model_version
        outcome.input_tokens += reply.input_tokens
        outcome.output_tokens += reply.output_tokens
        try:
            document = json.loads(reply.text)
            if not isinstance(document, dict):
                raise Rejected("A resposta precisa ser um objeto JSON.")
            outcome.value = parse(document)
            outcome.code = ""
            return outcome
        except json.JSONDecodeError:
            reason = "A resposta não é um JSON válido. Responda só com o objeto JSON, sem texto nem cercas de código."
        except Rejected as ex:
            reason = str(ex)
        outcome.code = INVALID
        conversation = [
            *messages,
            {"role": "assistant", "content": reply.text[:4000] or "(vazio)"},
            {"role": "user", "content": f"A resposta foi recusada: {reason} Responda de novo só com o JSON corrigido."},
        ]
    return outcome
