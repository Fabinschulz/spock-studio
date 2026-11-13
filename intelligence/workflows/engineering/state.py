from __future__ import annotations

import time
from typing import Callable, TypedDict

from agents import planning as planning_model
from agents.runner import Model
from contracts.messages import StartRequest
from infrastructure.events import Emitter, now

WORKFLOW_ID = "engineering.request"
WORKFLOW_VERSION = "1"
MAX_ITERATIONS = 8
MAX_MILLISECONDS = 2000
# Com modelo, uma chamada leva segundos: o tempo e os tokens sobem, mas o motor continua sendo quem define o teto.
MAX_MILLISECONDS_MODEL = 60000
MAX_TOKENS = 20000
AREA_BY_STACK = {
    "dotnet": "servico",
    "javascript": "interface",
    "typescript": "interface",
    "node": "interface",
    "flutter": "interface",
}
ARCHITECTURE = "architecture"
PLANNING = planning_model.AGENT_ID
AGENTS = {PLANNING, ARCHITECTURE}
AGENT_VERSION = "1"
MODEL_LIMITATIONS = {
    "MODEL_BUDGET_EXCEEDED": "O orçamento de tokens da execução acabou.",
    "MODEL_TIMEOUT": "O modelo não respondeu no prazo.",
    "MODEL_UNAVAILABLE": "O modelo não está disponível.",
    "MODEL_NOT_CONFIGURED": "Nenhum modelo está configurado.",
    "MODEL_FORBIDDEN": "O proxy de modelo recusou a chamada.",
    "MODEL_OUTPUT_INVALID": "O modelo respondeu fora do contrato, mesmo depois de uma nova tentativa.",
}
EVALUATION = {"schema": "engineering.evaluation.v1", "verdict": "deterministic", "judge": "not_run", "tokens": 0}


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
    limits: dict


class Segment:
    """Nada disso vai para o checkpoint."""

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


    def limits(self) -> dict:
        """A retomada continua com os limites com que a execução começou."""
        return {"iterations": self.max_iterations, "milliseconds": self.max_milliseconds, "tokens": self.max_tokens}


def segment_from_state(emitter: Emitter, clock: Callable[[], float] | None, state: dict) -> Segment:
    """
    O checkpoint vem do disco: só valem inteiros não negativos e nunca acima do teto do runtime. Sem eles (checkpoint de
    versão anterior), vale o teto com modelo.
    """
    saved = state.get("limits") if isinstance(state.get("limits"), dict) else {}

    def bounded(name: str, ceiling: int) -> int:
        value = saved.get(name)
        return min(value, ceiling) if isinstance(value, int) and not isinstance(value, bool) and value >= 0 else ceiling

    return Segment(
        emitter,
        clock,
        bounded("iterations", MAX_ITERATIONS),
        bounded("milliseconds", MAX_MILLISECONDS_MODEL),
        max_tokens=bounded("tokens", MAX_TOKENS),
    )


def initial_state(request: StartRequest) -> dict:
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


def blank_state(project_id: str, run_id: str, execution_id: str) -> dict:
    return {
        "run_id": run_id, "project_id": project_id, "execution_id": execution_id, "started_at": now(), "stacks": [],
        "evidence": [], "retrieval_attempted": False, "limitations": [], "failure": "", "plan": None, "impact": None,
        "agents": [], "evaluation": None, "valid": False, "summary": "",
    }


def copy_state(state: dict) -> dict:
    copied = dict(state)
    copied["limitations"] = list(state.get("limitations") or [])
    return copied


def limit(state: dict, code: str, message: str) -> None:
    limitations = state.get("limitations") or []
    if any(item["code"] == code for item in limitations):
        return
    state["limitations"] = [*limitations, {"code": code, "message": message}]


def fail(state: dict, code: str, message: str) -> dict:
    state["valid"] = False
    state["failure"] = code
    limit(state, code, message)
    return state


def text(value: object) -> str:
    return value if isinstance(value, str) else ""
