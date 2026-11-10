"""O ai.ack.v3: o que o runtime devolve ao motor no fim de cada trecho, sempre montado só com dados conferidos."""

from __future__ import annotations

from contracts.engineering import impact_ok, intents_ok, judgment_ok, judgment_outcome
from contracts.messages import ResumeRequest
from infrastructure.events import now
from workflows.engineering.state import (
    AGENTS,
    EVALUATION,
    WORKFLOW_ID,
    WORKFLOW_VERSION,
    blank_state,
    fail,
    text,
)


def build_ack(state: dict, status: str, pending: dict | None = None) -> dict:
    failure = state.get("failure") or ""
    limitations = state.get("limitations") or []
    plan = state["plan"] if state.get("valid") and isinstance(state.get("plan"), dict) else {"schema": "engineering.plan.v1", "steps": []}
    output = {
        "summary": state.get("summary") or "",
        "plan": plan,
        "stacks": list(state.get("stacks") or []),
        "evidence": [public_evidence(item) for item in state.get("evidence") or []],
        "retrievalAttempted": bool(state.get("retrieval_attempted")),
    }
    if impact_ok(state.get("impact"), state.get("evidence")):
        output["impact"] = state["impact"]
    if (evaluation := public_evaluation(state)) is not None:
        output["evaluation"] = evaluation
    ack = {
        "schema": "ai.ack.v3",
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
        "agents": [dict(item) for item in state.get("agents") or [] if agents_ok([item])],
        # O que o runtime contou; quem vale é o proxy do motor, que conta por conta própria. Sem custo em dinheiro (D4).
        "usage": {
            "modelCalls": int(state.get("model_calls") or 0),
            "inputTokens": int(state.get("input_tokens") or 0),
            "outputTokens": int(state.get("output_tokens") or 0),
            "costUsd": 0,
        },
        "nextActions": [dict(item) for item in state.get("actions") or []] if state_intents_ok(state) else [],
    }
    if status == "interrupted" and pending:
        ack["approval"] = {"interruptId": pending["interruptId"], "actionIds": list(pending.get("actionIds") or [])}
    return ack


def failed_ack(state: dict, code: str, message: str) -> dict:
    return build_ack(fail(state, code, message), "failed")


def lost(request: ResumeRequest) -> dict:
    blank = blank_state(request.project_id, request.run_id, request.execution_id)
    return failed_ack(blank, "CHECKPOINT_LOST", "A execução pausada não está mais disponível.")


def rejected(payload: object, field: str) -> dict:
    body = payload if isinstance(payload, dict) else {}
    blank = blank_state(text(body.get("projectId")), text(body.get("runId")), text(body.get("executionId")))
    return failed_ack(blank, "CONTRACT_INVALID", f"A mensagem do motor não respeita o contrato ({field}).")


def agents_ok(agents: object) -> bool:
    if not isinstance(agents, list):
        return False
    return all(
        isinstance(item, dict) and item.get("agentId") in AGENTS and item.get("status") == "completed"
        for item in agents
    )


def state_intents_ok(state: dict) -> bool:
    return intents_ok(state.get("actions") or [], state.get("evidence"))


def public_evaluation(state: dict) -> dict | None:
    """O que sai no ack: o veredito determinístico e, só se passar na conferência, a opinião do juiz."""
    evaluation = state.get("evaluation")
    if not isinstance(evaluation, dict) or evaluation.get("verdict") != "deterministic":
        return None
    tokens = evaluation.get("tokens")
    public = {**EVALUATION, "tokens": tokens if isinstance(tokens, int) and not isinstance(tokens, bool) and tokens >= 0 else 0}
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


def public_evidence(item: dict) -> dict:
    return {
        "documentId": item.get("documentId", ""),
        "chunkId": item.get("chunkId", ""),
        "filePath": item.get("filePath", ""),
        "hash": item.get("hash", ""),
        "score": item.get("score", 0),
        "excerpt": item.get("excerpt", ""),
    }
