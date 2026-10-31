"""Recall@k sobre o índice lexical — baseline antes de embeddings."""

from __future__ import annotations

import json
from pathlib import Path
from typing import Any

from knowledge import index as rag

_DATASET = Path(__file__).resolve().parent / "datasets" / "recall.json"


class RecallEvalError(Exception):
    pass


def recall_at_k(retrieved: list[str], expected: list[str], k: int) -> float:
    if not expected:
        return 1.0
    top = set(retrieved[: max(k, 1)])
    hits = sum(1 for item in expected if item in top)
    return hits / len(expected)


def evaluate_case(index_root: str, case: dict[str, Any], k: int) -> float:
    project_id = case["projectId"]
    rag.sync(index_root, project_id, case["sources"])
    resync = case.get("resync")
    if isinstance(resync, dict) and isinstance(resync.get("sources"), list):
        rag.sync(index_root, project_id, resync["sources"])
    hits = rag.search(index_root, project_id, case["query"], limit=k)
    retrieved = [item["chunkId"] for item in hits]
    return recall_at_k(retrieved, case["expectChunkIds"], k)


def load_dataset(path: Path | None = None) -> dict[str, Any]:
    document = json.loads((path or _DATASET).read_text(encoding="utf-8"))
    if document.get("schema") != "rag.recall.v1":
        raise RecallEvalError("dataset inválido")
    return document


def run_dataset(index_root: str, document: dict[str, Any] | None = None) -> dict[str, Any]:
    doc = document or load_dataset()
    k = int(doc.get("k", 3))
    results: list[dict[str, Any]] = []
    for case in doc["cases"]:
        score = evaluate_case(index_root, case, k)
        results.append({"id": case["id"], "recall": score})
    total = sum(item["recall"] for item in results) / max(len(results), 1)
    return {"k": k, "meanRecall": total, "cases": results}
