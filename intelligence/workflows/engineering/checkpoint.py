"""Conferência do checkpoint: ele vem do disco e só volta se ainda for o que a pausa gravou."""

from __future__ import annotations

from contracts.engineering import forbidden, impact_ok
from workflows.engineering.ack import agents_ok, state_actions_ok
from workflows.engineering.nodes import plan_validator


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
    if forbidden(state.get("plan")) or forbidden(state.get("impact")):
        return False
    if not agents_ok(state.get("agents") or []):
        return False
    if state.get("impact") is not None and not impact_ok(state.get("impact"), evidence):
        return False
    if not state_actions_ok(state) or (state.get("actions") and state.get("impact") is None):
        return False
    clone = dict(state)
    clone["limitations"] = list(state["limitations"])
    return plan_validator(clone).get("valid") is True
