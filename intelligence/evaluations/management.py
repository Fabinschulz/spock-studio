"""Evals de gestão (management.eval.v1): perguntas reais de gestão sobre cenários de fatos do motor.

Mede o que importa numa investigação, sem depender de quem escreveu a resposta (modelo falso, roteirizado ou real):

- ``framing``: o primeiro olhar da investigação cobre os grupos de fatos que a pergunta pede;
- ``grounding``: cada afirmação cita fatos que a investigação de fato consultou e só traz números que esses fatos têm (a mesma
  regra do conferente do motor);
- ``coverage``: a resposta cita os fatos essenciais do caso;
- ``abstention``: quando os fatos não têm o que a pergunta pede, a resposta diz que está indisponível e não inventa cálculo,
  inferência nem recomendação.

Uma investigação que falhou pontua zero em tudo o que depende da resposta. Os mínimos ficam no dataset. Para pontuar uma rodada com
modelo real, o teste ``ManagementEvalLiveTests`` (motor) grava os resultados e ``python intelligence/evaluations/management.py score``
os pontua: veja o roadmap.
"""

from __future__ import annotations

import json
import os
import re
import sys
from decimal import Decimal, InvalidOperation
from pathlib import Path
from typing import Any, Callable

if __name__ == "__main__":  # pragma: no cover - uso como script
    _ROOT = Path(__file__).resolve().parents[1]
    for _path in (_ROOT / "packages", _ROOT):
        if str(_path) not in sys.path:
            sys.path.insert(0, str(_path))
    os.environ.setdefault("LANGSMITH_TRACING", "false")

from workflows.investigation.nodes import plain  # noqa: E402

_DATASET = Path(__file__).resolve().parent / "datasets" / "management.json"
# Os grupos de fatos são do motor; a avaliação lê a mesma cópia que o teste do motor confere contra o catálogo dele.
_GROUPS = Path(__file__).resolve().parents[2] / "contracts" / "fixtures" / "management" / "fact-groups.json"


def fact_groups() -> list[dict]:
    return json.loads(_GROUPS.read_text(encoding="utf-8"))


def groups_for(facts: list[dict]) -> list[dict]:
    present = {fact["group"] for fact in facts}
    return [group for group in fact_groups() if group["name"] in present]
_NUMBER = re.compile(r"\d+(?:[.,]\d+)?")
_CLAIM_SECTIONS = ("observed", "calculations", "inferences", "recommendations")
_METRICS = ("framing", "grounding", "coverage", "abstention")
_BROKER = {"url": "http://127.0.0.1:49152/v1/model", "token": "t" * 43}


class ManagementEvalError(Exception):
    pass


def load_dataset(path: Path | None = None) -> dict[str, Any]:
    document = json.loads((path or _DATASET).read_text(encoding="utf-8"))
    if document.get("schema") != "management.eval.v1":
        raise ManagementEvalError("dataset inválido")
    return document


def numbers(text: str) -> set[str]:
    """Os números de um texto, normalizados como o conferente do motor: vírgula vira ponto e zeros sobrando somem ("05" e "5,0" são "5")."""
    found: set[str] = set()
    for raw in _NUMBER.findall(text):
        try:
            found.add(format(Decimal(raw.replace(",", ".")).normalize(), "f"))
        except InvalidOperation:
            found.add(raw)
    return found


def claims_of(answer: dict | None) -> list[dict]:
    """O resumo e cada afirmação das seções, na ordem em que aparecem."""
    if not isinstance(answer, dict):
        return []
    items = [answer["summary"]] if isinstance(answer.get("summary"), dict) else []
    for section in _CLAIM_SECTIONS:
        items.extend(item for item in answer.get(section) or [] if isinstance(item, dict))
    return items


def framing(case: dict, ack: dict) -> float:
    for step in ack.get("path") or []:
        if step.get("id") == "frame":
            looked = {plain(group) for group in step.get("groups") or []}
            return 1.0 if {plain(group) for group in case["focus"]} <= looked else 0.0
    return 0.0


def grounding(answer: dict | None, facts: dict[str, str], consulted: set[str]) -> float:
    """A fração das afirmações ancoradas: cita ao menos um fato consultado e todo número dela está nos fatos que cita."""
    claims = claims_of(answer)
    if not claims:
        return 0.0
    grounded = 0
    for claim in claims:
        cited = [fact_id for fact_id in claim.get("evidence") or [] if fact_id in consulted and fact_id in facts]
        known = set().union(*(numbers(facts[fact_id]) for fact_id in cited)) if cited else set()
        if cited and numbers(str(claim.get("text", ""))) <= known:
            grounded += 1
    return grounded / len(claims)


def coverage(case: dict, answer: dict | None) -> float | None:
    if not case["mustCite"]:
        return None
    cited = {fact_id for claim in claims_of(answer) for fact_id in claim.get("evidence") or []}
    return len(set(case["mustCite"]) & cited) / len(case["mustCite"])


def abstention(case: dict, answer: dict | None) -> float | None:
    """Sem o que a pergunta pede nos fatos: precisa dizer que está indisponível e não inventar cálculo, inferência nem recomendação."""
    if not case["unanswerable"]:
        return None
    if not isinstance(answer, dict) or not answer.get("unavailable"):
        return 0.0
    return 1.0 if not any(answer.get(section) for section in ("calculations", "inferences", "recommendations")) else 0.0


def score_case(case: dict, scenario: list[dict], ack: dict | None) -> dict[str, Any]:
    ack = ack or {}
    answer = ack.get("answer") if ack.get("status") in ("completed", "completed_with_limitations") else None
    facts = {item["id"]: item["text"] for item in scenario}
    consulted = {fact_id for fact_id in ack.get("consulted") or [] if isinstance(fact_id, str)}
    return {
        "id": case["id"],
        "failed": answer is None,
        "framing": framing(case, ack),
        "grounding": grounding(answer, facts, consulted),
        "coverage": 0.0 if answer is None and case["mustCite"] else coverage(case, answer),
        "abstention": 0.0 if answer is None and case["unanswerable"] else abstention(case, answer),
        "claims": len(claims_of(answer)),
        "rejected": len(ack.get("rejected") or []),
    }


def score_dataset(document: dict[str, Any], acks: dict[str, dict]) -> dict[str, Any]:
    """Pontua cada caso (um ack por id; o que falta pontua como investigação que falhou) e confere os mínimos do dataset."""
    results = [score_case(case, document["scenarios"][case["scenario"]], acks.get(case["id"])) for case in document["cases"]]
    means: dict[str, float | None] = {}
    for metric in _METRICS:
        values = [item[metric] for item in results if item[metric] is not None]
        means[metric] = round(sum(values) / len(values), 4) if values else None
    minimums = document["minimums"]
    verdict = {
        metric: {"value": means[metric], "minimum": minimums[metric], "ok": means[metric] is None or means[metric] >= minimums[metric]}
        for metric in _METRICS
    }
    return {"cases": results, "means": means, "verdict": verdict, "passed": all(item["ok"] for item in verdict.values())}


def request_for(document: dict[str, Any], case: dict, with_model: bool) -> dict:
    """O ``ai.investigation.start.v2`` de um caso: a pergunta e os fatos do cenário. Com modelo, o proxy é o do motor (aqui, um endereço de teste)."""
    payload: dict[str, Any] = {
        "schema": "ai.investigation.start.v2",
        "investigationId": "inv-eeeeeeeeeeee",
        "executionId": "ex-eeeeeeeeeeee",
        "projectId": "p-eeeeeeeeeeee",
        "question": case["question"],
        "facts": document["scenarios"][case["scenario"]],
        "groups": groups_for(document["scenarios"][case["scenario"]]),
        "policy": {"modelAllowed": with_model},
        "limits": {"maxIterations": 12, "maxMilliseconds": 150000, "maxTokens": 40000},
    }
    if with_model:
        payload["broker"] = dict(_BROKER)
    return payload


def run_dataset(document: dict[str, Any] | None = None, model_for: Callable[[dict], Any] | None = None) -> dict[str, dict]:
    """
    Roda a investigação de cada caso. ``model_for(case)`` devolve o modelo daquele caso (um falso roteirizado nos testes); sem ele, a
    investigação roda sem modelo. Devolve um ack por id de caso.
    """
    from workflows.investigation import graph

    doc = document or load_dataset()
    acks: dict[str, dict] = {}
    for case in doc["cases"]:
        model = model_for(case) if model_for else None
        payload = request_for(doc, case, model is not None)
        factory = (lambda _request, fake=model: fake) if model is not None else None
        acks[case["id"]] = list(graph.run_investigation(payload, model_factory=factory))[-1]
    return acks


def format_report(report: dict[str, Any]) -> str:
    lines = ["caso".ljust(28) + "".join(metric[:10].ljust(12) for metric in _METRICS) + "afirmações"]
    for item in report["cases"]:
        cells = "".join(("-" if item[metric] is None else f"{item[metric]:.2f}").ljust(12) for metric in _METRICS)
        lines.append(item["id"].ljust(28) + cells + f"{item['claims']}" + (" (falhou)" if item["failed"] else ""))
    lines.append("")
    for metric, state in report["verdict"].items():
        value = "n/a" if state["value"] is None else f"{state['value']:.2f}"
        lines.append(f"{metric.ljust(10)} média {value}  mínimo {state['minimum']:.2f}  {'ok' if state['ok'] else 'ABAIXO DO MÍNIMO'}")
    lines.append("resultado: " + ("passou" if report["passed"] else "não passou"))
    return "\n".join(lines)


def main(argv: list[str]) -> int:
    if len(argv) != 2 or argv[0] != "score":
        print("uso: python management.py score <acks.json>", file=sys.stderr)
        return 2
    acks = json.loads(Path(argv[1]).read_text(encoding="utf-8"))
    if not isinstance(acks, dict):
        print("o arquivo precisa ser um objeto {idDoCaso: ack}", file=sys.stderr)
        return 2
    report = score_dataset(load_dataset(), acks)
    print(format_report(report))
    return 0 if report["passed"] else 1


if __name__ == "__main__":  # pragma: no cover - uso como script
    raise SystemExit(main(sys.argv[1:]))
