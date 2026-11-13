"""O contrato da investigação gerencial (``ai.investigation.start.v1`` e ``ai.investigation.ack.v1``).

O motor manda a pergunta e os fatos que ele mesmo calculou; a inteligência cruza esses fatos em etapas e devolve uma
resposta cujas afirmações citam ids de fatos. Aqui ficam a leitura do pedido e as validações do que o modelo escreve.
A conferência dos números continua sendo do motor: este módulo só garante forma e citação.
"""

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

MIN_QUESTION = 3
MAX_QUESTION = 500
MAX_FACTS = 300
MAX_FACT_TEXT = 1200
MAX_GROUP = 40
MAX_CLAIM = 400
MAX_PER_SECTION = 6
MAX_HYPOTHESES = 4
MAX_NEEDS = 4

SECTIONS = ("observed", "calculations", "inferences", "recommendations")
INVESTIGATION_SCHEMA = "ai.investigation.ack.v1"
HYPOTHESES_SCHEMA = "management.hypotheses.v1"
CONCLUSION_SCHEMA = "management.conclusion.v1"


@dataclass(frozen=True)
class Fact:
    id: str
    group: str
    text: str


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

    @property
    def groups(self) -> list[str]:
        """Os grupos de fatos, na ordem em que aparecem."""
        return list(dict.fromkeys(fact.group for fact in self.facts))


def parse_investigation(payload: object) -> InvestigationRequest:
    body = _object(payload, "mensagem")
    if body.get("schema") != "ai.investigation.start.v1":
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
    return InvestigationRequest(
        investigation_id=_match(body, "investigationId", INVESTIGATION_ID),
        execution_id=_match(body, "executionId", _EXECUTION_ID),
        project_id=_match(body, "projectId", _PROJECT_ID),
        question=question.strip(),
        facts=_facts(body.get("facts")),
        max_iterations=limits["maxIterations"],
        max_milliseconds=limits["maxMilliseconds"],
        max_tokens=limits["maxTokens"],
        model_allowed=policy["modelAllowed"],
        broker_url=broker_url,
        broker_token=broker_token,
    )


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


def hypotheses_from_model(document: dict, known: set[str], groups: set[str]) -> dict:
    """
    As hipóteses de uma rodada. Cada uma precisa citar fatos que já foram reunidos (citar um id que o modelo não viu é
    inventar). ``needs`` são os grupos de fatos que ele quer ver a seguir; o que não é um grupo conhecido é descartado
    sem recusar a resposta, porque um pedido a mais não é um fato inventado.
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
    return {"schema": HYPOTHESES_SCHEMA, "hypotheses": hypotheses, "needs": wanted}


def conclusion_from_model(document: dict, known: set[str]) -> dict:
    """
    A resposta final. A forma é estrita (resumo com texto, seções em lista). Uma afirmação sem citação válida não derruba a
    resposta: ela sai das seções e aparece em ``rejected`` com o motivo, como faz a conferência do motor.
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
    """Uma afirmação com os ids que existem. Sem texto, não é afirmação; ids que não existem são tirados."""
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
