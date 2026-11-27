from __future__ import annotations

from typing import Callable

from agents.runner import BUDGET, Outcome
from workflows.engineering.state import (
    AGENT_VERSION,
    ARCHITECTURE,
    MODEL_LIMITATIONS,
    Segment,
    copy_state,
    fail,
    limit,
)

CanSpend = Callable[[int], bool]
ModelCall = Callable[[CanSpend], Outcome]


def budgeted(segment: Segment, node: Callable[[dict], dict], counts_iteration: bool = True) -> Callable[[dict], dict]:
    """
    O limite de etapas é do trecho que o motor chama de análise (o primeiro até o plano validado); os nós seguintes só
    respondem ao tempo e aos tokens.
    """
    def run(state: dict) -> dict:
        state = node(copy_state(state))
        if counts_iteration:
            state["iterations"] = int(state.get("iterations") or 0) + 1
        if state.get("failure"):
            return state
        if counts_iteration and state["iterations"] > segment.max_iterations:
            fail(state, "LIMIT_ITERATIONS", "A execução passou do limite de etapas.")
        elif segment.work_ms() > segment.max_milliseconds:
            fail(state, "LIMIT_TIME", "A execução passou do limite de tempo.")
        elif int(state.get("tokens") or 0) > segment.max_tokens:
            fail(state, "LIMIT_TOKENS", "A execução passou do limite de tokens.")
        return state

    return run


def model_call(state: dict, segment: Segment, call: ModelCall, adopt: Callable[[Outcome], None],
               fallback_code: str, fallback_message: str) -> int:
    """
    O que o modelo escreveu entra só se passou na validação; senão o resultado determinístico fica e duas limitações dizem o motivo.
    Passado o tempo da execução, nenhuma chamada nova começa (o resultado determinístico fica); a que já começou termina, e a espera
    por ela não conta como tempo de trabalho do runtime: um modelo lento não derruba a execução, só deixa de ser usado nas etapas seguintes.
    """
    spent = int(state.get("tokens") or 0)
    out_of_time = False

    def can_spend(needed: int) -> bool:
        nonlocal out_of_time
        if segment.elapsed_ms() >= segment.max_milliseconds:
            out_of_time = True
            return False
        return spent + needed <= segment.max_tokens

    waiting_since = segment.clock()
    outcome = call(can_spend)
    segment.model_seconds += segment.clock() - waiting_since
    if out_of_time and outcome.value is None and outcome.code == BUDGET:
        outcome.code = "MODEL_TIME_EXCEEDED"
    state["model_calls"] = int(state.get("model_calls") or 0) + outcome.calls
    state["input_tokens"] = int(state.get("input_tokens") or 0) + outcome.input_tokens
    state["output_tokens"] = int(state.get("output_tokens") or 0) + outcome.output_tokens
    state["tokens"] = spent + outcome.tokens
    if outcome.value is not None:
        adopt(outcome)
    else:
        message = MODEL_LIMITATIONS.get(outcome.code, "O modelo não pôde ser usado.")
        # O motivo da recusa (JSON cortado, texto antes do JSON, campo que falta) é texto do motor, nunca do modelo.
        limit(state, outcome.code, f"{message} ({outcome.reason})" if outcome.reason and outcome.code == "MODEL_OUTPUT_INVALID" else message)
        limit(state, fallback_code, fallback_message)
    return outcome.tokens


def run_agent(state: dict, segment: Segment | None, agent_id: str, version: str, work: Callable[[], int]) -> None:
    clock = segment.clock if segment else None
    started = clock() if clock else 0.0
    if segment:
        segment.emitter.emit("agent.started", {"agentVersion": version}, agent_id)
    tokens = work()
    duration = int((clock() - started) * 1000) if clock else 0
    record_agent(state, agent_id, duration, tokens)
    if segment:
        segment.emitter.emit("agent.completed", {"status": "completed", "durationMs": duration, "tokens": tokens}, agent_id)


def record_agent(state: dict, agent_id: str, duration: int, tokens: int) -> None:
    others = [item for item in state.get("agents") or [] if item.get("agentId") != agent_id]
    entry = {"agentId": agent_id, "agentVersion": AGENT_VERSION, "status": "completed", "durationMs": duration, "tokens": tokens}
    state["agents"] = [*others, entry] if agent_id == ARCHITECTURE else [entry, *others]
