"""Uma investigação que falhou pontua zero em tudo o que depende da resposta; os mínimos ficam no dataset. Para pontuar uma
rodada com modelo real, o teste ``ManagementEvalLiveTests`` (motor) grava os resultados e
``python intelligence/evaluations/management.py score`` os pontua.

A primeira rodada real aceita vira a linha de base do provedor (``baseline``): as rodadas seguintes se medem contra
ela, para que um ajuste de prompt não piore o que já funcionava sem ninguém perceber. A linha de base guarda só
números e ids de caso, nunca o texto das respostas.
"""

from __future__ import annotations

import hashlib
import json
import os
import re
import sys
from datetime import date
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
_BASELINES = _DATASET.parent
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
_PROVIDER = re.compile(r"[a-z][a-z0-9-]{1,30}")
_RESULTS_NAME = re.compile(r"management-eval-([a-z][a-z0-9-]{1,30})\.json")
# Um modelo real não responde igual duas vezes; abaixo disto é variação, acima é regressão.
BASELINE_TOLERANCE = 0.05


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


def fell_back(ack: dict | None) -> bool:
    """A conclusão veio das regras, não do modelo (prazo, teto, resposta inválida): a nota mede o plano de reserva, não o modelo."""
    limitations = (ack or {}).get("limitations") or []
    return any(isinstance(item, dict) and item.get("code") == "INVESTIGATION_CONCLUSION_FALLBACK" for item in limitations)


def score_case(case: dict, scenario: list[dict], ack: dict | None) -> dict[str, Any]:
    ack = ack or {}
    answer = ack.get("answer") if ack.get("status") in ("completed", "completed_with_limitations") else None
    facts = {item["id"]: item["text"] for item in scenario}
    consulted = {fact_id for fact_id in ack.get("consulted") or [] if isinstance(fact_id, str)}
    return {
        "id": case["id"],
        "failed": answer is None,
        "fallback": fell_back(ack),
        "framing": framing(case, ack),
        "grounding": grounding(answer, facts, consulted),
        "coverage": 0.0 if answer is None and case["mustCite"] else coverage(case, answer),
        "abstention": 0.0 if answer is None and case["unanswerable"] else abstention(case, answer),
        "claims": len(claims_of(answer)),
        "rejected": len(ack.get("rejected") or []),
    }


def score_dataset(document: dict[str, Any], acks: dict[str, dict]) -> dict[str, Any]:
    """Um id sem ack pontua como investigação que falhou."""
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
    # Uma rodada em que o modelo não escreveu a conclusão de algum caso é inconclusiva: não passa, e não vira linha de base.
    fallbacks = [item["id"] for item in results if item["fallback"]]
    return {
        "cases": results,
        "means": means,
        "verdict": verdict,
        "fallbacks": fallbacks,
        "passed": all(item["ok"] for item in verdict.values()) and not fallbacks,
    }


def request_for(document: dict[str, Any], case: dict, with_model: bool) -> dict:
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
        mark = " (falhou)" if item["failed"] else " (conclusão das regras, não do modelo)" if item["fallback"] else ""
        lines.append(item["id"].ljust(28) + cells + f"{item['claims']}" + mark)
    lines.append("")
    for metric, state in report["verdict"].items():
        value = "n/a" if state["value"] is None else f"{state['value']:.2f}"
        lines.append(f"{metric.ljust(10)} média {value}  mínimo {state['minimum']:.2f}  {'ok' if state['ok'] else 'ABAIXO DO MÍNIMO'}")
    if report["fallbacks"]:
        lines.append(
            f"INCONCLUSIVA: em {len(report['fallbacks'])} de {len(report['cases'])} casos o modelo não escreveu a conclusão "
            "(prazo, teto ou resposta inválida); as notas acima medem o plano de reserva, não o modelo."
        )
    lines.append("resultado: " + ("passou" if report["passed"] else "inconclusiva" if report["fallbacks"] else "não passou"))
    return "\n".join(lines)


def dataset_digest(path: Path | None = None) -> str:
    # Pelo conteúdo e não pelo arquivo: fim de linha diferente entre máquinas não pode invalidar a linha de base.
    document = json.loads((path or _DATASET).read_text(encoding="utf-8"))
    canonical = json.dumps(document, ensure_ascii=False, sort_keys=True, separators=(",", ":"))
    return hashlib.sha256(canonical.encode("utf-8")).hexdigest()


def baseline_path(provider: str, folder: Path | None = None) -> Path:
    if not _PROVIDER.fullmatch(provider):
        raise ManagementEvalError("provedor inválido")
    return (folder or _BASELINES) / f"management-baseline-{provider}.json"


def provider_of(results: Path) -> str | None:
    match = _RESULTS_NAME.fullmatch(results.name)
    return match.group(1) if match else None


def make_baseline(report: dict[str, Any], provider: str, recorded_on: str, digest: str) -> dict[str, Any]:
    return {
        "schema": "management.eval.baseline.v1",
        "provider": provider,
        "recordedOn": recorded_on,
        "dataset": digest,
        "means": report["means"],
        "cases": [{"id": item["id"], **{metric: item[metric] for metric in _METRICS}, "failed": item["failed"]} for item in report["cases"]],
    }


def compare_to_baseline(report: dict[str, Any], baseline: dict[str, Any], digest: str) -> dict[str, Any]:
    """Uma média que cai mais que a tolerância abaixo da linha de base é regressão. Dataset mudado: a linha de base não vale."""
    if baseline.get("schema") != "management.eval.baseline.v1":
        raise ManagementEvalError("linha de base inválida")
    if baseline.get("dataset") != digest:
        return {"applies": False, "recordedOn": baseline.get("recordedOn"), "metrics": {}, "regressed": False}
    metrics: dict[str, dict[str, Any]] = {}
    for metric in _METRICS:
        now, before = report["means"][metric], baseline["means"].get(metric)
        delta = None if now is None or before is None else round(now - before, 4)
        metrics[metric] = {"value": now, "baseline": before, "delta": delta, "regressed": delta is not None and delta < -BASELINE_TOLERANCE}
    return {
        "applies": True,
        "recordedOn": baseline.get("recordedOn"),
        "metrics": metrics,
        "regressed": any(item["regressed"] for item in metrics.values()),
    }


def format_comparison(provider: str, comparison: dict[str, Any] | None) -> str:
    if comparison is None:
        return f"linha de base de {provider}: nenhuma ainda. Se esta rodada for aceita, grave-a com o comando baseline."
    if not comparison["applies"]:
        return f"linha de base de {provider} ({comparison['recordedOn']}): feita com outro dataset, não se aplica. Grave uma nova."
    lines = [f"linha de base de {provider} ({comparison['recordedOn']}), tolerância {BASELINE_TOLERANCE:.2f}:"]
    for metric, state in comparison["metrics"].items():
        if state["delta"] is None:
            lines.append(f"{metric.ljust(10)} sem comparação")
            continue
        verdict = "PIOROU" if state["regressed"] else "ok"
        lines.append(f"{metric.ljust(10)} {state['baseline']:.2f} -> {state['value']:.2f}  ({state['delta']:+.2f})  {verdict}")
    return "\n".join(lines)


def _read_acks(path: Path) -> dict[str, dict] | None:
    acks = json.loads(path.read_text(encoding="utf-8"))
    if not isinstance(acks, dict):
        print("o arquivo precisa ser um objeto {idDoCaso: ack}", file=sys.stderr)
        return None
    return acks


def _score_command(results: Path, provider: str | None) -> int:
    acks = _read_acks(results)
    if acks is None:
        return 2
    report = score_dataset(load_dataset(), acks)
    print(format_report(report))
    regressed = False
    if provider:
        stored = baseline_path(provider)
        comparison = None
        if stored.exists():
            comparison = compare_to_baseline(report, json.loads(stored.read_text(encoding="utf-8")), dataset_digest())
            regressed = comparison["regressed"]
        print()
        print(format_comparison(provider, comparison))
    return 0 if report["passed"] and not regressed else 1


def _baseline_command(results: Path, provider: str) -> int:
    acks = _read_acks(results)
    if acks is None:
        return 2
    report = score_dataset(load_dataset(), acks)
    if any(item["failed"] or item["fallback"] for item in report["cases"]):
        # Uma rodada com investigação que falhou ou que caiu no plano de reserva (rede, chave, prazo, teto) mede a infraestrutura, não o modelo.
        print("rodada com caso que falhou ou sem conclusão do modelo: rode de novo antes de gravar a linha de base", file=sys.stderr)
        return 1
    target = baseline_path(provider)
    baseline = make_baseline(report, provider, date.today().isoformat(), dataset_digest())
    target.write_text(json.dumps(baseline, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(format_report(report))
    print(f"\nlinha de base de {provider} gravada em {target}")
    return 0


def main(argv: list[str]) -> int:
    usage = "uso: python management.py score <acks.json> [provedor] | baseline <acks.json> [provedor]"
    if len(argv) not in (2, 3) or argv[0] not in ("score", "baseline"):
        print(usage, file=sys.stderr)
        return 2
    results = Path(argv[1])
    provider = argv[2] if len(argv) == 3 else provider_of(results)
    if provider is not None and not _PROVIDER.fullmatch(provider):
        print("provedor inválido: use letras minúsculas, como openai ou anthropic", file=sys.stderr)
        return 2
    if argv[0] == "score":
        return _score_command(results, provider)
    if provider is None:
        print("informe o provedor: o nome do arquivo não segue management-eval-<provedor>.json", file=sys.stderr)
        return 2
    return _baseline_command(results, provider)


if __name__ == "__main__":  # pragma: no cover - uso como script
    raise SystemExit(main(sys.argv[1:]))
