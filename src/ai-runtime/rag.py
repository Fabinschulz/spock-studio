"""Índice lexical por projeto.

O motor envia só os textos que já passou pela política. Este módulo grava o índice
fora do projeto, um SQLite por projectId, e não chama modelo nem embeddings.
"""

from __future__ import annotations

import json
import re
import sqlite3
import threading
from contextlib import contextmanager
from pathlib import Path

_PROJECT_ID = re.compile(r"^p-[0-9a-f]{12}$")
_HASH = re.compile(r"^[0-9a-f]{64}$")
_TOKEN = re.compile(r"[^\W_]{4,}", re.UNICODE)
_BLOCKED_DIRS = {
    "node_modules", "bin", "obj", ".git", "dist", "build", ".next", "target", ".venv", "venv", "__pycache__",
}
_locks: dict[str, threading.Lock] = {}
_locks_guard = threading.Lock()


class KnowledgeError(Exception):
    pass


def manifest(index_root: str, project_id: str) -> list[dict]:
    path = _index_file(index_root, project_id)
    if not path.exists():
        return []
    with _session(path) as conn:
        rows = conn.execute("SELECT path, content_hash FROM documents").fetchall()
    return [{"path": row[0], "hash": row[1]} for row in rows]


def sync(index_root: str, project_id: str, sources: list) -> dict:
    path = _index_file(index_root, project_id)
    incoming = [_source(item) for item in sources]
    incoming = [item for item in incoming if item is not None]
    with _session(path) as conn:
        known = {row[0]: row[1] for row in conn.execute("SELECT path, content_hash FROM documents")}
        seen: set[str] = set()
        updated = unchanged = 0
        for item in incoming:
            seen.add(item["path"])
            if known.get(item["path"]) == item["hash"]:
                unchanged += 1
                continue
            if "text" not in item:
                continue
            _replace(conn, item)
            updated += 1
        removed = 0
        for old in list(known):
            if old in seen:
                continue
            _delete(conn, old)
            removed += 1
        documents = conn.execute("SELECT COUNT(*) FROM documents").fetchone()[0]
    return {"updated": updated, "unchanged": unchanged, "removed": removed, "documents": documents}


def search(index_root: str, project_id: str, goal: str, limit: int = 5) -> list[dict]:
    tokens = []
    for token in _TOKEN.findall(goal or ""):
        lowered = token.lower()
        if lowered not in tokens:
            tokens.append(lowered)
        if len(tokens) == 8:
            break
    if not tokens:
        return []
    path = _index_file(index_root, project_id)
    if not path.exists():
        return []
    match = " OR ".join(tokens)
    with _session(path) as conn:
        try:
            rows = conn.execute(
                """
                SELECT path, chunk_id, content_hash, snippet(chunks_fts, 0, '', '', '…', 12), bm25(chunks_fts)
                FROM chunks_fts
                WHERE chunks_fts MATCH ? AND project_id = ?
                ORDER BY bm25(chunks_fts)
                LIMIT ?
                """,
                (match, project_id, limit),
            ).fetchall()
        except sqlite3.OperationalError as ex:
            raise KnowledgeError("consulta inválida") from ex
    hits = []
    for file_path, chunk_id, digest, excerpt, score in rows:
        hits.append({
            "documentId": file_path,
            "chunkId": chunk_id,
            "filePath": file_path,
            "hash": digest,
            "score": round(-float(score), 4),
            "excerpt": (excerpt or "")[:160],
        })
    return hits


def _source(item: object) -> dict | None:
    if not isinstance(item, dict):
        return None
    path = item.get("path")
    digest = item.get("hash")
    if not isinstance(path, str) or not isinstance(digest, str) or not _HASH.match(digest) or not _allowed(path):
        return None
    source = {"path": path, "hash": digest}
    text = item.get("text")
    if isinstance(text, str) and text.strip() and "\0" not in text:
        source["text"] = text[:24_000]
    return source


def _allowed(path: str) -> bool:
    if not path or path.startswith(("/", "\\")) or "\\" in path or ".." in path.split("/"):
        return False
    parts = [part for part in path.split("/") if part]
    if not parts or any(part.lower() in _BLOCKED_DIRS for part in parts):
        return False
    name = parts[-1].lower()
    if name == ".env" or name.startswith(".env.") or name.endswith((".pem", ".key", ".p12", ".pfx", ".png", ".exe", ".dll")):
        return False
    return True


def _index_file(index_root: str, project_id: str) -> Path:
    if not isinstance(project_id, str) or not _PROJECT_ID.match(project_id):
        raise KnowledgeError("projeto inválido")
    if not isinstance(index_root, str) or not index_root:
        raise KnowledgeError("índice inválido")
    root = Path(index_root).resolve()
    target = (root / project_id).resolve()
    if target.parent != root:
        raise KnowledgeError("índice inválido")
    target.mkdir(parents=True, exist_ok=True)
    return target / "index.sqlite"


def _guard(path: Path) -> threading.Lock:
    key = str(path)
    with _locks_guard:
        lock = _locks.get(key)
        if lock is None:
            lock = threading.Lock()
            _locks[key] = lock
        return lock


def _connect(path: Path) -> sqlite3.Connection:
    conn = sqlite3.connect(path)
    conn.execute("PRAGMA foreign_keys = ON")
    return conn


@contextmanager
def _session(path: Path):
    with _guard(path):
        conn = _connect(path)
        try:
            _ensure(conn)
            yield conn
            conn.commit()
        finally:
            conn.close()


def _ensure(conn: sqlite3.Connection) -> None:
    conn.execute(
        """
        CREATE TABLE IF NOT EXISTS documents (
            path TEXT PRIMARY KEY,
            content_hash TEXT NOT NULL
        )
        """
    )
    conn.execute(
        """
        CREATE VIRTUAL TABLE IF NOT EXISTS chunks_fts USING fts5(
            body,
            path UNINDEXED,
            chunk_id UNINDEXED,
            content_hash UNINDEXED,
            project_id UNINDEXED,
            tokenize = 'unicode61'
        )
        """
    )


def _replace(conn: sqlite3.Connection, item: dict) -> None:
    _delete(conn, item["path"])
    conn.execute(
        "INSERT INTO documents(path, content_hash) VALUES (?, ?)",
        (item["path"], item["hash"]),
    )
    for index, body in enumerate(_chunks(item["path"], item["text"]), start=1):
        conn.execute(
            """
            INSERT INTO chunks_fts(body, path, chunk_id, content_hash, project_id)
            VALUES (?, ?, ?, ?, ?)
            """,
            (body, item["path"], f"{item['path']}#{index}", item["hash"], _project_of(conn)),
        )


def _project_of(conn: sqlite3.Connection) -> str:
    # O arquivo já é exclusivo do projectId; a coluna repete o id para a consulta não cruzar linhas.
    name = Path(conn.execute("PRAGMA database_list").fetchone()[2]).parent.name
    if not _PROJECT_ID.match(name):
        raise KnowledgeError("projeto inválido")
    return name


def _delete(conn: sqlite3.Connection, path: str) -> None:
    conn.execute("DELETE FROM chunks_fts WHERE path = ?", (path,))
    conn.execute("DELETE FROM documents WHERE path = ?", (path,))


def _chunks(path: str, text: str) -> list[str]:
    ext = path.rsplit(".", 1)[-1].lower()
    if ext in {"md", "markdown"}:
        parts = re.split(r"(?m)^(?=#{1,6}\s)", text)
    elif ext == "json":
        parts = _json_parts(text)
    elif ext in {"yml", "yaml"}:
        parts = re.split(r"(?m)^(?=\S[^:\n]*:)", text)
    elif ext in {"cs", "ts", "tsx", "js", "jsx", "py", "java", "kt"}:
        parts = re.split(
            r"(?m)^(?=(?:public |private |protected |internal |static |class |def |function |export |async ))",
            text,
        )
    else:
        parts = re.split(r"\n\s*\n", text)
    cleaned = []
    for part in parts:
        part = part.strip()
        if len(part) < 20:
            continue
        cleaned.append(part[:1200])
        if len(cleaned) == 12:
            break
    if not cleaned and text.strip():
        cleaned.append(text.strip()[:1200])
    return cleaned


def _json_parts(text: str) -> list[str]:
    try:
        value = json.loads(text)
    except json.JSONDecodeError:
        return [text]
    if not isinstance(value, dict):
        return [text[:1200]]
    return [json.dumps({key: item}, ensure_ascii=False) for key, item in list(value.items())[:12]]
