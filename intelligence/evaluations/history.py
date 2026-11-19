"""Histórico das métricas dos evals, uma linha JSON por medição, para ver a tendência e não só o passa/falha do dia.

O motor .NET grava no mesmo arquivo o eval de arquitetura (``ArchitectureEvaluationTests`` com ``MARILIZ_RECORD_EVALS=1``).
"""

from __future__ import annotations

import datetime
import json
from pathlib import Path
from typing import Any

PATH = Path(__file__).resolve().parent / "history.jsonl"


def record(suite: str, metrics: dict[str, Any], path: Path = PATH, today: datetime.date | None = None) -> dict[str, Any]:
    entry = {"suite": suite, "measuredAt": (today or datetime.date.today()).isoformat(), "metrics": metrics}
    with path.open("a", encoding="utf-8", newline="\n") as handle:
        handle.write(json.dumps(entry, ensure_ascii=False, sort_keys=True, separators=(",", ":")) + "\n")
    return entry


def read(suite: str, path: Path = PATH) -> list[dict[str, Any]]:
    if not path.exists():
        return []
    entries = [json.loads(line) for line in path.read_text(encoding="utf-8").splitlines() if line.strip()]
    return [entry for entry in entries if entry.get("suite") == suite]
