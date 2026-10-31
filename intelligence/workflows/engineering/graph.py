"""Grafo engineering.request.v1.

Nós determinísticos: nenhum modelo é chamado e nada é gravado no projeto.
A busca usa só as fontes que o motor enviou.

A memória de trabalho é o dicionário deste processo. A pausa também vai para a
memória de execução (checkpoint em SQLite, fora do índice e fora do projeto),
para a retomada sobreviver a um reinício. O diário de negócio fica no motor.
"""

from __future__ import annotations

import re
import threading
import time

from knowledge.index import KnowledgeError, search, sync
from memory.execution import ExecutionError, delete as delete_checkpoint, load as load_checkpoint, save as save_checkpoint

WORKFLOW_ID = "engineering.request.v1"
MAX_ITERATIONS = 8
MAX_MILLISECONDS = 2000
MAX_TOKENS = 0
_STACK = re.compile(r"^[a-z0-9][a-z0-9_-]{0,31}$")
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
_AGENT_ID = "architecture"
_sessions: dict[str, dict] = {}
_lock = threading.Lock()


def run_start(payload: dict, model=None, clock=None) -> dict:
    state = _initial(payload)
    state["clock"] = clock or time.monotonic
    state["started"] = state["clock"]()
    for node in (request_analyzer, context_builder, rag_research, planning_agent, plan_validator):
        state = node(state)
        state["iterations"] += 1
        if _over_budget(state):
            return _fail(state, state["limitations"][-1]["code"], state["limitations"][-1]["message"])
    if not state["valid"]:
        return _ack(state, "failed")
    if state["asks_impact"]:
        state = architecture_agent(state)
        state = impact_eval(state)
    state = approval_gate(state)
    if state.get("paused"):
        _remember(state)
        return _ack(state, "interrupted")
    state = synthesizer(state)
    state = evaluation_judge(state, model)
    return _ack(state, "completed")


def run_resume(payload: dict, model=None) -> dict:
    run_id = _text(payload.get("runId"))
    state = _recall(run_id, payload)
    if state is None:
        return _lost(payload)
    incoming = payload.get("input") if isinstance(payload.get("input"), dict) else {}
    approved = payload.get("approved") is True or incoming.get("approved") is True
    if not approved:
        return _ack(state, "interrupted")
    state = dict(state)
    state["approved"] = True
    state["paused"] = False
    state = synthesizer(state)
    state = evaluation_judge(state, model)
    _forget(state)
    return _ack(state, "completed")


def run_cancel(run_id: str, project_id: str = "", execution_root: str = "") -> None:
    with _lock:
        state = _sessions.pop(run_id, None)
    root = execution_root or (state or {}).get("execution_root") or ""
    project = project_id or (state or {}).get("project_id") or ""
    if root and project:
        _drop(root, project, run_id)


def request_analyzer(state: dict) -> dict:
    if not state["goal"]:
        _limit(state, "REQUEST_EMPTY", "O pedido veio sem objetivo.")
    return state


def context_builder(state: dict) -> dict:
    # Só aceita identificadores curtos vindos do motor. Texto livre do pedido não entra aqui.
    return state


def rag_research(state: dict) -> dict:
    state["evidence"] = []
    state["retrieval_attempted"] = False
    root = state["knowledge_root"]
    sources = state["sources"]
    if not root or sources is None:
        _limit(state, "RAG_NOT_READY", "A recuperação de contexto ainda não indexa o projeto.")
        return state
    try:
        stats = sync(root, state["project_id"], sources)
        hits = search(root, state["project_id"], state["goal"])
    except KnowledgeError:
        _limit(state, "RAG_NOT_READY", "A recuperação de contexto ainda não indexa o projeto.")
        return state
    state["retrieval_attempted"] = True
    state["evidence"] = hits
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
    if not isinstance(plan, dict) or plan.get("schema") != "engineering.plan.v1":
        state["valid"] = False
        _limit(state, "PLAN_INVALID", "O plano não respeita o contrato.")
        return state
    steps = plan.get("steps")
    if not isinstance(steps, list) or not steps or _forbidden(plan):
        state["valid"] = False
        _limit(state, "PLAN_INVALID", "O plano não respeita o contrato.")
        return state
    for step in steps:
        if not isinstance(step, dict) or not _text(step.get("id")) or not _text(step.get("title")):
            state["valid"] = False
            _limit(state, "PLAN_INVALID", "O plano não respeita o contrato.")
            return state
    state["valid"] = True
    return state


def architecture_agent(state: dict) -> dict:
    """Um agente, só quando o plano pediu impacto. Não chama modelo e não grava no projeto."""
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
    state["agents"] = [{"id": _AGENT_ID, "status": "completed"}]
    return state


def impact_eval(state: dict) -> dict:
    if _impact_ok(state.get("impact"), state.get("evidence")):
        return state
    state["impact"] = None
    state["agents"] = []
    _limit(state, "IMPACT_INVALID", "O impacto técnico não respeita o contrato.")
    return state


def approval_gate(state: dict) -> dict:
    state["paused"] = bool(state["requires_approval"] and not state["approved"])
    return state


def synthesizer(state: dict) -> dict:
    count = len(state["plan"]["steps"]) if isinstance(state.get("plan"), dict) else 0
    detected = len(state["stacks"])
    found = len(state["evidence"])
    state["summary"] = f"{count} etapa(s). Stacks detectadas: {detected}. Trechos: {found}. Nenhuma alteração no projeto."
    return state


def evaluation_judge(state: dict, model=None) -> dict:
    # Orçamento de tokens é zero: o juiz não chama modelo e não vira o veredito.
    del model
    state["tokens"] = 0
    _limit(state, "JUDGE_NOT_RUN", "O juiz por modelo não é o veredito desta fase.")
    state["evaluation"] = {
        "schema": "engineering.evaluation.v1",
        "verdict": "deterministic",
        "judge": "not_run",
        "tokens": 0,
    }
    return state


def _initial(payload: dict) -> dict:
    incoming = payload.get("input") if isinstance(payload.get("input"), dict) else {}
    context = payload.get("context") if isinstance(payload.get("context"), dict) else {}
    return {
        "run_id": _text(payload.get("runId")),
        "project_id": _text(payload.get("projectId")),
        "execution_id": _text(payload.get("executionId")),
        "goal": _text(incoming.get("goal")),
        "requires_approval": incoming.get("requiresApproval") is True,
        "asks_impact": incoming.get("impact") is True,
        "approved": False,
        "paused": False,
        "stacks": _stacks(context.get("stacks")),
        "knowledge_root": _text(context.get("knowledgeRoot")),
        "execution_root": _text(context.get("executionRoot")),
        "sources": context.get("sources") if isinstance(context.get("sources"), list) else None,
        "evidence": [],
        "retrieval_attempted": False,
        "limitations": [],
        "plan": None,
        "impact": None,
        "agents": [],
        "evaluation": None,
        "tokens": 0,
        "valid": False,
        "summary": "",
        "iterations": 0,
    }


def _stacks(raw: object) -> list[str]:
    if not isinstance(raw, list):
        return []
    found: list[str] = []
    for item in raw:
        if isinstance(item, str) and _STACK.match(item) and item not in found:
            found.append(item)
        if len(found) == 8:
            break
    return found


def _forbidden(value: object) -> bool:
    if isinstance(value, dict):
        return any(key in _FORBIDDEN or _forbidden(child) for key, child in value.items())
    if isinstance(value, list):
        return any(_forbidden(item) for item in value)
    return False


_DURABLE = (
    "run_id",
    "project_id",
    "execution_id",
    "goal",
    "requires_approval",
    "approved",
    "paused",
    "stacks",
    "evidence",
    "retrieval_attempted",
    "limitations",
    "plan",
    "asks_impact",
    "impact",
    "agents",
    "valid",
    "summary",
    "iterations",
    "execution_root",
)


def _remember(state: dict) -> None:
    with _lock:
        _sessions[state["run_id"]] = state
    _persist(state)


def _persist(state: dict) -> None:
    root = state.get("execution_root") or ""
    if not root:
        return
    try:
        save_checkpoint(root, state["project_id"], state["run_id"], {key: state.get(key) for key in _DURABLE})
    except (ExecutionError, TypeError, ValueError):
        return


def _recall(run_id: str, payload: dict) -> dict | None:
    with _lock:
        state = _sessions.get(run_id)
    if state is not None:
        return state
    context = payload.get("context") if isinstance(payload.get("context"), dict) else {}
    root = _text(context.get("executionRoot"))
    project_id = _text(payload.get("projectId"))
    if not root or not project_id:
        return None
    try:
        restored = load_checkpoint(root, project_id, run_id)
    except ExecutionError:
        return None
    restored.setdefault("asks_impact", False)
    restored.setdefault("impact", None)
    restored.setdefault("agents", [])
    if not _checkpoint_ok(restored, project_id, run_id):
        return None
    restored["execution_root"] = restored.get("execution_root") or root
    with _lock:
        _sessions[run_id] = restored
    return restored


def _checkpoint_ok(state: object, project_id: str, run_id: str) -> bool:
    if not isinstance(state, dict):
        return False
    if state.get("project_id") != project_id or state.get("run_id") != run_id or state.get("paused") is not True:
        return False
    if not isinstance(state.get("stacks"), list) or not isinstance(state.get("limitations"), list):
        return False
    evidence = state.get("evidence")
    if not isinstance(evidence, list) or any(not isinstance(item, dict) for item in evidence):
        return False
    if _forbidden(state.get("plan")) or _forbidden(state.get("impact")):
        return False
    if not _agents_ok(state.get("agents")):
        return False
    if state.get("impact") is not None and not _impact_ok(state.get("impact"), state.get("evidence")):
        return False
    clone = dict(state)
    clone["limitations"] = list(state["limitations"])
    return plan_validator(clone).get("valid") is True


def _forget(state: dict) -> None:
    with _lock:
        _sessions.pop(state["run_id"], None)
    _drop(state.get("execution_root") or "", state.get("project_id") or "", state["run_id"])


def _drop(root: str, project_id: str, run_id: str) -> None:
    if not root or not project_id:
        return
    try:
        delete_checkpoint(root, project_id, run_id)
    except ExecutionError:
        return


def _limit(state: dict, code: str, message: str) -> None:
    if any(item["code"] == code for item in state["limitations"]):
        return
    state["limitations"].append({"code": code, "message": message})


def _over_budget(state: dict) -> bool:
    if state["iterations"] > MAX_ITERATIONS:
        _limit(state, "LIMIT_ITERATIONS", "A execução passou do limite de etapas.")
        return True
    clock = state.get("clock") or time.monotonic
    started = state.get("started", clock())
    if (clock() - started) * 1000 > MAX_MILLISECONDS:
        _limit(state, "LIMIT_TIME", "A execução passou do limite de tempo.")
        return True
    if int(state.get("tokens") or 0) > MAX_TOKENS:
        _limit(state, "LIMIT_TOKENS", "A execução passou do limite de tokens.")
        return True
    return False


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
    return all(isinstance(item, dict) and item.get("id") == _AGENT_ID and item.get("status") == "completed" for item in agents)


def _ack(state: dict, status: str) -> dict:
    plan = state["plan"] if state.get("valid") else {"schema": "engineering.plan.v1", "steps": []}
    output = {
        "summary": state["summary"],
        "plan": plan,
        "stacks": list(state["stacks"]),
        "evidence": [_public_evidence(item) for item in state["evidence"]],
        "retrievalAttempted": bool(state["retrieval_attempted"]),
        "agents": [dict(item) for item in state.get("agents") or [] if _agents_ok([item])],
    }
    if _impact_ok(state.get("impact"), state.get("evidence")):
        output["impact"] = state["impact"]
    evaluation = state.get("evaluation")
    if isinstance(evaluation, dict) and evaluation.get("verdict") == "deterministic" and evaluation.get("judge") == "not_run":
        output["evaluation"] = {
            "schema": "engineering.evaluation.v1",
            "verdict": "deterministic",
            "judge": "not_run",
            "tokens": 0,
        }
    return {
        "schema": "workflow.ack.v1",
        "workflowId": WORKFLOW_ID,
        "runId": state["run_id"],
        "projectId": state["project_id"],
        "executionId": state["execution_id"],
        "status": status,
        "limitations": state["limitations"],
        "output": output,
    }


def _fail(state: dict, code: str, message: str) -> dict:
    state["valid"] = False
    _limit(state, code, message)
    return _ack(state, "failed")


def _lost(payload: dict) -> dict:
    state = _initial(payload)
    state["valid"] = False
    _limit(state, "CHECKPOINT_LOST", "A execução pausada não está mais neste processo.")
    return _ack(state, "failed")


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
