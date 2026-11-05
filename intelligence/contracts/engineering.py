"""Regras dos formatos que o grafo produz (engineering.plan.v1 e engineering.impact.v1).

Ficam aqui, e não no grafo, porque o grafo e os agentes com modelo validam a mesma saída: o que o modelo escreve só vira
plano ou impacto se passar por estas funções, e as chaves de comando, caminho e escrita nunca passam.
"""

from __future__ import annotations

import re

FORBIDDEN = {"command", "commands", "executable", "arguments", "shell", "write", "path"}
AREAS = {"servico", "interface", "projeto"}
RISKS = {"sem-evidencia", "sem-stack"}

PLAN_SCHEMA = "engineering.plan.v1"
IMPACT_SCHEMA = "engineering.impact.v1"
MAX_STEPS = 16
MAX_STEP_EVIDENCE = 8
MAX_TITLE = 120
STEP_ID = re.compile(r"^[a-z][a-z0-9-]{0,31}$")


def forbidden(value: object) -> bool:
    """Qualquer chave de comando, caminho ou escrita, em qualquer nível, invalida a saída."""
    if isinstance(value, dict):
        return any(key in FORBIDDEN or forbidden(child) for key, child in value.items())
    if isinstance(value, list):
        return any(forbidden(item) for item in value)
    return False


def known_chunks(evidence: object) -> set[str]:
    """Os ``chunkId`` que a recuperação realmente devolveu: a única fonte válida de citação."""
    known: set[str] = set()
    if isinstance(evidence, list):
        for item in evidence:
            if isinstance(item, dict) and isinstance(item.get("chunkId"), str):
                known.add(item["chunkId"])
    return known


def impact_ok(impact: object, evidence: object) -> bool:
    if not isinstance(impact, dict) or impact.get("schema") != IMPACT_SCHEMA:
        return False
    if forbidden(impact) or set(impact) != {"schema", "areas", "risks", "evidence"}:
        return False
    areas = impact.get("areas")
    risks = impact.get("risks")
    refs = impact.get("evidence")
    if not isinstance(areas, list) or not areas or any(area not in AREAS for area in areas):
        return False
    if not isinstance(risks, list) or any(risk not in RISKS for risk in risks):
        return False
    if not isinstance(refs, list):
        return False
    known = known_chunks(evidence)
    return all(isinstance(ref, str) and ref in known for ref in refs)


class Rejected(Exception):
    """A saída do modelo não respeita o contrato. A mensagem é nossa (nunca texto do modelo) e pode voltar a ele."""


def plan_from_model(document: object, known: set[str]) -> dict:
    """
    Valida o plano que o modelo escreveu e devolve a versão normalizada. Cada passo pode citar trechos recuperados
    (``evidence``); se houve recuperação, ao menos um passo precisa citar um. Citar o que não foi recuperado é recusado.
    """
    if not isinstance(document, dict) or document.get("schema") != PLAN_SCHEMA or set(document) != {"schema", "steps"}:
        raise Rejected(f"O objeto precisa ter só 'schema' ({PLAN_SCHEMA}) e 'steps'.")
    steps = document.get("steps")
    if not isinstance(steps, list) or not 1 <= len(steps) <= MAX_STEPS:
        raise Rejected(f"'steps' precisa ter de 1 a {MAX_STEPS} etapas.")
    if forbidden(document):
        raise Rejected("O plano não pode ter comandos, caminhos nem escrita.")

    seen: set[str] = set()
    cited = False
    normalized: list[dict] = []
    for step in steps:
        if not isinstance(step, dict) or not set(step) <= {"id", "title", "evidence"}:
            raise Rejected("Cada etapa só pode ter 'id', 'title' e 'evidence'.")
        step_id, title = step.get("id"), step.get("title")
        if not isinstance(step_id, str) or not STEP_ID.match(step_id) or step_id in seen:
            raise Rejected("O 'id' de cada etapa é único, em minúsculas, com números e hífen (até 32 caracteres).")
        if not isinstance(title, str) or not title.strip() or len(title) > MAX_TITLE or any(ord(c) < 32 for c in title):
            raise Rejected(f"O 'title' de cada etapa é um texto de uma linha com até {MAX_TITLE} caracteres.")
        seen.add(step_id)
        item = {"id": step_id, "title": title.strip()}
        refs = step.get("evidence")
        if refs is not None:
            if not isinstance(refs, list) or len(refs) > MAX_STEP_EVIDENCE or any(not isinstance(r, str) for r in refs):
                raise Rejected(f"'evidence' é uma lista de até {MAX_STEP_EVIDENCE} ids de trechos.")
            if any(ref not in known for ref in refs):
                raise Rejected("'evidence' só pode citar ids de trechos que foram fornecidos.")
            if refs:
                item["evidence"] = list(dict.fromkeys(refs))
                cited = True
        normalized.append(item)

    if known and not cited:
        raise Rejected("Há trechos do projeto: ao menos uma etapa precisa citar um deles em 'evidence'.")
    return {"schema": PLAN_SCHEMA, "steps": normalized}


def impact_from_model(document: object, known: set[str], has_stacks: bool) -> dict:
    """
    Valida o impacto escrito pelo modelo. Com trechos recuperados, a citação é obrigatória; sem eles, o risco
    ``sem-evidencia`` é obrigatório (o modelo não pode afirmar impacto sem base e esconder isso).
    """
    if not isinstance(document, dict) or document.get("schema") != IMPACT_SCHEMA:
        raise Rejected(f"O objeto precisa ter 'schema' ({IMPACT_SCHEMA}), 'areas', 'risks' e 'evidence'.")
    impact = {key: document.get(key) for key in ("schema", "areas", "risks", "evidence")}
    if set(document) != set(impact):
        raise Rejected("Só 'schema', 'areas', 'risks' e 'evidence' são permitidos.")
    evidence = [{"chunkId": chunk} for chunk in sorted(known)]
    if not impact_ok(impact, evidence):
        raise Rejected(
            f"'areas' usa só {sorted(AREAS)}, 'risks' só {sorted(RISKS)} e 'evidence' só ids de trechos fornecidos."
        )
    impact["areas"] = list(dict.fromkeys(impact["areas"]))
    impact["risks"] = list(dict.fromkeys(impact["risks"]))
    impact["evidence"] = list(dict.fromkeys(impact["evidence"]))
    if known and not impact["evidence"]:
        raise Rejected("Há trechos do projeto: 'evidence' precisa citar ao menos um.")
    if not known and "sem-evidencia" not in impact["risks"]:
        raise Rejected("Não há trechos do projeto: 'risks' precisa incluir 'sem-evidencia'.")
    if not has_stacks and "sem-stack" not in impact["risks"]:
        raise Rejected("Nenhuma stack foi detectada: 'risks' precisa incluir 'sem-stack'.")
    return impact


JUDGMENT_SCHEMA = "engineering.judgment.v1"
JUDGE_EVAL_ID = "EV-JUDGE-001"
CRITERIA_VERSION = "1"
# Critérios da versão 1. O de coerência arquitetural só existe quando o pedido pediu impacto.
CRITERIA = ("plan_quality", "context_fit", "request_fit")
IMPACT_CRITERION = "architecture_coherence"
SCORE_MIN, SCORE_MAX, PASSING_SCORE = 1, 5, 3
MAX_RATIONALE = 300


def judgment_outcome(scores: dict) -> str:
    """``passed`` só se todo critério alcançou a nota mínima. É uma opinião registrada, nunca o veredito."""
    return "passed" if all(score >= PASSING_SCORE for score in scores.values()) else "failed"


def _scores_ok(scores: object, with_impact: bool) -> bool:
    expected = set(CRITERIA) | ({IMPACT_CRITERION} if with_impact else set())
    if not isinstance(scores, dict) or set(scores) != expected:
        return False
    return all(isinstance(v, int) and not isinstance(v, bool) and SCORE_MIN <= v <= SCORE_MAX for v in scores.values())


def _rationale_ok(text: object) -> bool:
    return isinstance(text, str) and 0 < len(text.strip()) <= MAX_RATIONALE and not any(ord(c) < 32 and c not in "\n\t" for c in text)


def judgment_from_model(document: object, known: set[str], with_impact: bool) -> dict:
    """
    Valida a opinião do juiz. Notas inteiras de 1 a 5 em cada critério da versão, justificativa curta e, havendo trechos,
    ao menos uma citação (só de trechos fornecidos). O juiz não escolhe critério nem escala.
    """
    if not isinstance(document, dict) or document.get("schema") != JUDGMENT_SCHEMA:
        raise Rejected(f"O objeto precisa ter 'schema' ({JUDGMENT_SCHEMA}), 'criteriaVersion', 'scores', 'rationale' e 'evidence'.")
    if set(document) != {"schema", "criteriaVersion", "scores", "rationale", "evidence"} or forbidden(document):
        raise Rejected("Só 'schema', 'criteriaVersion', 'scores', 'rationale' e 'evidence' são permitidos.")
    if document["criteriaVersion"] != CRITERIA_VERSION:
        raise Rejected(f"'criteriaVersion' é '{CRITERIA_VERSION}'.")
    if not _scores_ok(document["scores"], with_impact):
        names = list(CRITERIA) + ([IMPACT_CRITERION] if with_impact else [])
        raise Rejected(f"'scores' tem exatamente {names}, cada um com um número inteiro de {SCORE_MIN} a {SCORE_MAX}.")
    if not _rationale_ok(document["rationale"]):
        raise Rejected(f"'rationale' é um texto de até {MAX_RATIONALE} caracteres.")
    refs = document["evidence"]
    if not isinstance(refs, list) or len(refs) > MAX_STEP_EVIDENCE or any(not isinstance(r, str) or r not in known for r in refs):
        raise Rejected("'evidence' só cita ids de trechos que foram fornecidos.")
    if known and not refs:
        raise Rejected("Há trechos do projeto: 'evidence' precisa citar ao menos um.")
    return {
        "schema": JUDGMENT_SCHEMA,
        "criteriaVersion": CRITERIA_VERSION,
        "scores": dict(document["scores"]),
        "rationale": document["rationale"].strip(),
        "evidence": list(dict.fromkeys(refs)),
    }


def judgment_ok(judgment: object, evidence: object, with_impact: bool) -> bool:
    """A mesma conferência, para o que vem do estado (checkpoint) antes de sair no ack."""
    if not isinstance(judgment, dict):
        return False
    core = ("schema", "criteriaVersion", "scores", "rationale", "evidence")
    if set(judgment) != {*core, "model", "modelVersion"}:
        return False
    if not all(isinstance(judgment[key], str) and 0 < len(judgment[key]) <= 100 for key in ("model", "modelVersion")):
        return False
    try:
        judgment_from_model({key: judgment[key] for key in core}, known_chunks(evidence), with_impact)
    except Rejected:
        return False
    return True


ACTION_FILE_WRITE = "file.write"
DECISION_ACTION_ID = "act-decisao-impacto"
_ACTION_ID = re.compile(r"^act-[0-9a-z-]{1,32}$")
_DECISION_TARGET = re.compile(r"^\.mariliz/decisions/ex-[0-9a-f]{12}-impacto\.md$")
_ACTION_KEYS = {"id", "kind", "summary", "target", "evidence", "requiresApproval"}


def decision_action(execution_id: str, impact: dict) -> dict:
    """
    A proposta de registrar a decisão de arquitetura em ``.mariliz/decisions/``. É só uma proposta: não carrega o conteúdo
    do arquivo (o motor o escreve a partir do impacto já validado) nem comando. Precisa de aprovação, sempre.
    """
    return {
        "id": DECISION_ACTION_ID,
        "kind": ACTION_FILE_WRITE,
        "summary": "Registrar a decisão de arquitetura (áreas, riscos e evidências) em .mariliz/decisions",
        "target": f".mariliz/decisions/{execution_id}-impacto.md",
        "evidence": list(impact.get("evidence") or []),
        "requiresApproval": True,
    }


def actions_ok(actions: object, execution_id: str, evidence: object) -> bool:
    """Toda ação do estado tem só os campos do contrato, id único, alvo no lugar fixo e citações de trechos recuperados."""
    if not isinstance(actions, list) or len(actions) > 32:
        return False
    known = known_chunks(evidence)
    seen: set[str] = set()
    for action in actions:
        if not isinstance(action, dict) or not set(action) <= _ACTION_KEYS or forbidden(action):
            return False
        action_id, target = action.get("id"), action.get("target")
        if not isinstance(action_id, str) or not _ACTION_ID.match(action_id) or action_id in seen:
            return False
        if action.get("kind") != ACTION_FILE_WRITE or action.get("requiresApproval") is not True:
            return False
        if target != f".mariliz/decisions/{execution_id}-impacto.md" or not _DECISION_TARGET.match(str(target)):
            return False
        summary = action.get("summary")
        if not isinstance(summary, str) or not 0 < len(summary) <= 200:
            return False
        refs = action.get("evidence")
        if not isinstance(refs, list) or any(not isinstance(ref, str) or ref not in known for ref in refs):
            return False
        seen.add(action_id)
    return True
