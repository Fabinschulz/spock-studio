"""Grafo engineering.request.v1.

Nós determinísticos: nenhum modelo é chamado e nada é gravado no projeto.
A busca usa só as fontes que o motor enviou. A pausa para aprovação fica em memória
deste processo; persistir isso entre reinícios é a fase de memória.
"""

from __future__ import annotations

import re
import threading

from knowledge.index import KnowledgeError, search, sync

WORKFLOW_ID = "engineering.request.v1"
_STACK = re.compile(r"^[a-z0-9][a-z0-9_-]{0,31}$")
_FORBIDDEN = {"command", "commands", "executable", "arguments", "shell", "write", "path"}
_sessions: dict[str, dict] = {}
_lock = threading.Lock()


def run_start(payload: dict, model=None) -> dict:
    state = _initial(payload)
    for node in (request_analyzer, context_builder, rag_research, planning_agent, plan_validator):
        state = node(state)
        state["iterations"] += 1
        if state["iterations"] > 8:
            return _fail(state, "PLAN_INVALID", "O plano excedeu o limite de etapas.")
    if not state["valid"]:
        return _ack(state, "failed")
    state = approval_gate(state)
    if state.get("paused"):
        with _lock:
            _sessions[state["run_id"]] = state
        return _ack(state, "interrupted")
    state = synthesizer(state)
    state = evaluation_judge(state, model)
    return _ack(state, "completed")


def run_resume(payload: dict, model=None) -> dict:
    run_id = _text(payload.get("runId"))
    with _lock:
        state = _sessions.get(run_id)
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
    with _lock:
        _sessions.pop(run_id, None)
    return _ack(state, "completed")


def run_cancel(run_id: str) -> None:
    with _lock:
        _sessions.pop(run_id, None)


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
    # O modelo é um parâmetro para o teste provar que o juiz não o chama.
    del model
    _limit(state, "JUDGE_NOT_RUN", "O juiz por modelo não é o veredito desta fase.")
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
        "approved": False,
        "paused": False,
        "stacks": _stacks(context.get("stacks")),
        "knowledge_root": _text(context.get("knowledgeRoot")),
        "sources": context.get("sources") if isinstance(context.get("sources"), list) else None,
        "evidence": [],
        "retrieval_attempted": False,
        "limitations": [],
        "plan": None,
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


def _limit(state: dict, code: str, message: str) -> None:
    if any(item["code"] == code for item in state["limitations"]):
        return
    state["limitations"].append({"code": code, "message": message})


def _ack(state: dict, status: str) -> dict:
    plan = state["plan"] if state.get("valid") else {"schema": "engineering.plan.v1", "steps": []}
    return {
        "schema": "workflow.ack.v1",
        "workflowId": WORKFLOW_ID,
        "runId": state["run_id"],
        "projectId": state["project_id"],
        "executionId": state["execution_id"],
        "status": status,
        "limitations": state["limitations"],
        "output": {
            "summary": state["summary"],
            "plan": plan,
            "stacks": list(state["stacks"]),
            "evidence": [_public_evidence(item) for item in state["evidence"]],
            "retrievalAttempted": bool(state["retrieval_attempted"]),
        },
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
