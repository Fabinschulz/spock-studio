"""Memória de execução: checkpoints do LangGraph, separados do índice de conhecimento.

Um SQLite por projeto em ``<raiz>/projects/<projectId>/execution/checkpoint.sqlite``, fora da pasta do projeto. Cada
execução é um ``thread_id`` (o runId). O arquivo some quando não resta execução pausada nele. O diário de negócio
continua no motor.
"""

from __future__ import annotations

import re
import sqlite3
import threading
from contextlib import contextmanager
from pathlib import Path
from typing import Iterator

from langgraph.checkpoint.sqlite import SqliteSaver

_PROJECT_ID = re.compile(r"^p-[0-9a-f]{12}$")
_RUN_ID = re.compile(r"^run-[0-9a-f]{12}$")
_locks: dict[str, threading.Lock] = {}
_locks_guard = threading.Lock()


class ExecutionError(Exception):
    pass


def checkpoint_file(root: str, project_id: str) -> Path:
    if not isinstance(project_id, str) or not _PROJECT_ID.match(project_id):
        raise ExecutionError("projeto inválido")
    if not isinstance(root, str) or not root:
        raise ExecutionError("memória inválida")
    base = Path(root).resolve()
    target = (base / "projects" / project_id / "execution").resolve()
    checkpoint = target / "checkpoint.sqlite"
    if base not in checkpoint.parents:
        raise ExecutionError("memória inválida")
    return checkpoint


@contextmanager
def saver(root: str, project_id: str, run_id: str) -> Iterator[SqliteSaver]:
    """Uma conexão por uso: fecha antes de devolver, para o arquivo poder ser apagado (Windows)."""
    _check_run(run_id)
    path = checkpoint_file(root, project_id)
    with _guard(path):
        path.parent.mkdir(parents=True, exist_ok=True)
        conn = sqlite3.connect(path, check_same_thread=False)
        try:
            yield SqliteSaver(conn)
            conn.commit()
        finally:
            conn.close()


def drop(root: str, project_id: str, run_id: str) -> None:
    _check_run(run_id)
    path = checkpoint_file(root, project_id)
    if not path.exists():
        return
    with _guard(path):
        conn = sqlite3.connect(path, check_same_thread=False)
        try:
            checkpoints = SqliteSaver(conn)
            checkpoints.setup()
            checkpoints.delete_thread(run_id)
            left = conn.execute("SELECT COUNT(*) FROM checkpoints").fetchone()[0]
            conn.commit()
        finally:
            conn.close()
        if left == 0:
            path.unlink(missing_ok=True)


def _check_run(run_id: str) -> None:
    if not isinstance(run_id, str) or not _RUN_ID.match(run_id):
        raise ExecutionError("execução inválida")


def _guard(path: Path) -> threading.Lock:
    key = str(path)
    with _locks_guard:
        lock = _locks.get(key)
        if lock is None:
            lock = threading.Lock()
            _locks[key] = lock
        return lock
