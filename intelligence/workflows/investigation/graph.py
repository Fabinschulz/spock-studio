"""Grafo management.investigation (versão 1) em LangGraph: a investigação de gestão em etapas.

É o segundo grafo do runtime, e só existe porque aqui há etapas e uma decisão no meio: enquadrar a pergunta, reunir fatos,
levantar hipóteses, decidir se faltam fatos de outro grupo (riscos, execuções, mudanças) e voltar a reunir, com teto de
rodadas, até concluir. Uma pergunta simples continua sendo uma chamada só, no assistente gerencial do motor.

Os fatos são do motor e o texto deles não vai para o estado. Nada é gravado no projeto e não há pausa nem checkpoint: o
caminho percorrido (etapas, grupos e ids de fatos consultados) volta no ack e é o motor quem o guarda e confere a resposta.
O token do proxy fica só no cliente de modelo.
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
from workflows.engineering.budget import budgeted
from workflows.engineering.state import Segment, copy_state
from workflows.investigation import nodes

WORKFLOW_ID = "management.investigation"
WORKFLOW_VERSION = "1"
MAX_ITERATIONS = 12
MAX_MILLISECONDS = 5000
# Até quatro chamadas de modelo (três rodadas de hipóteses e a conclusão): o motor define o teto, o runtime só restringe.
MAX_MILLISECONDS_MODEL = 150000
MAX_TOKENS = 40000

__all__ = ["WORKFLOW_ID", "WORKFLOW_VERSION", "build", "run_investigation"]


class State(TypedDict, total=False):
    """A memória de trabalho: ids, nomes de grupo e contagens. Texto de fato e de hipótese não é o que o caminho guarda."""

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
    valid: bool
    tokens: int
    model_calls: int
    input_tokens: int
    output_tokens: int
    iterations: int


def run_investigation(payload: dict, clock: Callable[[], float] | None = None,
                      model_factory: Callable[[InvestigationRequest], Model] | None = None) -> Iterator[dict]:
    """``model_factory`` troca o cliente do proxy (os testes usam um modelo falso). Devolve eventos e, por último, o ack."""
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
        min(MAX_TOKENS, request.max_tokens) if model else 0,
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


def build(run: nodes.Run) -> StateGraph:
    segment = run.segment
    graph = StateGraph(State)
    graph.add_node("frame", budgeted(segment, lambda state: nodes.frame(state, run)))
    graph.add_node("gather", budgeted(segment, lambda state: nodes.gather(state, run)))
    graph.add_node("hypothesize", budgeted(segment, lambda state: nodes.hypothesize(state, run)))
    graph.add_node("retrieve", budgeted(segment, lambda state: nodes.retrieve(state, run)))
    graph.add_node("decide", budgeted(segment, lambda state: nodes.decide(state, run), counts_iteration=False))
    graph.add_node("conclude", budgeted(segment, lambda state: nodes.conclude(state, run)))

    graph.add_edge(START, "frame")
    graph.add_conditional_edges("frame", lambda state: END if state.get("failure") else "gather", ["gather", END])
    graph.add_conditional_edges("gather", lambda state: END if state.get("failure") else "hypothesize", ["hypothesize", END])
    graph.add_conditional_edges("hypothesize", lambda state: END if state.get("failure") else "decide", ["decide", END])
    # O laço: pedir mais fatos volta a reunir. O teto de rodadas e o orçamento estão em ``decide``, não aqui.
    graph.add_conditional_edges(
        "decide",
        lambda state: END if state.get("failure") else ("retrieve" if state.get("searches") else "gather" if state.get("next_groups") else "conclude"),
        ["retrieve", "gather", "conclude", END],
    )
    # Depois de buscar na memória, ainda pode haver grupos de fatos pedidos; senão o modelo lê o que veio e decide de novo.
    graph.add_conditional_edges(
        "retrieve", lambda state: END if state.get("failure") else ("gather" if state.get("next_groups") else "hypothesize"), ["gather", "hypothesize", END]
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
    """Um pedido fora do contrato não vira execução: devolve um ack recusado, só com o campo que não passou."""
    raw = payload.get("investigationId") if isinstance(payload, dict) else None
    identifier = raw if isinstance(raw, str) and INVESTIGATION_ID.match(raw) else ""
    return {"schema": INVESTIGATION_SCHEMA, "investigationId": identifier, "status": "rejected", "failure": "CONTRACT_INVALID", "field": reason}


def _broker(request: InvestigationRequest) -> Model:
    return ModelClient(request.broker_url, request.broker_token, request.execution_id)
