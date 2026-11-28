from __future__ import annotations

import json
from dataclasses import dataclass
from typing import Callable, Protocol

from agents.model_client import ModelError, ModelReply
from contracts.engineering import Rejected

ATTEMPTS = 2
INVALID = "MODEL_OUTPUT_INVALID"
BUDGET = "MODEL_BUDGET_EXCEEDED"
# Sem tokenizador aqui: três caracteres por token erra para cima em português e em código, que é o lado seguro.
CHARS_PER_TOKEN = 3


def input_estimate(messages: list[dict]) -> int:
    return -(-sum(len(str(message.get("content", ""))) for message in messages) // CHARS_PER_TOKEN)


class Model(Protocol):
    def complete(self, agent_id: str, purpose: str, messages: list[dict], max_tokens: int, response_schema: str | None = None) -> ModelReply: ...


@dataclass
class Outcome:
    """O uso conta mesmo quando a saída foi recusada."""

    value: dict | None = None
    code: str = ""
    calls: int = 0
    input_tokens: int = 0
    output_tokens: int = 0
    model: str = ""
    model_version: str = ""
    # Por que a última saída foi recusada, em texto do próprio motor (nunca texto do modelo): é o que diferencia um JSON cortado, um texto
    # antes do JSON e um campo que falta, e o que deixa a pessoa agir.
    reason: str = ""

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
        # O motor também cobra, mas não chamar já economiza a ida e volta e deixa o código claro. A entrada entra na
        # reserva: sem ela, uma chamada com contexto grande passava na conferência e estourava o teto da execução.
        if not can_spend(outcome.tokens + input_estimate(conversation) + max_tokens):
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
                raise Rejected("A resposta precisa ser um objeto JSON.")  # vira outcome.reason no except abaixo
            outcome.value = parse(document)
            outcome.code = ""
            return outcome
        except json.JSONDecodeError as ex:
            reason = "A resposta não é um JSON válido. Responda só com o objeto JSON, sem texto nem cercas de código."
            outcome.reason = f"JSON inválido na posição {ex.pos} de {len(reply.text)} caracteres"
        except Rejected as ex:
            reason = str(ex)
            outcome.reason = reason[:200]
        outcome.code = INVALID
        conversation = [
            *messages,
            {"role": "assistant", "content": reply.text[:4000] or "(vazio)"},
            {"role": "user", "content": f"A resposta foi recusada: {reason} Responda de novo só com o JSON corrigido."},
        ]
    return outcome
