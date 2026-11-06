"""Grafo engineering.request (versão 1 do comportamento) em LangGraph.

Os nós são determinísticos. Só quando o motor autoriza modelo (``policy.modelAllowed``) e dá o endereço do proxy, o
Planning e o Architecture tentam o modelo; se a saída não passar na validação ou o orçamento acabar, o resultado
determinístico fica no lugar e uma limitação diz por quê. Nada é gravado no projeto. A busca usa só as fontes que o
motor enviou, e o texto dessas fontes não entra no estado nem no checkpoint. O token do proxy fica só no ``Segment``.

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

from agents import architecture as architecture_model
from agents import judge as judge_model
from agents import planning as planning_model
from agents.model_client import ModelClient
from agents.runner import Model, Outcome
from contracts.engineering import ACTION_FILE_WRITE, CRITERIA_VERSION, JUDGE_EVAL_ID, actions_ok, decision_action, judgment_ok, judgment_outcome
from contracts.engineering import forbidden as _forbidden
from contracts.engineering import impact_ok as _impact_ok
from contracts.messages import ContractError, ResumeRequest, StartRequest, parse_resume, parse_start
from infrastructure.events import Emitter, now
from knowledge import retrieval
from knowledge.index import KnowledgeError, sync
from memory.execution import ExecutionError, drop, saver

WORKFLOW_ID = "engineering.request"
WORKFLOW_VERSION = "1"
MAX_ITERATIONS = 8
MAX_MILLISECONDS = 2000
# Com modelo, uma chamada leva segundos: o tempo e os tokens sobem, mas o motor continua sendo quem define o teto.
MAX_MILLISECONDS_MODEL = 60000
MAX_TOKENS = 20000
_AREA_BY_STACK = {
    "dotnet": "servico",
    "javascript": "interface",
    "typescript": "interface",
    "node": "interface",
    "flutter": "interface",
}
_ARCHITECTURE = "architecture"
_PLANNING = planning_model.AGENT_ID
_AGENTS = {_PLANNING, _ARCHITECTURE}
_MODEL_LIMITATIONS = {
    "MODEL_BUDGET_EXCEEDED": "O orçamento de tokens da execução acabou.",
    "MODEL_TIMEOUT": "O modelo não respondeu no prazo.",
    "MODEL_UNAVAILABLE": "O modelo não está disponível.",
    "MODEL_NOT_CONFIGURED": "Nenhum modelo está configurado.",
    "MODEL_FORBIDDEN": "O proxy de modelo recusou a chamada.",
    "MODEL_OUTPUT_INVALID": "O modelo respondeu fora do contrato, mesmo depois de uma nova tentativa.",
}
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
    judgment: dict | None
    allowed_actions: list[str]
    actions: list[dict]
    tokens: int
    model_calls: int
    input_tokens: int
    output_tokens: int
    valid: bool
    summary: str
    iterations: int


class Segment:
    """O que vale só para um trecho da execução: relógio, limites, fontes e eventos. Nada disso vai para o checkpoint."""

    def __init__(self, emitter: Emitter, clock: Callable[[], float] | None = None, max_iterations: int = MAX_ITERATIONS,
                 max_milliseconds: int = MAX_MILLISECONDS, sources: list | None = None, model: Model | None = None,
                 max_tokens: int = 0) -> None:
        self.emitter = emitter
        self.clock = clock or time.monotonic
        self.started = self.clock()
        self.max_iterations = max_iterations
        self.max_milliseconds = max_milliseconds
        self.sources = sources
        self.model = model
        self.max_tokens = max_tokens


def broker_client(request: StartRequest) -> Model:
    return ModelClient(request.broker_url, request.broker_token, request.execution_id)


def run_start(payload: dict, clock: Callable[[], float] | None = None,
              model_factory: Callable[[StartRequest], Model] | None = None) -> Iterator[dict]:
    """``model_factory`` troca o cliente do proxy (os testes usam um modelo falso); o padrão fala com o proxy do motor."""
    try:
        request = parse_start(payload)
    except ContractError as ex:
        yield _rejected(payload, str(ex))
        return
    emitter = Emitter(request.execution_id)
    model = (model_factory or broker_client)(request) if request.model_allowed else None
    # O motor define os limites; o runtime só pode ser mais restritivo. O teto de tokens só existe com modelo.
    ceiling = MAX_MILLISECONDS_MODEL if model else MAX_MILLISECONDS
    segment = Segment(
        emitter,
        clock,
        min(MAX_ITERATIONS, request.limits.max_iterations),
        min(ceiling, request.limits.max_milliseconds),
        request.sources,
        model,
        min(MAX_TOKENS, request.limits.max_tokens) if model else 0,
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
    proposed = set(pending.get("actionIds") or [])
    if not set(request.action_ids) <= proposed:
        # Aprovar o que nunca foi proposto não retoma nada: a pausa continua onde estava.
        yield _ack(_fail(dict(state), "APPROVAL_MISMATCH", "A aprovação cita ações que não foram propostas."), "failed")
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
    segment.emitter.emit("context.retrieval.started", {"strategy": ["lexical", "symbol"]})
    try:
        stats = sync(root, state["project_id"], segment.sources)
        hits = retrieval.search(root, state["project_id"], state["goal"])
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
            "retriever": hit.get("retriever", "lexical"),
        })
    _limit(state, "EMBEDDINGS_NOT_READY", "A busca é híbrida (texto e símbolos). Embeddings ficam para quando a avaliação mostrar ganho.")
    if not stats["valid"]:
        _limit(state, "INDEX_INVALID", "Parte do índice não passou na validação e será refeita na próxima execução.")
    if stats["pending"]:
        _limit(state, "INDEX_PENDING", "Parte do projeto ainda não foi indexada; entra nas próximas execuções.")
    if not hits:
        if stats["documents"] == 0:
            _limit(state, "RAG_EMPTY", "Não há fontes indexáveis neste projeto.")
        else:
            _limit(state, "RAG_NO_HITS", "Nenhum trecho do projeto corresponde ao pedido.")
    return state


def planning_agent(state: dict, segment: Segment | None = None) -> dict:
    """Plano determinístico sempre; com modelo autorizado, o do modelo o substitui se passar na validação."""
    steps = [{"id": "describe", "title": "Descrever o pedido"}]
    if state["stacks"]:
        steps.append({"id": "context", "title": "Considerar a stack detectada"})
    if state["evidence"]:
        steps.append({"id": "evidence", "title": "Usar trechos recuperados"})
    if state["asks_impact"]:
        steps.append({"id": "impact", "title": "Avaliar o impacto técnico"})
    state["plan"] = {"schema": "engineering.plan.v1", "steps": steps}

    if segment is not None and segment.model is not None:
        def adopt(plan: dict) -> None:
            state["plan"] = plan

        _model_step(state, segment, _PLANNING, planning_model.AGENT_VERSION,
                    lambda can_spend: planning_model.plan(segment.model, state, can_spend), adopt,
                    "PLAN_MODEL_FALLBACK", "O plano veio das regras determinísticas, não do modelo.")
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
    """Um agente, só quando o plano pediu impacto. Determinístico; com modelo autorizado, o do modelo o substitui se passar."""
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
    state["impact"] = {"schema": "engineering.impact.v1", "areas": areas, "risks": risks, "evidence": refs}
    tokens = 0
    if segment is not None and segment.model is not None:
        def adopt(impact: dict) -> None:
            state["impact"] = impact

        tokens = _model_call(state, segment, lambda can_spend: architecture_model.assess(segment.model, state, can_spend), adopt,
                             "IMPACT_MODEL_FALLBACK", "O impacto veio das regras determinísticas, não do modelo.")
    duration = int((time.monotonic() - started) * 1000)
    _record_agent(state, _ARCHITECTURE, duration, tokens)
    if segment:
        segment.emitter.emit("agent.completed", {"status": "completed", "durationMs": duration, "tokens": tokens}, _ARCHITECTURE)
    return state


def impact_eval(state: dict) -> dict:
    if _impact_ok(state.get("impact"), state.get("evidence")):
        return state
    state["impact"] = None
    state["agents"] = [item for item in state.get("agents") or [] if item.get("agentId") != _ARCHITECTURE]
    _limit(state, "IMPACT_INVALID", "O impacto técnico não respeita o contrato.")
    return state


def propose_actions(state: dict) -> dict:
    """
    Proposta de ação, nunca execução: só com a política do motor permitindo ``file.write``, com pausa para aprovação e
    com impacto já validado. O agente não escolhe o alvo nem escreve o conteúdo; o motor decide, aprova e executa.
    """
    state["actions"] = []
    if (ACTION_FILE_WRITE in (state.get("allowed_actions") or []) and state["requires_approval"]
            and _impact_ok(state.get("impact"), state.get("evidence"))):
        state["actions"] = [decision_action(state["execution_id"], state["impact"])]
    return state


def approval_gate(state: dict, segment: Segment) -> dict:
    if not state["requires_approval"] or state.get("approved"):
        return state
    decision = interrupt({
        "interruptId": interrupt_id(state["execution_id"]),
        "actionIds": [action["id"] for action in state.get("actions") or []],
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


def evaluation_judge(state: dict, segment: Segment | None = None) -> dict:
    """
    O veredito é sempre determinístico. Com modelo autorizado, um juiz independente registra uma opinião sobre o plano e o
    impacto finais (EV-JUDGE-001); ela não volta para o plano, não muda o veredito e, se falhar, só deixa uma limitação.
    """
    state["evaluation"] = dict(_EVALUATION)
    state["judgment"] = None
    judged = segment is not None and segment.model is not None and _judge_with_model(state, segment)
    if not judged:
        _limit(state, "JUDGE_NOT_RUN", "O juiz por modelo não é o veredito desta fase.")
    return state


def _judge_with_model(state: dict, segment: Segment) -> bool:
    segment.emitter.emit("eval.started", {"evalId": JUDGE_EVAL_ID, "kind": "model"})
    outcomes: list[Outcome] = []

    def call(can_spend: Callable[[int], bool]) -> Outcome:
        outcomes.append(judge_model.judge(segment.model, state, can_spend))
        return outcomes[-1]

    def adopt(judgment: dict) -> None:
        state["judgment"] = {**judgment, "model": outcomes[-1].model, "modelVersion": outcomes[-1].model_version}

    tokens = _model_call(state, segment, call, adopt, "JUDGE_NOT_RUN", "O juiz por modelo não rodou; o veredito segue determinístico.")
    judgment = state.get("judgment")
    verdict = judgment_outcome(judgment["scores"]) if judgment else "not_run"
    state["evaluation"] = {**_EVALUATION, "judge": verdict, "tokens": tokens}
    data = {"evalId": JUDGE_EVAL_ID, "kind": "model", "outcome": verdict}
    if judgment:
        data["criteriaVersion"] = CRITERIA_VERSION
    segment.emitter.emit("eval.completed", data)
    return judgment is not None


def interrupt_id(execution_id: str) -> str:
    return "int-" + hashlib.sha256(f"{execution_id}:approval".encode("utf-8")).hexdigest()[:12]


def build(segment: Segment) -> StateGraph:
    graph = StateGraph(State)
    graph.add_node("analyze_request", _budgeted(segment, request_analyzer))
    graph.add_node("build_context", _budgeted(segment, context_builder))
    graph.add_node("retrieve", _budgeted(segment, lambda state: rag_research(state, segment)))
    graph.add_node("plan", _budgeted(segment, lambda state: planning_agent(state, segment)))
    graph.add_node("validate_plan", _budgeted(segment, plan_validator))
    graph.add_node("architecture", lambda state: architecture_agent(_copy(state), segment))
    graph.add_node("check_impact", lambda state: propose_actions(impact_eval(_copy(state))))
    graph.add_node("approval", lambda state: approval_gate(_copy(state), segment))
    graph.add_node("synthesize", lambda state: synthesizer(_copy(state)))
    graph.add_node("evaluate", lambda state: evaluation_judge(_copy(state), segment))

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
    if not _actions_ok(state) or (state.get("actions") and state.get("impact") is None):
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
        elif int(state.get("tokens") or 0) > segment.max_tokens:
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
        "allowed_actions": list(request.allowed_actions),
        "actions": [],
        "tokens": 0,
        "model_calls": 0,
        "input_tokens": 0,
        "output_tokens": 0,
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


def _model_call(state: dict, segment: Segment, call: Callable[[Callable[[int], bool]], Outcome],
                adopt: Callable[[dict], None], fallback_code: str, fallback_message: str) -> int:
    """
    Uma tentativa com modelo: o que o modelo escreveu entra só se passou na validação; senão o resultado determinístico
    fica e duas limitações dizem o motivo. Devolve os tokens gastos, que entram na contagem da execução.
    """
    spent = int(state.get("tokens") or 0)
    outcome = call(lambda needed: spent + needed <= segment.max_tokens)
    state["model_calls"] = int(state.get("model_calls") or 0) + outcome.calls
    state["input_tokens"] = int(state.get("input_tokens") or 0) + outcome.input_tokens
    state["output_tokens"] = int(state.get("output_tokens") or 0) + outcome.output_tokens
    state["tokens"] = spent + outcome.tokens
    if outcome.value is not None:
        adopt(outcome.value)
    else:
        _limit(state, outcome.code, _MODEL_LIMITATIONS.get(outcome.code, "O modelo não pôde ser usado."))
        _limit(state, fallback_code, fallback_message)
    return outcome.tokens


def _model_step(state: dict, segment: Segment, agent_id: str, version: str, call: Callable[[Callable[[int], bool]], Outcome],
                adopt: Callable[[dict], None], fallback_code: str, fallback_message: str) -> None:
    """Um agente só com modelo (Planning): anuncia o começo e o fim e entra na lista de agentes com os tokens gastos."""
    started = time.monotonic()
    segment.emitter.emit("agent.started", {"agentVersion": version}, agent_id)
    tokens = _model_call(state, segment, call, adopt, fallback_code, fallback_message)
    duration = int((time.monotonic() - started) * 1000)
    _record_agent(state, agent_id, duration, tokens)
    segment.emitter.emit("agent.completed", {"status": "completed", "durationMs": duration, "tokens": tokens}, agent_id)


def _record_agent(state: dict, agent_id: str, duration: int, tokens: int) -> None:
    others = [item for item in state.get("agents") or [] if item.get("agentId") != agent_id]
    entry = {"agentId": agent_id, "agentVersion": _AGENT_VERSION, "status": "completed", "durationMs": duration, "tokens": tokens}
    state["agents"] = [*others, entry] if agent_id == _ARCHITECTURE else [entry, *others]


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


def _agents_ok(agents: object) -> bool:
    if not isinstance(agents, list):
        return False
    return all(
        isinstance(item, dict) and item.get("agentId") in _AGENTS and item.get("status") == "completed"
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
    if (evaluation := _public_evaluation(state)) is not None:
        output["evaluation"] = evaluation
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
        # O que o runtime contou; quem vale é o proxy do motor, que conta por conta própria. Sem custo em dinheiro (D4).
        "usage": {
            "modelCalls": int(state.get("model_calls") or 0),
            "inputTokens": int(state.get("input_tokens") or 0),
            "outputTokens": int(state.get("output_tokens") or 0),
            "costUsd": 0,
        },
        "nextActions": [dict(item) for item in state.get("actions") or []] if _actions_ok(state) else [],
    }
    if status == "interrupted" and pending:
        ack["approval"] = {"interruptId": pending["interruptId"], "actionIds": list(pending.get("actionIds") or [])}
    return ack


def _actions_ok(state: dict) -> bool:
    return actions_ok(state.get("actions") or [], state.get("execution_id") or "", state.get("evidence"))


def _public_evaluation(state: dict) -> dict | None:
    """O que sai no ack: o veredito determinístico e, só se passar na conferência, a opinião do juiz."""
    evaluation = state.get("evaluation")
    if not isinstance(evaluation, dict) or evaluation.get("verdict") != "deterministic":
        return None
    tokens = evaluation.get("tokens")
    public = {**_EVALUATION, "tokens": tokens if isinstance(tokens, int) and not isinstance(tokens, bool) and tokens >= 0 else 0}
    judgment = state.get("judgment")
    judge_accepted = (
        evaluation.get("judge") in ("passed", "failed")
        and judgment_ok(judgment, state.get("evidence"), state.get("impact") is not None)
        and judgment_outcome(judgment["scores"]) == evaluation["judge"]
    )
    if judge_accepted:
        public["judge"] = evaluation["judge"]
        public["judgment"] = dict(judgment)
    return public


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
