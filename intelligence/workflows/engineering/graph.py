"""Só com ``policy.modelAllowed`` e o endereço do proxy o Planning e o Architecture tentam o modelo; se a saída não passar
na validação ou o orçamento acabar, o resultado determinístico fica e uma limitação diz por quê. O texto das fontes não
entra no estado nem no checkpoint, e o token do proxy fica só no ``Segment``. A pausa vai para o checkpoint em SQLite,
fora do projeto, para a retomada sobreviver a um reinício.
"""

from __future__ import annotations

from contextlib import nullcontext
from typing import Callable, Iterator

from langgraph.checkpoint.memory import InMemorySaver
from langgraph.graph import END, START, StateGraph
from langgraph.types import Command

from agents.model_client import ModelClient
from agents.runner import Model
from contracts.messages import ContractError, ResumeRequest, StartRequest, parse_resume, parse_start
from infrastructure.events import Emitter
from memory.execution import ExecutionError, drop, saver
from workflows.engineering.ack import build_ack, failed_ack, lost, rejected
from workflows.engineering.budget import budgeted
from workflows.engineering.checkpoint import checkpoint_ok
from workflows.engineering.nodes import (
    approval_gate,
    architecture_agent,
    context_builder,
    evaluation_judge,
    impact_eval,
    interrupt_id,
    plan_validator,
    planning_agent,
    propose_actions,
    rag_research,
    request_analyzer,
    synthesizer,
)
from workflows.engineering.state import (
    MAX_ITERATIONS,
    MAX_MILLISECONDS,
    MAX_MILLISECONDS_MODEL,
    WORKFLOW_ID,
    WORKFLOW_VERSION,
    Segment,
    State,
    blank_state,
    initial_state,
    segment_from_state,
)

__all__ = [
    "WORKFLOW_ID", "WORKFLOW_VERSION", "Segment", "Emitter", "build", "checkpoint_ok", "impact_eval", "interrupt_id",
    "plan_validator", "run_cancel", "run_resume", "run_start",
]


def broker_client(request: StartRequest) -> Model:
    return ModelClient(request.broker_url, request.broker_token, request.execution_id)


def run_start(payload: dict, clock: Callable[[], float] | None = None,
              model_factory: Callable[[StartRequest], Model] | None = None) -> Iterator[dict]:
    try:
        request = parse_start(payload)
    except ContractError as ex:
        yield rejected(payload, str(ex))
        return
    emitter = Emitter(request.execution_id)
    model = (model_factory or broker_client)(request) if request.model_allowed else None
    # O motor define os limites; o runtime só pode ser mais restritivo. O teto de tokens é o do motor (o contrato recusa
    # um acima da faixa) e só existe com modelo.
    ceiling = MAX_MILLISECONDS_MODEL if model else MAX_MILLISECONDS
    segment = Segment(
        emitter,
        clock,
        min(MAX_ITERATIONS, request.limits.max_iterations),
        min(ceiling, request.limits.max_milliseconds),
        request.sources,
        model,
        request.limits.max_tokens if model else 0,
    )
    yield emitter.event("workflow.started")
    initial = initial_state(request)
    initial["limits"] = segment.limits()
    if request.requires_approval and not request.execution_root:
        yield failed_ack(initial, "CHECKPOINT_UNAVAILABLE", "Sem memória de execução para pausar.")
        return
    yield from _drive(segment, request.execution_root, request.project_id, request.run_id, initial)


def run_resume(payload: dict, clock: Callable[[], float] | None = None) -> Iterator[dict]:
    try:
        request = parse_resume(payload)
    except ContractError as ex:
        yield rejected(payload, str(ex))
        return
    paused = _paused(request)
    if paused is None:
        yield lost(request)
        return
    state, pending = paused
    if not request.approved:
        yield build_ack(state, "interrupted", pending)
        return
    proposed = set(pending.get("actionIds") or [])
    if not set(request.action_ids) <= proposed:
        # Aprovar o que nunca foi proposto não retoma nada: a pausa continua onde estava.
        yield failed_ack(dict(state), "APPROVAL_MISMATCH", "A aprovação cita ações que não foram propostas.")
        return
    segment = segment_from_state(Emitter(request.execution_id, int(pending.get("eventSeq", 0))), clock, state)
    yield from _drive(segment, request.execution_root, request.project_id, request.run_id, Command(resume={"approved": True}))


def run_cancel(run_id: str, project_id: str = "", execution_root: str = "") -> None:
    if not execution_root or not project_id:
        return
    try:
        drop(execution_root, project_id, run_id)
    except ExecutionError:
        return


def build(segment: Segment) -> StateGraph:
    graph = StateGraph(State)
    graph.add_node("analyze_request", budgeted(segment, request_analyzer))
    graph.add_node("build_context", budgeted(segment, context_builder))
    graph.add_node("retrieve", budgeted(segment, lambda state: rag_research(state, segment)))
    graph.add_node("plan", budgeted(segment, lambda state: planning_agent(state, segment)))
    graph.add_node("validate_plan", budgeted(segment, plan_validator))
    # Depois do plano o limite de etapas não conta, mas tempo e tokens valem até o fim, inclusive depois da pausa.
    graph.add_node("architecture", budgeted(segment, lambda state: architecture_agent(state, segment), counts_iteration=False))
    graph.add_node("check_impact", budgeted(segment, lambda state: propose_actions(impact_eval(state)), counts_iteration=False))
    graph.add_node("approval", budgeted(segment, lambda state: approval_gate(state, segment), counts_iteration=False))
    graph.add_node("synthesize", budgeted(segment, synthesizer, counts_iteration=False))
    graph.add_node("evaluate", budgeted(segment, lambda state: evaluation_judge(state, segment), counts_iteration=False))

    graph.add_edge(START, "analyze_request")
    for current, following in (("analyze_request", "build_context"), ("build_context", "retrieve"), ("retrieve", "plan"), ("plan", "validate_plan")):
        graph.add_conditional_edges(current, lambda state, nxt=following: END if state.get("failure") else nxt, [following, END])
    graph.add_conditional_edges(
        "validate_plan",
        lambda state: END if state.get("failure") else ("architecture" if state["asks_impact"] else "evaluate"),
        ["architecture", "evaluate", END],
    )
    graph.add_edge("architecture", "check_impact")
    # O juiz roda antes da pausa: quem aprova já vê a opinião, e a retomada não precisa do proxy de modelo.
    graph.add_edge("check_impact", "evaluate")
    graph.add_edge("evaluate", "approval")
    graph.add_edge("approval", "synthesize")
    graph.add_edge("synthesize", END)
    return graph


def _drive(segment: Segment, execution_root: str, project_id: str, run_id: str, start: dict | Command) -> Iterator[dict]:
    config = {"configurable": {"thread_id": run_id}}
    try:
        with (saver(execution_root, project_id, run_id) if execution_root else nullcontext(InMemorySaver())) as checkpoints:
            app = build(segment).compile(checkpointer=checkpoints)
            for mode, chunk in app.stream(start, config, stream_mode=["custom", "updates"], durability="exit"):
                if mode == "custom":
                    yield chunk
            snapshot = app.get_state(config)
    except ExecutionError:
        blank = blank_state(project_id, run_id, segment.emitter.execution_id)
        yield failed_ack(blank, "CHECKPOINT_UNAVAILABLE", "Sem memória de execução para pausar.")
        return
    if snapshot.interrupts:
        yield build_ack(snapshot.values, "interrupted", snapshot.interrupts[0].value)
        return
    # Uma execução que terminou não deixa checkpoint: o que sobra no disco é só o que está pausado.
    run_cancel(run_id, project_id, execution_root)
    yield build_ack(snapshot.values, "failed" if snapshot.values.get("failure") else "completed")


def _paused(request: ResumeRequest) -> tuple[dict, dict] | None:
    if not request.execution_root:
        return None
    config = {"configurable": {"thread_id": request.run_id}}
    try:
        with saver(request.execution_root, request.project_id, request.run_id) as checkpoints:
            snapshot = build(Segment(Emitter(request.execution_id))).compile(checkpointer=checkpoints).get_state(config)
    except ExecutionError:
        return None
    if not snapshot.interrupts:
        return None
    pending = snapshot.interrupts[0].value
    values = snapshot.values
    if not isinstance(pending, dict) or pending.get("interruptId") != request.interrupt_id:
        return None
    if values.get("execution_id") != request.execution_id or not checkpoint_ok(values, request.project_id, request.run_id):
        return None
    return values, pending
