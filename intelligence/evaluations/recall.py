"""Recall@k e MRR da recuperação de contexto (rag.recall.v2).

Cada expectativa é um arquivo e um trecho que o chunk recuperado precisa conter, então o formato do chunkId pode mudar
sem invalidar o dataset. ``baseline`` guarda o último resultado aceito: uma mudança na recuperação não pode piorar.
"""

from __future__ import annotations

import hashlib
import json
from pathlib import Path
from typing import Any, Callable

from knowledge import index as rag
from knowledge import retrieval

_DATASET = Path(__file__).resolve().parent / "datasets" / "recall.json"
_MRR_DEPTH = 10

Search = Callable[[str, str, str, int], list[dict]]


class RecallEvalError(Exception):
    pass


def recall_at_k(retrieved: list[bool], k: int) -> float:
    return 1.0 if any(retrieved[: max(k, 1)]) else 0.0


def reciprocal_rank(retrieved: list[bool]) -> float:
    for position, relevant in enumerate(retrieved, start=1):
        if relevant:
            return 1 / position
    return 0.0


def load_dataset(path: Path | None = None) -> dict[str, Any]:
    document = json.loads((path or _DATASET).read_text(encoding="utf-8"))
    if document.get("schema") != "rag.recall.v2":
        raise RecallEvalError("dataset inválido")
    return document


def evaluate_case(index_root: str, document: dict[str, Any], case: dict[str, Any], search: Search) -> dict[str, Any]:
    project_id = case["projectId"]
    sources = case.get("sources") or document["corpora"][case["corpus"]]
    rag.sync(index_root, project_id, _hashed(sources))
    if case.get("resync"):
        rag.sync(index_root, project_id, _hashed(case["resync"]))
    hits = search(index_root, project_id, case["query"], _MRR_DEPTH)
    recalls, ranks = [], []
    for expected in case["expect"]:
        relevant = [
            hit["filePath"] == expected["filePath"]
            and expected["anchor"] in (rag.chunk_body(index_root, project_id, hit["chunkId"]) or "")
            for hit in hits
        ]
        recalls.append(recall_at_k(relevant, int(document["k"])))
        ranks.append(reciprocal_rank(relevant))
    return {"id": case["id"], "recall": sum(recalls) / len(recalls), "mrr": sum(ranks) / len(ranks)}


def run_dataset(index_root: str, document: dict[str, Any] | None = None, search: Search | None = None) -> dict[str, Any]:
    doc = document or load_dataset()
    run = search or (lambda root, project, query, limit: retrieval.search(root, project, query, limit))
    results = [evaluate_case(index_root, doc, case, run) for case in doc["cases"]]
    count = max(len(results), 1)
    return {
        "k": int(doc["k"]),
        "recallAtK": round(sum(item["recall"] for item in results) / count, 4),
        "mrr": round(sum(item["mrr"] for item in results) / count, 4),
        "cases": results,
    }


def _hashed(sources: list[dict]) -> list[dict]:
    return [{**item, "hash": hashlib.sha256(item["text"].encode("utf-8")).hexdigest()} for item in sources]
