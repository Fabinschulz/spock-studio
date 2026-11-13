"""A conferência dos números da resposta continua sendo do motor: aqui só se garantem forma e citação."""

from __future__ import annotations

import re
from dataclasses import dataclass, field

from contracts.engineering import Rejected
from contracts.messages import ContractError, _broker, _match, _object

INVESTIGATION_ID = re.compile(r"^inv-[0-9a-f]{12}$")
_EXECUTION_ID = re.compile(r"^ex-[0-9a-f]{12}$")
_PROJECT_ID = re.compile(r"^p-[0-9a-f]{12}$")
_FACT_ID = re.compile(r"^[A-Za-z0-9][A-Za-z0-9._:-]{0,63}$")
_LIMITS = ("maxIterations", "maxMilliseconds", "maxTokens")
_MEMORY_PATH = re.compile(r"^@(decision|external)/[A-Za-z0-9._:/-]{1,200}$")
_HASH = re.compile(r"^[0-9a-f]{64}$")

MIN_QUESTION = 3
MAX_QUESTION = 500
MAX_FACTS = 300
MAX_FACT_TEXT = 1200
MAX_GROUP = 40
MAX_CLAIM = 400
MAX_PER_SECTION = 6
MAX_HYPOTHESES = 4
MAX_NEEDS = 4
MAX_QUERY = 120
MAX_SEARCHES_PER_ROUND = 2
MAX_SEARCHES = 4
MAX_MEMORY_DOCUMENTS = 1200
MAX_MEMORY_TEXT = 4000

SECTIONS = ("observed", "calculations", "inferences", "recommendations")
INVESTIGATION_SCHEMA = "ai.investigation.ack.v1"
HYPOTHESES_SCHEMA = "management.hypotheses.v1"
CONCLUSION_SCHEMA = "management.conclusion.v1"


@dataclass(frozen=True)
class Fact:
    id: str
    group: str
    text: str


GROUP_ROLES = frozenset({"base", "default", "sign"})
MAX_GROUPS = 30
MAX_KEYWORDS = 40


@dataclass(frozen=True)
class FactGroup:
    """O runtime não conhece nomes de grupo: o motor os descreve."""

    name: str
    roles: frozenset[str]
    keywords: tuple[str, ...]


@dataclass(frozen=True)
class Memory:
    root: str
    documents: list[dict]


@dataclass(frozen=True)
class InvestigationRequest:
    investigation_id: str
    execution_id: str
    project_id: str
    question: str
    facts: list[Fact]
    max_iterations: int
    max_milliseconds: int
    max_tokens: int
    model_allowed: bool = False
    broker_url: str = field(default="", repr=False)
    broker_token: str = field(default="", repr=False)
    memory: Memory | None = None
    catalog: tuple[FactGroup, ...] = ()

    @property
    def groups(self) -> list[str]:
        return list(dict.fromkeys(fact.group for fact in self.facts))


def parse_investigation(payload: object) -> InvestigationRequest:
    body = _object(payload, "mensagem")
    if body.get("schema") != "ai.investigation.start.v2":
        raise ContractError("schema")
    policy = _object(body.get("policy"), "policy")
    limits = _object(body.get("limits"), "limits")
    if any(not isinstance(limits.get(name), int) or isinstance(limits.get(name), bool) or limits[name] < 0 for name in _LIMITS):
        raise ContractError("limits")
    if not isinstance(policy.get("modelAllowed"), bool):
        raise ContractError("policy")
    question = body.get("question")
    if not isinstance(question, str) or not MIN_QUESTION <= len(question.strip()) <= MAX_QUESTION:
        raise ContractError("question")
    broker_url, broker_token = _broker(body.get("broker"), policy["modelAllowed"])
    facts = _facts(body.get("facts"))
    return InvestigationRequest(
        investigation_id=_match(body, "investigationId", INVESTIGATION_ID),
        execution_id=_match(body, "executionId", _EXECUTION_ID),
        project_id=_match(body, "projectId", _PROJECT_ID),
        question=question.strip(),
        facts=facts,
        max_iterations=limits["maxIterations"],
        max_milliseconds=limits["maxMilliseconds"],
        max_tokens=limits["maxTokens"],
        model_allowed=policy["modelAllowed"],
        broker_url=broker_url,
        broker_token=broker_token,
        memory=_memory(body.get("memory")),
        catalog=parse_groups(body.get("groups"), facts),
    )


def parse_groups(value: object, facts: list[Fact] | None = None) -> tuple[FactGroup, ...]:
    """Todo grupo dos fatos precisa estar descrito: sem isso, o laço não sabe por onde começar."""
    if not isinstance(value, list) or not 1 <= len(value) <= MAX_GROUPS:
        raise ContractError("groups")
    groups: list[FactGroup] = []
    for item in value:
        if not isinstance(item, dict):
            raise ContractError("groups")
        name, roles, keywords = item.get("name"), item.get("roles"), item.get("keywords")
        if not isinstance(name, str) or not 0 < len(name) <= MAX_GROUP or any(group.name == name for group in groups):
            raise ContractError("groups")
        if not isinstance(roles, list) or not all(isinstance(role, str) and role in GROUP_ROLES for role in roles) or len(set(roles)) != len(roles):
            raise ContractError("groups")
        if not isinstance(keywords, list) or len(keywords) > MAX_KEYWORDS or not all(isinstance(word, str) and 0 < len(word) <= MAX_GROUP for word in keywords):
            raise ContractError("groups")
        groups.append(FactGroup(name, frozenset(roles), tuple(keywords)))
    described = {group.name for group in groups}
    if any(fact.group not in described for fact in facts or []):
        raise ContractError("groups")
    return tuple(groups)


def _memory(value: object) -> Memory | None:
    """
    Só entram decisões registradas e itens externos autorizados pelo motor, num índice à parte do de engenharia: arquivo do
    projeto nunca passa por aqui.
    """
    if value is None:
        return None
    body = _object(value, "memory")
    root, documents = body.get("root"), body.get("documents")
    if not isinstance(root, str) or not 0 < len(root) <= 500 or "\0" in root:
        raise ContractError("memory")
    if not isinstance(documents, list) or len(documents) > MAX_MEMORY_DOCUMENTS:
        raise ContractError("memory")
    accepted: list[dict] = []
    seen: set[str] = set()
    for item in documents:
        if not isinstance(item, dict):
            raise ContractError("memory")
        path, digest, text = item.get("path"), item.get("hash"), item.get("text")
        if not isinstance(path, str) or not _MEMORY_PATH.match(path) or ".." in path.split("/") or path in seen:
            raise ContractError("memory")
        if not isinstance(digest, str) or not _HASH.match(digest) or not isinstance(text, str) or not 0 < len(text) <= MAX_MEMORY_TEXT:
            raise ContractError("memory")
        seen.add(path)
        accepted.append({"path": path, "hash": digest, "text": text})
    return Memory(root, accepted)


def _facts(value: object) -> list[Fact]:
    if not isinstance(value, list) or not 1 <= len(value) <= MAX_FACTS:
        raise ContractError("facts")
    facts: list[Fact] = []
    seen: set[str] = set()
    for item in value:
        if not isinstance(item, dict):
            raise ContractError("facts")
        fact_id, group, text = item.get("id"), item.get("group"), item.get("text")
        if not isinstance(fact_id, str) or not _FACT_ID.match(fact_id) or fact_id in seen:
            raise ContractError("facts")
        if not isinstance(group, str) or not 0 < len(group) <= MAX_GROUP or not isinstance(text, str) or not 0 < len(text) <= MAX_FACT_TEXT:
            raise ContractError("facts")
        seen.add(fact_id)
        facts.append(Fact(fact_id, group, text))
    return facts


def hypotheses_from_model(document: dict, known: set[str], groups: set[str], can_search: bool = False, searched: tuple[str, ...] = ()) -> dict:
    """
    Citar um id que o modelo não viu é inventar. Um ``needs`` desconhecido é descartado sem recusar a resposta, porque um
    pedido a mais não é um fato inventado.
    """
    raw = document.get("hypotheses")
    if not isinstance(raw, list) or len(raw) > MAX_HYPOTHESES:
        raise Rejected(f"'hypotheses' precisa ser uma lista de até {MAX_HYPOTHESES} itens.")
    hypotheses = []
    for item in raw:
        claim = _claim(item, known)
        if claim is None or not claim["evidence"]:
            raise Rejected("Cada hipótese precisa de 'text' e de 'evidence' com ids dos fatos fornecidos.")
        hypotheses.append(claim)
    needs = document.get("needs", [])
    if not isinstance(needs, list):
        raise Rejected("'needs' precisa ser uma lista de nomes de grupos de fatos.")
    wanted = [name for name in dict.fromkeys(needs) if isinstance(name, str) and name in groups][:MAX_NEEDS]
    return {"schema": HYPOTHESES_SCHEMA, "hypotheses": hypotheses, "needs": wanted, "searches": _searches(document.get("searches"), can_search, searched)}


def _searches(value: object, can_search: bool, searched: tuple[str, ...]) -> list[str]:
    """Pedir uma busca a mais não derruba a resposta: sem memória, ou fora da forma, vira nada."""
    if not can_search or not isinstance(value, list):
        return []
    done = {query.casefold() for query in searched}
    queries: list[str] = []
    for item in value:
        if not isinstance(item, str):
            continue
        query = " ".join(item.split())[:MAX_QUERY]
        if len(query) >= 3 and query.casefold() not in done and query.casefold() not in {q.casefold() for q in queries}:
            queries.append(query)
    return queries[:MAX_SEARCHES_PER_ROUND]


def conclusion_from_model(document: dict, known: set[str]) -> dict:
    """
    Uma afirmação sem citação válida não derruba a resposta: sai das seções e aparece em ``rejected`` com o motivo, como faz a
    conferência do motor.
    """
    summary = document.get("summary")
    if not isinstance(summary, dict) or not isinstance(summary.get("text"), str) or not summary["text"].strip():
        raise Rejected("'summary' precisa ser um objeto com 'text'.")
    rejected: list[dict] = []

    def keep(item: object, section: str) -> dict | None:
        claim = _claim(item, known)
        if claim is None:
            rejected.append({"text": _clip(item.get("text") if isinstance(item, dict) else "") or "(sem texto)", "reason": "malformed"})
            return None
        if not claim["evidence"]:
            rejected.append({"text": claim["text"], "reason": "no_valid_evidence"})
            return None
        return claim

    accepted_summary = keep(summary, "summary")
    sections: dict[str, list[dict]] = {}
    for name in SECTIONS:
        raw = document.get(name, [])
        if not isinstance(raw, list):
            raise Rejected(f"'{name}' precisa ser uma lista.")
        sections[name] = [claim for item in raw[:MAX_PER_SECTION] if (claim := keep(item, name))]
        rejected.extend({"text": _clip(item.get("text")) or "(sem texto)", "reason": "too_many"} for item in raw[MAX_PER_SECTION:] if isinstance(item, dict))
    unavailable = document.get("unavailable", [])
    if not isinstance(unavailable, list):
        raise Rejected("'unavailable' precisa ser uma lista.")
    return {
        "schema": CONCLUSION_SCHEMA,
        "summary": accepted_summary,
        **sections,
        "unavailable": [{"text": _clip(item.get("text"))} for item in unavailable[:MAX_PER_SECTION] if isinstance(item, dict) and _clip(item.get("text"))],
        "rejected": rejected,
    }


def _claim(item: object, known: set[str]) -> dict | None:
    if not isinstance(item, dict):
        return None
    text = _clip(item.get("text"))
    if not text:
        return None
    evidence = item.get("evidence", [])
    ids = [value for value in dict.fromkeys(evidence) if isinstance(value, str) and value in known] if isinstance(evidence, list) else []
    return {"text": text, "evidence": ids}


def _clip(value: object) -> str:
    return value.strip()[:MAX_CLAIM] if isinstance(value, str) else ""
