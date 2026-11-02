"""Grafo engineering.request (versão 1 do comportamento) em LangGraph.

Nós determinísticos: nenhum modelo é chamado e nada é gravado no projeto. A busca usa só as fontes que o motor enviou,
e o texto dessas fontes não entra no estado nem no checkpoint.

A memória de trabalho é o estado do grafo. A pausa usa ``interrupt()`` e vai para a memória de execução (checkpoint em
SQLite, fora do índice e fora do projeto), para a retomada sobreviver a um reinício. O diário de negócio fica no motor.

``run_start`` e ``run_resume`` devolvem um iterador: eventos ai.event.v1 enquanto o grafo anda e, por último, o ai.ack.v2.
"""

from __future__ import annotations

import hashlib
import time
from contextlib import nullcontext
from typing import Callable, Iterator, TypedDict

from langgraph.checkpoint.memory import InMemorySaver
from langgraph.graph import END, START, StateGraph
from langgraph.types import Command, interrupt

from contracts.messages import ContractError, ResumeRequest, StartRequest, parse_resume, parse_start
from infrastructure.events import Emitter, now
from knowledge.index import KnowledgeError, search, sync
from memory.execution import ExecutionError, drop, saver

WORKFLOW_ID = "engineering.request"
WORKFLOW_VERSION = "1"
MAX_ITERATIONS = 8
MAX_MILLISECONDS = 2000
MAX_TOKENS = 0
_FORBIDDEN = {"command", "commands", "executable", "arguments", "shell", "write", "path"}
_AREAS = {"servico", "interface", "projeto"}
_AREA_BY_STACK = {
    "dotnet": "servico",
    "javascript": "interface",
    "typescript": "interface",
    "node": "interface",
    "flutter": "interface",
}
_RISKS = {"sem-evidencia", "sem-stack"}
_ARCHITECTURE = "architecture"
_AGENT_VERSION = "1"
_EVALUATION = {"schema": "engineering.evaluation.v1", "verdict": "deterministic", "judge": "not_run", "tokens": 0}


class State(TypedDict, total=False):
    run_id: str
    project_id: str
    execution_id: str
    started_at: str
    goal: str
    requires_approval: bool
    asks_impact: bool
    approved: bool
    stacks: list[str]
    knowledge_root: str
    evidence: list[dict]
    retrieval_attempted: bool
    limitations: list[dict]
    failure: str
    plan: dict | None
    impact: dict | None
    agents: list[dict]
    evaluation: dict | None
    tokens: int
    valid: bool
    summary: str
    iterations: int


class Segment:
    """O que vale só para um trecho da execução: relógio, limites, fontes e eventos. Nada disso vai para o checkpoint."""

    def __init__(self, emitter: Emitter, clock: Callable[[], float] | None = None, max_iterations: int = MAX_ITERATIONS,
                 max_milliseconds: int = MAX_MILLISECONDS, sources: list | None = None) -> None:
        self.emitter = emitter
        self.clock = clock or time.monotonic
        self.started = self.clock()
        self.max_iterations = max_iterations
        self.max_milliseconds = max_milliseconds
        self.sources = sources


def run_start(payload: dict, clock: Callable[[], float] | None = None) -> Iterator[dict]:
    try:
        request = parse_start(payload)
    except ContractError as ex:
        yield _rejected(payload, str(ex))
        return
    emitter = Emitter(request.execution_id)
    segment = Segment(
        emitter,
        clock,
        min(MAX_ITERATIONS, request.limits.max_iterations),
        min(MAX_MILLISECONDS, request.limits.max_milliseconds),
        request.sources,
    )
    yield emitter.event("workflow.started")
    initial = _initial(request)
    if request.requires_approval and not request.execution_root:
        yield _ack(_fail(initial, "CHECKPOINT_UNAVAILABLE", "Sem memória de execução para pausar."), "failed")
        return
    yield from _drive(segment, request.execution_root, request.project_id, request.run_id, initial)


def run_resume(payload: dict, clock: Callable[[], float] | None = None) -> Iterator[dict]:
    try:
        request = parse_resume(payload)
    except ContractError as ex:
        yield _rejected(payload, str(ex))
        return
    paused = _paused(request)
    if paused is None:
        yield _lost(request)
        return
    state, pending = paused
    if not request.approved:
        yield _ack(state, "interrupted", pending)
        return
    segment = Segment(Emitter(request.execution_id, int(pending.get("eventSeq", 0))), clock)
    yield from _drive(segment, request.execution_root, request.project_id, request.run_id, Command(resume={"approved": True}))


def run_cancel(run_id: str, project_id: str = "", execution_root: str = "") -> None:
    if not execution_root or not project_id:
        return
    try:
        drop(execution_root, project_id, run_id)
    except ExecutionError:
        return


def request_analyzer(state: dict) -> dict:
    if not state["goal"]:
        _limit(state, "REQUEST_EMPTY", "O pedido veio sem objetivo.")
    return state


def context_builder(state: dict) -> dict:
    # Só aceita identificadores curtos vindos do motor. Texto livre do pedido não entra aqui.
    return state


def rag_research(state: dict, segment: Segment) -> dict:
    state["evidence"] = []
    state["retrieval_attempted"] = False
    root = state["knowledge_root"]
    if not root or segment.sources is None:
        _limit(state, "RAG_NOT_READY", "A recuperação de contexto ainda não indexa o projeto.")
        return state
    segment.emitter.emit("context.retrieval.started", {"strategy": ["lexical"]})
    try:
        stats = sync(root, state["project_id"], segment.sources)
        hits = search(root, state["project_id"], state["goal"])
    except KnowledgeError:
        _limit(state, "RAG_NOT_READY", "A recuperação de contexto ainda não indexa o projeto.")
        return state
    state["retrieval_attempted"] = True
    state["evidence"] = hits
    for hit in hits:
        segment.emitter.emit("context.retrieved", {
            "documentId": hit["documentId"],
            "chunkId": hit["chunkId"],
            "filePath": hit["filePath"],
            "hash": hit["hash"],
            "score": hit["score"],
            "retriever": "lexical",
        })
    _limit(state, "EMBEDDINGS_NOT_READY", "A busca é lexical. Embeddings ficam para quando houver uma baseline de recall.")
    if not hits:
        if stats["documents"] == 0:
            _limit(state, "RAG_EMPTY", "Não há fontes indexáveis neste projeto.")
        else:
            _limit(state, "RAG_NO_HITS", "Nenhum trecho do projeto corresponde ao pedido.")
    return state


def planning_agent(state: dict) -> dict:
    steps = [{"id": "describe", "title": "Descrever o pedido"}]
    if state["stacks"]:
        steps.append({"id": "context", "title": "Considerar a stack detectada"})
    if state["evidence"]:
        steps.append({"id": "evidence", "title": "Usar trechos recuperados"})
    if state["asks_impact"]:
        steps.append({"id": "impact", "title": "Avaliar o impacto técnico"})
    state["plan"] = {"schema": "engineering.plan.v1", "steps": steps}
    return state


def plan_validator(state: dict) -> dict:
    plan = state.get("plan")
    steps = plan.get("steps") if isinstance(plan, dict) else None
    valid = (
        isinstance(plan, dict)
        and plan.get("schema") == "engineering.plan.v1"
        and isinstance(steps, list)
        and bool(steps)
        and not _forbidden(plan)
        and all(isinstance(step, dict) and _text(step.get("id")) and _text(step.get("title")) for step in steps)
    )
    state["valid"] = valid
    if not valid:
        _fail(state, "PLAN_INVALID", "O plano não respeita o contrato.")
    return state


def architecture_agent(state: dict, segment: Segment | None = None) -> dict:
    """Um agente, só quando o plano pediu impacto. Não chama modelo e não grava no projeto."""
    started = time.monotonic()
    if segment:
        segment.emitter.emit("agent.started", {"agentVersion": _AGENT_VERSION}, _ARCHITECTURE)
    areas: list[str] = []
    for stack in state["stacks"]:
        area = _AREA_BY_STACK.get(stack)
        if area and area not in areas:
            areas.append(area)
    if not areas:
        areas.append("projeto")
    risks: list[str] = []
    if not state["evidence"]:
        risks.append("sem-evidencia")
    if not state["stacks"]:
        risks.append("sem-stack")
    refs: list[str] = []
    for item in state["evidence"]:
        chunk = item.get("chunkId") if isinstance(item, dict) else None
        if isinstance(chunk, str) and chunk and chunk not in refs:
            refs.append(chunk)
        if len(refs) == 8:
            break
    duration = int((time.monotonic() - started) * 1000)
    state["impact"] = {"schema": "engineering.impact.v1", "areas": areas, "risks": risks, "evidence": refs}
    state["agents"] = [{"agentId": _ARCHITECTURE, "agentVersion": _AGENT_VERSION, "status": "completed", "durationMs": duration, "tokens": 0}]
    if segment:
        segment.emitter.emit("agent.completed", {"status": "completed", "durationMs": duration, "tokens": 0}, _ARCHITECTURE)
    return state


def impact_eval(state: dict) -> dict:
    if _impact_ok(state.get("impact"), state.get("evidence")):
        return state
    state["impact"] = None
    state["agents"] = []
    _limit(state, "IMPACT_INVALID", "O impacto técnico não respeita o contrato.")
    return state


def approval_gate(state: dict, segment: Segment) -> dict:
    if not state["requires_approval"] or state.get("approved"):
        return state
    decision = interrupt({
        "interruptId": interrupt_id(state["execution_id"]),
        "actionIds": [],
        "eventSeq": segment.emitter.next_seq,
    })
    state["approved"] = isinstance(decision, dict) and decision.get("approved") is True
    return state


def synthesizer(state: dict) -> dict:
    count = len(state["plan"]["steps"]) if isinstance(state.get("plan"), dict) else 0
    detected = len(state["stacks"])
    found = len(state["evidence"])
    state["summary"] = f"{count} etapa(s). Stacks detectadas: {detected}. Trechos: {found}. Nenhuma alteração no projeto."
    return state


def evaluation_judge(state: dict) -> dict:
    # Orçamento de tokens é zero: o juiz não chama modelo e não vira o veredito.
    state["tokens"] = 0
    _limit(state, "JUDGE_NOT_RUN", "O juiz por modelo não é o veredito desta fase.")
    state["evaluation"] = dict(_EVALUATION)
    return state


def interrupt_id(execution_id: str) -> str:
    return "int-" + hashlib.sha256(f"{execution_id}:approval".encode("utf-8")).hexdigest()[:12]


def build(segment: Segment) -> StateGraph:
    graph = StateGraph(State)
    graph.add_node("analyze_request", _budgeted(segment, request_analyzer))
    graph.add_node("build_context", _budgeted(segment, context_builder))
    graph.add_node("retrieve", _budgeted(segment, lambda state: rag_research(state, segment)))
    graph.add_node("plan", _budgeted(segment, planning_agent))
    graph.add_node("validate_plan", _budgeted(segment, plan_validator))
    graph.add_node("architecture", lambda state: architecture_agent(_copy(state), segment))
    graph.add_node("check_impact", lambda state: impact_eval(_copy(state)))
    graph.add_node("approval", lambda state: approval_gate(_copy(state), segment))
    graph.add_node("synthesize", lambda state: synthesizer(_copy(state)))
    graph.add_node("evaluate", lambda state: evaluation_judge(_copy(state)))

    graph.add_edge(START, "analyze_request")
    for current, following in (("analyze_request", "build_context"), ("build_context", "retrieve"), ("retrieve", "plan"), ("plan", "validate_plan")):
        graph.add_conditional_edges(current, lambda state, nxt=following: END if state.get("failure") else nxt, [following, END])
    graph.add_conditional_edges(
        "validate_plan",
        lambda state: END if state.get("failure") else ("architecture" if state["asks_impact"] else "approval"),
        ["architecture", "approval", END],
    )
    graph.add_edge("architecture", "check_impact")
    graph.add_edge("check_impact", "approval")
    graph.add_edge("approval", "synthesize")
    graph.add_edge("synthesize", "evaluate")
    graph.add_edge("evaluate", END)
    return graph


def checkpoint_ok(state: object, project_id: str, run_id: str) -> bool:
    """O checkpoint vem do disco: não volta se mudou de projeto, se perdeu a pausa ou se carrega comando."""
    if not isinstance(state, dict):
        return False
    if state.get("project_id") != project_id or state.get("run_id") != run_id or state.get("approved") is True:
        return False
    if not isinstance(state.get("stacks"), list) or not isinstance(state.get("limitations"), list):
        return False
    evidence = state.get("evidence")
    if not isinstance(evidence, list) or any(not isinstance(item, dict) for item in evidence):
        return False
    if _forbidden(state.get("plan")) or _forbidden(state.get("impact")):
        return False
    if not _agents_ok(state.get("agents") or []):
        return False
    if state.get("impact") is not None and not _impact_ok(state.get("impact"), evidence):
        return False
    clone = dict(state)
    clone["limitations"] = list(state["limitations"])
    return plan_validator(clone).get("valid") is True


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
        blank = _blank(project_id, run_id, segment.emitter.execution_id)
        yield _ack(_fail(blank, "CHECKPOINT_UNAVAILABLE", "Sem memória de execução para pausar."), "failed")
        return
    if snapshot.interrupts:
        yield _ack(snapshot.values, "interrupted", snapshot.interrupts[0].value)
        return
    run_cancel(run_id, project_id, execution_root)
    yield _ack(snapshot.values, "failed" if snapshot.values.get("failure") else "completed")


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


def _budgeted(segment: Segment, node: Callable[[dict], dict]) -> Callable[[dict], dict]:
    def run(state: dict) -> dict:
        state = node(_copy(state))
        state["iterations"] = int(state.get("iterations") or 0) + 1
        if state.get("failure"):
            return state
        if state["iterations"] > segment.max_iterations:
            _fail(state, "LIMIT_ITERATIONS", "A execução passou do limite de etapas.")
        elif (segment.clock() - segment.started) * 1000 > segment.max_milliseconds:
            _fail(state, "LIMIT_TIME", "A execução passou do limite de tempo.")
        elif int(state.get("tokens") or 0) > MAX_TOKENS:
            _fail(state, "LIMIT_TOKENS", "A execução passou do limite de tokens.")
        return state

    return run


def _initial(request: StartRequest) -> dict:
    return {
        "run_id": request.run_id,
        "project_id": request.project_id,
        "execution_id": request.execution_id,
        "started_at": now(),
        "goal": request.goal,
        "requires_approval": request.requires_approval,
        "asks_impact": request.impact,
        "approved": False,
        "stacks": list(request.stacks),
        "knowledge_root": request.knowledge_root,
        "evidence": [],
        "retrieval_attempted": False,
        "limitations": [],
        "failure": "",
        "plan": None,
        "impact": None,
        "agents": [],
        "evaluation": None,
        "tokens": 0,
        "valid": False,
        "summary": "",
        "iterations": 0,
    }


def _blank(project_id: str, run_id: str, execution_id: str) -> dict:
    return {
        "run_id": run_id, "project_id": project_id, "execution_id": execution_id, "started_at": now(), "stacks": [],
        "evidence": [], "retrieval_attempted": False, "limitations": [], "failure": "", "plan": None, "impact": None,
        "agents": [], "evaluation": None, "valid": False, "summary": "",
    }


def _copy(state: dict) -> dict:
    copied = dict(state)
    copied["limitations"] = list(state.get("limitations") or [])
    return copied


def _forbidden(value: object) -> bool:
    if isinstance(value, dict):
        return any(key in _FORBIDDEN or _forbidden(child) for key, child in value.items())
    if isinstance(value, list):
        return any(_forbidden(item) for item in value)
    return False


def _limit(state: dict, code: str, message: str) -> None:
    limitations = state.get("limitations") or []
    if any(item["code"] == code for item in limitations):
        return
    state["limitations"] = [*limitations, {"code": code, "message": message}]


def _fail(state: dict, code: str, message: str) -> dict:
    state["valid"] = False
    state["failure"] = code
    _limit(state, code, message)
    return state


def _impact_ok(impact: object, evidence: object) -> bool:
    if not isinstance(impact, dict) or impact.get("schema") != "engineering.impact.v1":
        return False
    if _forbidden(impact) or set(impact) != {"schema", "areas", "risks", "evidence"}:
        return False
    areas = impact.get("areas")
    risks = impact.get("risks")
    refs = impact.get("evidence")
    if not isinstance(areas, list) or not areas or any(area not in _AREAS for area in areas):
        return False
    if not isinstance(risks, list) or any(risk not in _RISKS for risk in risks):
        return False
    if not isinstance(refs, list):
        return False
    known = set()
    if isinstance(evidence, list):
        for item in evidence:
            if isinstance(item, dict) and isinstance(item.get("chunkId"), str):
                known.add(item["chunkId"])
    return all(isinstance(ref, str) and ref in known for ref in refs)


def _agents_ok(agents: object) -> bool:
    if not isinstance(agents, list):
        return False
    return all(
        isinstance(item, dict) and item.get("agentId") == _ARCHITECTURE and item.get("status") == "completed"
        for item in agents
    )


def _ack(state: dict, status: str, pending: dict | None = None) -> dict:
    failure = state.get("failure") or ""
    limitations = state.get("limitations") or []
    plan = state["plan"] if state.get("valid") and isinstance(state.get("plan"), dict) else {"schema": "engineering.plan.v1", "steps": []}
    output = {
        "summary": state.get("summary") or "",
        "plan": plan,
        "stacks": list(state.get("stacks") or []),
        "evidence": [_public_evidence(item) for item in state.get("evidence") or []],
        "retrievalAttempted": bool(state.get("retrieval_attempted")),
    }
    if _impact_ok(state.get("impact"), state.get("evidence")):
        output["impact"] = state["impact"]
    if state.get("evaluation") == _EVALUATION:
        output["evaluation"] = dict(_EVALUATION)
    ack = {
        "schema": "ai.ack.v2",
        "workflowId": WORKFLOW_ID,
        "workflowVersion": WORKFLOW_VERSION,
        "runId": state.get("run_id") or "",
        "executionId": state.get("execution_id") or "",
        "projectId": state.get("project_id") or "",
        "status": status,
        "startedAt": state.get("started_at") or now(),
        "completedAt": now(),
        "warnings": [item for item in limitations if item["code"] != failure],
        "errors": [item for item in limitations if failure and item["code"] == failure],
        "output": output,
        "agents": [dict(item) for item in state.get("agents") or [] if _agents_ok([item])],
        "usage": {"modelCalls": 0, "inputTokens": 0, "outputTokens": 0, "costUsd": 0},
        "nextActions": [],
    }
    if status == "interrupted" and pending:
        ack["approval"] = {"interruptId": pending["interruptId"], "actionIds": list(pending.get("actionIds") or [])}
    return ack


def _lost(request: ResumeRequest) -> dict:
    blank = _blank(request.project_id, request.run_id, request.execution_id)
    return _ack(_fail(blank, "CHECKPOINT_LOST", "A execução pausada não está mais disponível."), "failed")


def _rejected(payload: object, field: str) -> dict:
    body = payload if isinstance(payload, dict) else {}
    blank = _blank(_text(body.get("projectId")), _text(body.get("runId")), _text(body.get("executionId")))
    return _ack(_fail(blank, "CONTRACT_INVALID", f"A mensagem do motor não respeita o contrato ({field})."), "failed")


def _public_evidence(item: dict) -> dict:
    return {
        "documentId": item.get("documentId", ""),
        "chunkId": item.get("chunkId", ""),
        "filePath": item.get("filePath", ""),
        "hash": item.get("hash", ""),
        "score": item.get("score", 0),
        "excerpt": item.get("excerpt", ""),
    }


def _text(value: object) -> str:
    return value if isinstance(value, str) else ""
