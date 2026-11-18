"""A validação completa por JSON Schema roda nos testes do motor; aqui fica só o que a inteligência precisa recusar antes de
executar. Texto livre do pedido só entra em ``goal``.
"""

from __future__ import annotations

import re
from dataclasses import dataclass, field

RUN_ID = re.compile(r"^run-[0-9a-f]{12}$")
EXECUTION_ID = re.compile(r"^ex-[0-9a-f]{12}$")
PROJECT_ID = re.compile(r"^p-[0-9a-f]{12}$")
INTERRUPT_ID = re.compile(r"^int-[0-9a-f]{12}$")
APPROVAL_ID = re.compile(r"^apr-[0-9a-f]{12}$")
_WORKFLOW_ID = re.compile(r"^[a-z]+([.][a-z]+)*$")
_VERSION = re.compile(r"^[0-9]+$")
_STACK = re.compile(r"^[a-z0-9][a-z0-9_-]{0,31}$")
_HASH = re.compile(r"^[0-9a-f]{64}$")
_LIMITS = ("maxIterations", "maxMilliseconds", "maxTokens", "maxCostUsd")
# O maior teto que o motor deixa escolher para um fluxo (ModelBudgets); o schema e os testes conferem os três valores.
MAX_TOKENS = 64000
_BROKER_URL = re.compile(r"^http://127\.0\.0\.1:[0-9]{1,5}/v1/model$")


class ContractError(Exception):
    pass


@dataclass(frozen=True)
class Limits:
    max_iterations: int
    max_milliseconds: int
    max_tokens: int
    max_cost_usd: float


@dataclass(frozen=True)
class StartRequest:
    workflow_id: str
    workflow_version: str
    run_id: str
    execution_id: str
    project_id: str
    goal: str
    impact: bool
    requires_approval: bool
    stacks: list[str]
    knowledge_root: str
    execution_root: str
    sources: list[dict] | None
    limits: Limits
    allowed_actions: list[str] = field(default_factory=list)
    model_allowed: bool = False
    # Proxy de modelo do motor. Só existe quando o modelo é permitido, e o token só vale para esta execução.
    broker_url: str = field(default="", repr=False)
    broker_token: str = field(default="", repr=False)


PREVIEW_SCHEMA = "ai.context.preview.v1"


@dataclass(frozen=True)
class PreviewRequest:
    project_id: str
    goal: str
    stacks: list[str]
    knowledge_root: str
    sources: list[dict]


SEARCH_SCHEMA = "ai.knowledge.search.v1"
MAX_FILTER_TECHNOLOGIES = 20
TECHNOLOGY_ID = re.compile(r"^[a-z0-9-]{1,40}$")
SEARCH_MAX_QUERY = 300
SEARCH_MAX_LIMIT = 10


@dataclass(frozen=True)
class SearchRequest:
    project_id: str
    query: str
    knowledge_root: str
    limit: int
    technologies: tuple[str, ...] = ()


@dataclass(frozen=True)
class ResumeRequest:
    run_id: str
    execution_id: str
    project_id: str
    interrupt_id: str
    approval_id: str
    approved: bool
    action_ids: list[str]
    execution_root: str


def parse_start(payload: object) -> StartRequest:
    body = _object(payload, "mensagem")
    if body.get("schema") != "ai.workflow.start.v2":
        raise ContractError("schema")
    incoming = _object(body.get("input"), "input")
    context = _object(body.get("context"), "context")
    policy = _object(body.get("policy"), "policy")
    limits = _object(body.get("limits"), "limits")
    if any(not isinstance(limits.get(name), (int, float)) or isinstance(limits.get(name), bool) or limits[name] < 0 for name in _LIMITS):
        raise ContractError("limits")
    if limits["maxTokens"] > MAX_TOKENS:
        raise ContractError("limits")
    goal = _goal(incoming.get("goal"))
    stacks = _stacks(context.get("stacks"))
    sources = context.get("sources")
    if sources is not None:
        sources = _sources(sources)
    actions = policy.get("allowedActions")
    if not isinstance(actions, list) or not isinstance(policy.get("modelAllowed"), bool):
        raise ContractError("policy")
    broker_url, broker_token = _broker(body.get("broker"), policy["modelAllowed"])
    return StartRequest(
        workflow_id=_match(body, "workflowId", _WORKFLOW_ID),
        workflow_version=_match(body, "workflowVersion", _VERSION),
        run_id=_match(body, "runId", RUN_ID),
        execution_id=_match(body, "executionId", EXECUTION_ID),
        project_id=_match(body, "projectId", PROJECT_ID),
        goal=goal,
        impact=incoming.get("impact") is True,
        requires_approval=incoming.get("requiresApproval") is True,
        stacks=stacks,
        knowledge_root=_text(context.get("knowledgeRoot")),
        execution_root=_text(context.get("executionRoot")),
        sources=sources,
        limits=Limits(int(limits["maxIterations"]), int(limits["maxMilliseconds"]), int(limits["maxTokens"]), float(limits["maxCostUsd"])),
        allowed_actions=[item for item in actions if isinstance(item, str)],
        model_allowed=policy["modelAllowed"],
        broker_url=broker_url,
        broker_token=broker_token,
    )


def parse_preview(payload: object) -> PreviewRequest:
    body = _object(payload, "mensagem")
    if body.get("schema") != PREVIEW_SCHEMA:
        raise ContractError("schema")
    context = _object(body.get("context"), "context")
    knowledge_root = _text(context.get("knowledgeRoot"))
    if not knowledge_root:
        raise ContractError("knowledgeRoot")
    return PreviewRequest(
        project_id=_match(body, "projectId", PROJECT_ID),
        goal=_goal(body.get("goal")),
        stacks=_stacks(context.get("stacks")),
        knowledge_root=knowledge_root,
        sources=_sources(context.get("sources")),
    )


def parse_search(payload: object) -> SearchRequest:
    body = _object(payload, "mensagem")
    if body.get("schema") != SEARCH_SCHEMA:
        raise ContractError("schema")
    query, limit, root = body.get("query"), body.get("limit"), _text(body.get("knowledgeRoot"))
    if not isinstance(query, str) or not 0 < len(query.strip()) <= SEARCH_MAX_QUERY:
        raise ContractError("query")
    if not isinstance(limit, int) or isinstance(limit, bool) or not 1 <= limit <= SEARCH_MAX_LIMIT:
        raise ContractError("limit")
    if not root:
        raise ContractError("knowledgeRoot")
    return SearchRequest(project_id=_match(body, "projectId", PROJECT_ID), query=query.strip(), knowledge_root=root, limit=limit,
                         technologies=_technologies(body.get("filters")))


def _technologies(filters: object) -> tuple[str, ...]:
    """As stacks do projeto, para a busca deixar de fora o que é marcado para outras stacks. Ausente: nada filtrado."""
    if filters is None:
        return ()
    if not isinstance(filters, dict) or set(filters) - {"technologies"}:
        raise ContractError("filters")
    values = filters.get("technologies", [])
    if not isinstance(values, list) or len(values) > MAX_FILTER_TECHNOLOGIES or not all(isinstance(v, str) and TECHNOLOGY_ID.match(v) for v in values):
        raise ContractError("filters.technologies")
    return tuple(dict.fromkeys(values))


def parse_resume(payload: object) -> ResumeRequest:
    body = _object(payload, "mensagem")
    if body.get("schema") != "ai.workflow.resume.v2":
        raise ContractError("schema")
    decision = _object(body.get("decision"), "decision")
    context = _object(body.get("context"), "context")
    action_ids = decision.get("actionIds")
    if not isinstance(decision.get("approved"), bool) or not isinstance(action_ids, list):
        raise ContractError("decision")
    return ResumeRequest(
        run_id=_match(body, "runId", RUN_ID),
        execution_id=_match(body, "executionId", EXECUTION_ID),
        project_id=_match(body, "projectId", PROJECT_ID),
        interrupt_id=_match(body, "interruptId", INTERRUPT_ID),
        approval_id=_match(decision, "approvalId", APPROVAL_ID),
        approved=decision["approved"],
        action_ids=[item for item in action_ids if isinstance(item, str)],
        execution_root=_text(context.get("executionRoot")),
    )


def _broker(value: object, model_allowed: bool) -> tuple[str, str]:
    """Com modelo permitido, o proxy é obrigatório e só aceita 127.0.0.1; sem modelo, ele não pode vir."""
    if not model_allowed:
        if value is not None:
            raise ContractError("broker")
        return "", ""
    broker = _object(value, "broker")
    url, token = broker.get("url"), broker.get("token")
    if set(broker) != {"url", "token"} or not isinstance(url, str) or not _BROKER_URL.match(url):
        raise ContractError("broker")
    if not isinstance(token, str) or not 32 <= len(token) <= 128:
        raise ContractError("broker")
    return url, token


def _goal(value: object) -> str:
    if not isinstance(value, str) or not 0 < len(value) <= 4000:
        raise ContractError("goal")
    return value


def _stacks(value: object) -> list[str]:
    if not isinstance(value, list) or len(value) > 8 or any(not isinstance(item, str) or not _STACK.match(item) for item in value):
        raise ContractError("stacks")
    return list(dict.fromkeys(value))


def _sources(value: object) -> list[dict]:
    if not isinstance(value, list) or any(not _source_ok(item) for item in value):
        raise ContractError("sources")
    return value


def _source_ok(item: object) -> bool:
    if not isinstance(item, dict) or not isinstance(item.get("path"), str) or not isinstance(item.get("hash"), str):
        return False
    return bool(_HASH.match(item["hash"])) and ("text" not in item or isinstance(item["text"], str))


def _object(value: object, name: str) -> dict:
    if not isinstance(value, dict):
        raise ContractError(name)
    return value


def _match(body: dict, name: str, pattern: re.Pattern) -> str:
    value = body.get(name)
    if not isinstance(value, str) or not pattern.match(value):
        raise ContractError(name)
    return value


def _text(value: object) -> str:
    return value if isinstance(value, str) else ""
