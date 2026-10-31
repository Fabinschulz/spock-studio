"""Checkpoint da execução, separado do índice de conhecimento.

Um SQLite por projeto, só para retomar um grafo pausado. Não é o índice
lexical e não fica na pasta do projeto.
"""

from __future__ import annotations

import json
import re
import sqlite3
import threading
from contextlib import contextmanager
from pathlib import Path

_PROJECT_ID = re.compile(r"^p-[0-9a-f]{12}$")
_RUN_ID = re.compile(r"^run-[0-9a-f]{12}$")
_MAX_BODY = 200_000
_locks: dict[str, threading.Lock] = {}
_locks_guard = threading.Lock()


class ExecutionError(Exception):
    pass


def save(root: str, project_id: str, run_id: str, state: dict) -> None:
    path = _file(root, project_id)
    _check_run(run_id)
    body = json.dumps(state, separators=(",", ":"), ensure_ascii=False)
    if len(body) > _MAX_BODY:
        raise ExecutionError("checkpoint grande demais")
    with _session(path) as conn:
        conn.execute(
            "INSERT OR REPLACE INTO checkpoints(run_id, project_id, body) VALUES (?, ?, ?)",
            (run_id, project_id, body),
        )


def load(root: str, project_id: str, run_id: str) -> dict | None:
    path = _file(root, project_id)
    _check_run(run_id)
    if not path.exists():
        return None
    with _session(path) as conn:
        row = conn.execute(
            "SELECT project_id, body FROM checkpoints WHERE run_id = ?",
            (run_id,),
        ).fetchone()
    if row is None or row[0] != project_id:
        return None
    try:
        state = json.loads(row[1])
    except json.JSONDecodeError:
        return None
    return state if isinstance(state, dict) else None


def delete(root: str, project_id: str, run_id: str) -> None:
    path = _file(root, project_id)
    _check_run(run_id)
    if not path.exists():
        return
    with _session(path) as conn:
        conn.execute("DELETE FROM checkpoints WHERE run_id = ? AND project_id = ?", (run_id, project_id))
        left = conn.execute("SELECT COUNT(*) FROM checkpoints").fetchone()[0]
    if left == 0:
        path.unlink(missing_ok=True)


def _check_run(run_id: str) -> None:
    if not isinstance(run_id, str) or not _RUN_ID.match(run_id):
        raise ExecutionError("execução inválida")


def _file(root: str, project_id: str) -> Path:
    if not isinstance(project_id, str) or not _PROJECT_ID.match(project_id):
        raise ExecutionError("projeto inválido")
    if not isinstance(root, str) or not root:
        raise ExecutionError("memória inválida")
    base = Path(root).resolve()
    target = (base / "projects" / project_id / "execution").resolve()
    checkpoint = target / "checkpoint.sqlite"
    if base not in checkpoint.parents:
        raise ExecutionError("memória inválida")
    target.mkdir(parents=True, exist_ok=True)
    return checkpoint


def _guard(path: Path) -> threading.Lock:
    key = str(path)
    with _locks_guard:
        lock = _locks.get(key)
        if lock is None:
            lock = threading.Lock()
            _locks[key] = lock
        return lock


@contextmanager
def _session(path: Path):
    with _guard(path):
        conn = sqlite3.connect(path)
        try:
            conn.execute(
                """
                CREATE TABLE IF NOT EXISTS checkpoints (
                    run_id TEXT PRIMARY KEY,
                    project_id TEXT NOT NULL,
                    body TEXT NOT NULL
                )
                """
            )
            yield conn
            conn.commit()
        finally:
            conn.close()
