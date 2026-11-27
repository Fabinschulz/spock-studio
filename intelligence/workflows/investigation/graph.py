"""Só existe como grafo porque há etapas e uma decisão no meio; uma pergunta simples continua sendo uma chamada só, no
assistente gerencial do motor. Não há pausa nem checkpoint: o caminho percorrido volta no ack e é o motor quem o guarda e
confere a resposta. O texto dos fatos não vai para o estado, e o token do proxy fica só no cliente de modelo.
"""

from __future__ import annotations

from typing import Callable, Iterator, TypedDict

from langgraph.graph import END, START, StateGraph

from agents.model_client import ModelClient
from agents.runner import Model
from contracts.investigation import (
    INVESTIGATION_ID,
    INVESTIGATION_SCHEMA,
    InvestigationRequest,
    parse_investigation,
)
from contracts.messages import ContractError
from infrastructure.events import Emitter, now
from workflows.engineering.state import Segment, copy_state, fail, limit
from workflows.investigation import nodes

WORKFLOW_ID = "management.investigation"
WORKFLOW_VERSION = "1"
MAX_ITERATIONS = 12
MAX_MILLISECONDS = 5000
# O motor define o orçamento de tempo; o runtime só restringe. Cada chamada de modelo de topo pode levar dezenas de segundos, então
# o teto aqui é folgado e quem decide o valor é o pedido do motor.
MAX_MILLISECONDS_MODEL = 300000

__all__ = ["WORKFLOW_ID", "WORKFLOW_VERSION", "build", "run_investigation"]


class State(TypedDict, total=False):
    """Texto de fato e de hipótese não entra: o estado é o caminho que o motor registra."""

    investigation_id: str
    project_id: str
    execution_id: str
    started_at: str
    round: int
    consulted: list[str]
    gathered: list[str]
    hypotheses: list[dict]
    needs: list[str]
    next_groups: list[str]
    searches: list[str]
    searched: list[str]
    retrieved: list[dict]
    path: list[dict]
    answer: dict | None
    rejected: list[dict]
    limitations: list[dict]
    failure: str
    stopped: str
    valid: bool
    tokens: int
    model_calls: int
    input_tokens: int
    output_tokens: int
    iterations: int


def run_investigation(payload: dict, clock: Callable[[], float] | None = None,
                      model_factory: Callable[[InvestigationRequest], Model] | None = None) -> Iterator[dict]:
    try:
        request = parse_investigation(payload)
    except ContractError as ex:
        yield _rejected(payload, str(ex))
        return
    emitter = Emitter(request.execution_id)
    model = (model_factory or _broker)(request) if request.model_allowed else None
    segment = Segment(
        emitter,
        clock,
        min(MAX_ITERATIONS, request.max_iterations),
        min(MAX_MILLISECONDS_MODEL if model else MAX_MILLISECONDS, request.max_milliseconds),
        None,
        model,
        request.max_tokens if model else 0,
    )
    yield emitter.event("workflow.started", {"workflowId": WORKFLOW_ID, "workflowVersion": WORKFLOW_VERSION})
    run = nodes.Run(request, segment)
    app = build(run).compile()
    final: dict = {}
    for mode, chunk in app.stream(_initial(request), stream_mode=["custom", "values"]):
        if mode == "custom":
            yield chunk
        else:
            final = chunk
    yield _ack(request, final)


def _over_limit(state: dict, segment: Segment) -> tuple[str, str] | None:
    if (segment.clock() - segment.started) * 1000 > segment.max_milliseconds:
        return "LIMIT_TIME", "A investigação passou do limite de tempo."
    if int(state.get("tokens") or 0) > segment.max_tokens:
        return "LIMIT_TOKENS", "A investigação passou do limite de tokens."
    return None


def _bounded(segment: Segment, node: Callable[[dict], dict], name: str, counts_iteration: bool = True) -> Callable[[dict], dict]:
    """
    Passar do tempo ou dos tokens depois de um nó não derruba o que já existe: com fatos reunidos a investigação para de olhar e
    conclui só com eles (``stopped``), e uma conclusão que terminou tarde continua valendo. Só falha quando não há nada reunido.
    """

    def run(state: dict) -> dict:
        state = node(copy_state(state))
        if counts_iteration:
            state["iterations"] = int(state.get("iterations") or 0) + 1
        if state.get("failure"):
            return state
        if counts_iteration and state["iterations"] > segment.max_iterations:
            fail(state, "LIMIT_ITERATIONS", "A execução passou do limite de etapas.")
            return state
        if (over := _over_limit(state, segment)) is None:
            return state
        code, message = over
        if name != "conclude" and not state.get("gathered"):
            fail(state, code, message)
            return state
        if name != "conclude":
            state["stopped"] = code
        limit(state, code, message)
        return state

    return run


def _after(state: dict, normal: str) -> str:
    return END if state.get("failure") else "conclude" if state.get("stopped") else normal


def build(run: nodes.Run) -> StateGraph:
    segment = run.segment
    graph = StateGraph(State)
    graph.add_node("frame", _bounded(segment, lambda state: nodes.frame(state, run), "frame"))
    graph.add_node("gather", _bounded(segment, lambda state: nodes.gather(state, run), "gather"))
    graph.add_node("hypothesize", _bounded(segment, lambda state: nodes.hypothesize(state, run), "hypothesize"))
    graph.add_node("retrieve", _bounded(segment, lambda state: nodes.retrieve(state, run), "retrieve"))
    graph.add_node("decide", _bounded(segment, lambda state: nodes.decide(state, run), "decide", counts_iteration=False))
    graph.add_node("conclude", _bounded(segment, lambda state: nodes.conclude(state, run), "conclude"))

    graph.add_edge(START, "frame")
    graph.add_conditional_edges("frame", lambda state: _after(state, "gather"), ["gather", "conclude", END])
    graph.add_conditional_edges("gather", lambda state: _after(state, "hypothesize"), ["hypothesize", "conclude", END])
    graph.add_conditional_edges("hypothesize", lambda state: _after(state, "decide"), ["decide", "conclude", END])
    # O teto de rodadas, o orçamento e o tempo para outra rodada estão em ``decide``, não aqui.
    graph.add_conditional_edges(
        "decide",
        lambda state: _after(state, "retrieve" if state.get("searches") else "gather" if state.get("next_groups") else "conclude"),
        ["retrieve", "gather", "conclude", END],
    )
    graph.add_conditional_edges(
        "retrieve", lambda state: _after(state, "gather" if state.get("next_groups") else "hypothesize"), ["gather", "hypothesize", "conclude", END]
    )
    graph.add_edge("conclude", END)
    return graph


def _initial(request: InvestigationRequest) -> dict:
    return {
        "investigation_id": request.investigation_id,
        "project_id": request.project_id,
        "execution_id": request.execution_id,
        "started_at": now(),
        "round": 0,
        "consulted": [],
        "gathered": [],
        "hypotheses": [],
        "needs": [],
        "next_groups": [],
        "searches": [],
        "searched": [],
        "retrieved": [],
        "path": [],
        "rejected": [],
        "limitations": [],
        "failure": "",
        "valid": False,
        "answer": None,
        "tokens": 0,
        "model_calls": 0,
        "input_tokens": 0,
        "output_tokens": 0,
        "iterations": 0,
    }


def _ack(request: InvestigationRequest, state: dict) -> dict:
    state = copy_state(state) if state else _initial(request)
    failure = state.get("failure") or ""
    answer = state.get("answer") if not failure else None
    if failure:
        status = "failed"
    else:
        status = "completed_with_limitations" if state.get("limitations") else "completed"
    ack = {
        "schema": INVESTIGATION_SCHEMA,
        "investigationId": request.investigation_id,
        "executionId": request.execution_id,
        "projectId": request.project_id,
        "status": status,
        "answer": answer,
        "rejected": list(state.get("rejected") or []) if answer else [],
        "path": list(state.get("path") or []),
        "consulted": list(state.get("gathered") or []),
        "retrieved": list(state.get("retrieved") or []),
        "limitations": list(state.get("limitations") or []),
        "rounds": int(state.get("round") or 0),
        "usage": {
            "modelCalls": int(state.get("model_calls") or 0),
            "inputTokens": int(state.get("input_tokens") or 0),
            "outputTokens": int(state.get("output_tokens") or 0),
            "tokens": int(state.get("tokens") or 0),
        },
    }
    if failure:
        ack["failure"] = failure
    return ack


def _rejected(payload: object, reason: str) -> dict:
    raw = payload.get("investigationId") if isinstance(payload, dict) else None
    identifier = raw if isinstance(raw, str) and INVESTIGATION_ID.match(raw) else ""
    return {"schema": INVESTIGATION_SCHEMA, "investigationId": identifier, "status": "rejected", "failure": "CONTRACT_INVALID", "field": reason}


def _broker(request: InvestigationRequest) -> Model:
    return ModelClient(request.broker_url, request.broker_token, request.execution_id)
