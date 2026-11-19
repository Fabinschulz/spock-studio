"""Recall e contaminação das referências da pasta Organização, no índice real e com o filtro de stack da busca.

O corpus (``reference-corpus.json``) é gerado pelo motor a partir dos documentos ``@reference/`` que ele indexa, e um teste
.NET falha se ficar diferente. Recall: um dos ``REF-*`` esperados no corpo de um dos k primeiros trechos. Contaminação:
trecho de seção marcada com outra stack entre os k primeiros; a medida sem filtro mostra quanto o filtro evita.

Uso: ``python intelligence/evaluations/references.py [--record]``. ``--record`` acrescenta a medição ao histórico.
"""

from __future__ import annotations

import json
import sys
import tempfile
from pathlib import Path
from typing import Any

if __package__ in (None, ""):
    sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from evaluations import history  # noqa: E402
from evaluations.recall import recall_at_k, reciprocal_rank  # noqa: E402
from knowledge import index as rag  # noqa: E402
from knowledge import retrieval  # noqa: E402

_DATASETS = Path(__file__).resolve().parent / "datasets"
DATASET = _DATASETS / "references.json"
CORPUS = _DATASETS / "reference-corpus.json"
SUITE = "references"
_PROJECT = "p-0e9a1c2d3b4f"
_DEPTH = 10


class ReferenceEvalError(Exception):
    pass


def load(dataset: Path = DATASET, corpus: Path = CORPUS) -> tuple[dict[str, Any], list[dict]]:
    document = json.loads(dataset.read_text(encoding="utf-8"))
    sources = json.loads(corpus.read_text(encoding="utf-8"))
    if document.get("schema") != "rag.references.v1" or sources.get("schema") != "rag.reference-corpus.v1":
        raise ReferenceEvalError("dataset ou corpus inválido")
    return document, sources["documents"]


def run(document: dict[str, Any], sources: list[dict]) -> dict[str, Any]:
    k = int(document["k"])
    stacks = {source["path"]: rag.technologies_of(source["path"], source["text"])[0] for source in sources}
    cases = []
    with tempfile.TemporaryDirectory() as root:
        rag.sync(root, _PROJECT, sources)
        for case in document["cases"]:
            wanted = case["technologies"]
            hits = retrieval.search(root, _PROJECT, case["query"], _DEPTH, wanted)
            unfiltered = retrieval.search(root, _PROJECT, case["query"], k)
            relevant = [any(ref in _body(root, hit) for ref in case["expect"]) for hit in hits]
            cases.append({
                "id": case["id"],
                "recall": recall_at_k(relevant, k),
                "mrr": reciprocal_rank(relevant),
                "contaminated": sum(1 for hit in hits[:k] if _foreign(stacks.get(hit["filePath"], []), wanted)),
                "contaminatedUnfiltered": sum(1 for hit in unfiltered if _foreign(stacks.get(hit["filePath"], []), wanted)),
                "top": [hit["filePath"].rsplit("/", 1)[-1] for hit in hits[:k]],
            })
    count = max(len(cases), 1)
    return {
        "k": k,
        "cases": cases,
        "metrics": {
            "cases": len(cases),
            "recallAtK": round(sum(case["recall"] for case in cases) / count, 4),
            "mrr": round(sum(case["mrr"] for case in cases) / count, 4),
            "contaminationAtK": round(sum(case["contaminated"] for case in cases) / (count * k), 4),
            "contaminationAtKUnfiltered": round(sum(case["contaminatedUnfiltered"] for case in cases) / (count * k), 4),
        },
    }


def _body(root: str, hit: dict) -> str:
    return rag.chunk_body(root, _PROJECT, hit["chunkId"]) or ""


def _foreign(document_stacks: list[str], wanted: list[str]) -> bool:
    return bool(document_stacks) and not set(document_stacks) & set(wanted)


if __name__ == "__main__":
    report = run(*load())
    for case in report["cases"]:
        mark = "ok" if case["recall"] else "fora"
        print(f"{mark:>4}  contaminação {case['contaminated']} (sem filtro {case['contaminatedUnfiltered']})  {case['id']}  {case['top']}")
    print(json.dumps(report["metrics"], ensure_ascii=False))
    if "--record" in sys.argv[1:]:
        history.record(SUITE, report["metrics"])
