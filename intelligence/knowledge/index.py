"""O índice fica fora do projeto e não chama modelo nem embeddings. É reconstruível: se o esquema mudar, ele é apagado e o
motor reenvia os textos.
"""

from __future__ import annotations

import re
import sqlite3
import sys
import threading
import traceback
from contextlib import contextmanager
from datetime import datetime, timezone
from pathlib import Path

from knowledge.chunking import MAX_CHUNKS, chunk, language_of

SCHEMA_VERSION = 3
_PROJECT_ID = re.compile(r"^p-[0-9a-f]{12}$")
_HASH = re.compile(r"^[0-9a-f]{64}$")
_TOKEN = re.compile(r"[^\W_]{2,}", re.UNICODE)
_CAMEL = re.compile(r"(?<=[a-z0-9])(?=[A-Z])|[_\-.]+")
_FRONTMATTER = re.compile(r"\A---\n(?P<body>.*?)\n---\n", re.DOTALL)
_TECHNOLOGIES = re.compile(r"^technologies:[ \t]*(?P<value>.*)$", re.MULTILINE)
_TECHNOLOGY_ID = re.compile(r"^[a-z0-9-]{1,40}$")
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
    _prepare_for_write(index_root, path)
    incoming = [_source(item) for item in sources]
    incoming = [item for item in incoming if item is not None]
    with _session(path) as conn:
        known = {row[0]: (row[1], row[2]) for row in conn.execute("SELECT path, content_hash, version FROM documents")}
        seen: set[str] = set()
        updated = unchanged = pending = 0
        problems: list[str] = []
        for item in incoming:
            seen.add(item["path"])
            previous = known.get(item["path"])
            if previous and previous[0] == item["hash"]:
                unchanged += 1
                continue
            if "text" not in item:
                pending += 1
                continue
            # Um documento que o banco recusa fica de fora sozinho: desfazer o lote inteiro deixaria o projeto sem índice.
            conn.execute("SAVEPOINT documento")
            try:
                written = _replace(conn, project_id, item, (previous[1] + 1) if previous else 1)
            except sqlite3.IntegrityError as ex:
                conn.execute("ROLLBACK TO documento")
                conn.execute("RELEASE documento")
                _log_skipped(ex)
                problems.append(item["path"])
                continue
            conn.execute("RELEASE documento")
            if written == 0:
                problems.append(item["path"])
            updated += 1
        removed = 0
        for old in list(known):
            if old in seen:
                continue
            _delete(conn, old)
            removed += 1
        documents = conn.execute("SELECT COUNT(*) FROM documents").fetchone()[0]
        chunks = conn.execute("SELECT COUNT(*) FROM chunks").fetchone()[0]
        orphans = conn.execute("SELECT COUNT(*) FROM documents d WHERE NOT EXISTS (SELECT 1 FROM chunks c WHERE c.path = d.path)").fetchone()[0]
        # O chunking para no teto de trechos por arquivo: quem chegou nele perdeu o fim, e isso precisa aparecer.
        truncated = conn.execute(
            "SELECT COUNT(*) FROM (SELECT path FROM chunks GROUP BY path HAVING COUNT(*) >= ?)", (MAX_CHUNKS,)
        ).fetchone()[0]
    return {
        "updated": updated, "unchanged": unchanged, "removed": removed, "pending": pending,
        "documents": documents, "chunks": chunks, "truncated": truncated, "valid": not problems and orphans == 0,
    }


def lexical(index_root: str, project_id: str, query: str, limit: int = 20) -> list[dict]:
    tokens = query_terms(query)
    if not tokens:
        return []
    path = _index_file(index_root, project_id)
    if not path.exists():
        return []
    match = " OR ".join(_match_term(token) for token in tokens)
    with _session(path) as conn:
        try:
            rows = conn.execute(
                """
                SELECT f.chunk_id, snippet(chunks_fts, 0, '', '', '…', 12), bm25(chunks_fts, 1.0, 3.0)
                FROM chunks_fts f
                WHERE chunks_fts MATCH ? AND f.project_id = ?
                ORDER BY bm25(chunks_fts, 1.0, 3.0)
                LIMIT ?
                """,
                (match, project_id, limit),
            ).fetchall()
        except sqlite3.OperationalError as ex:
            raise KnowledgeError("consulta inválida") from ex
        return [_hit(conn, chunk_id, round(-float(score), 4), excerpt) for chunk_id, excerpt, score in rows]


def symbols(index_root: str, project_id: str, names: list[str], limit: int = 20) -> list[dict]:
    """Trechos que declaram exatamente um dos nomes, ou o membro final de um nome qualificado (Charge em X.Charge)."""
    wanted = [name.lower() for name in names if name]
    if not wanted:
        return []
    path = _index_file(index_root, project_id)
    if not path.exists():
        return []
    marks = ",".join("?" for _ in wanted)
    with _session(path) as conn:
        rows = conn.execute(
            f"""
            SELECT chunk_id, MIN(CASE WHEN name IN ({marks}) THEN 0 ELSE 1 END) AS exact
            FROM symbols
            WHERE name IN ({marks}) OR tail IN ({marks})
            GROUP BY chunk_id
            ORDER BY exact, chunk_id
            LIMIT ?
            """,
            (*wanted, *wanted, *wanted, limit),
        ).fetchall()
        return [_hit(conn, chunk_id, 1.0 if exact == 0 else 0.5, None) for chunk_id, exact in rows]


def search(index_root: str, project_id: str, goal: str, limit: int = 5) -> list[dict]:
    """Só a busca lexical; a recuperação completa (com símbolos e fusão) fica em ``knowledge.retrieval``."""
    return lexical(index_root, project_id, goal, limit)


def chunk_body(index_root: str, project_id: str, chunk_id: str) -> str | None:
    path = _index_file(index_root, project_id)
    if not path.exists():
        return None
    with _session(path) as conn:
        row = conn.execute("SELECT body FROM chunks WHERE chunk_id = ?", (chunk_id,)).fetchone()
    return row[0] if row else None


def chunk_bodies(index_root: str, project_id: str, chunk_ids: list[str]) -> dict[str, str]:
    """Numa sessão só: a busca precisa de todos os candidatos, e uma conexão por trecho era o N+1."""
    ids = list(dict.fromkeys(chunk_ids))
    path = _index_file(index_root, project_id)
    if not ids or not path.exists():
        return {}
    bodies: dict[str, str] = {}
    with _session(path) as conn:
        for start in range(0, len(ids), 500):
            batch = ids[start : start + 500]
            marks = ",".join("?" * len(batch))
            for chunk_id, body in conn.execute(f"SELECT chunk_id, body FROM chunks WHERE chunk_id IN ({marks})", batch):
                bodies[chunk_id] = body
    return bodies


def query_terms(query: str) -> list[str]:
    tokens: list[str] = []
    for token in _TOKEN.findall(query or ""):
        # Palavras curtas só entram se forem sigla ou identificador (RRF, Pix, v2); "de", "o", "que" ficam de fora.
        if len(token) < 4 and not any(char.isupper() or char.isdigit() for char in token):
            continue
        lowered = token.lower()
        if lowered not in tokens:
            tokens.append(lowered)
        if len(tokens) == 12:
            break
    return tokens


def _match_term(token: str) -> str:
    """
    O índice não tem radical: "trechos" não achava "trecho". Plural vira prefixo do singular ("trecho"*), que acha as duas
    formas; o resto continua exato. Só ``s`` e ``es`` finais, que valem para o português e o inglês sem errar muito.
    """
    stem = _singular(token)
    return f'"{stem}"*' if stem != token else f'"{token}"'


def _singular(token: str) -> str:
    if len(token) < 5 or not token.endswith("s") or token.endswith("ss"):
        return token
    if token.endswith("es") and token[-3] in "rzl":
        return token[:-2]
    return token[:-1]


def identifier_parts(name: str) -> list[str]:
    return [part.lower() for part in _CAMEL.split(name) if len(part) >= 2]


def _hit(conn: sqlite3.Connection, chunk_id: str, score: float, excerpt: str | None) -> dict:
    row = conn.execute(
        """
        SELECT c.path, c.content_hash, c.kind, c.heading, c.start_line, c.body, d.language, d.module, d.version, d.technologies
        FROM chunks c JOIN documents d ON d.path = c.path
        WHERE c.chunk_id = ?
        """,
        (chunk_id,),
    ).fetchone()
    path, digest, kind, heading, start_line, body, language, module, version, technologies = row
    return {
        "documentId": path,
        "chunkId": chunk_id,
        "filePath": path,
        "hash": digest,
        "score": score,
        "excerpt": (excerpt if excerpt is not None else body)[:160],
        "kind": kind,
        "heading": heading,
        "startLine": start_line,
        "language": language,
        "module": module,
        "documentVersion": version,
        "technologies": [item for item in technologies.split(",") if item],
    }


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
    target = (root / "projects" / project_id / "knowledge").resolve()
    if target.parent.parent != root / "projects":
        raise KnowledgeError("índice inválido")
    return target / "index.sqlite"


def _prepare_for_write(index_root: str, path: Path) -> None:
    """Só quem grava cria a pasta do projeto e remove o índice antigo; consultar nunca mexe no disco."""
    try:
        path.parent.mkdir(parents=True, exist_ok=True)
        _drop_legacy(Path(index_root).resolve(), path.parent.parent.name)
    except OSError as ex:
        raise KnowledgeError("índice indisponível") from ex


def _drop_legacy(root: Path, project_id: str) -> None:
    """O índice da versão anterior ficava em <raiz>/<projectId>/index.sqlite; é reconstruível, então só é apagado."""
    legacy = root / project_id / "index.sqlite"
    if legacy.is_file():
        legacy.unlink(missing_ok=True)
        try:
            legacy.parent.rmdir()
        except OSError:
            pass


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
    """Falha de disco ou de SQLite (arquivo corrompido, banco travado) sai como `KnowledgeError`, que quem consulta sabe degradar."""
    try:
        with _guard(path):
            conn = sqlite3.connect(path)
            try:
                _ensure(conn)
                yield conn
                conn.commit()
            finally:
                conn.close()
    except (sqlite3.Error, OSError) as ex:
        raise KnowledgeError("índice indisponível") from ex


def _ensure(conn: sqlite3.Connection) -> None:
    version = conn.execute("PRAGMA user_version").fetchone()[0]
    if version != SCHEMA_VERSION:
        for table in ("documents", "chunks", "chunks_fts", "symbols"):
            conn.execute(f"DROP TABLE IF EXISTS {table}")
        conn.execute(f"PRAGMA user_version = {SCHEMA_VERSION}")
    conn.executescript(
        """
        CREATE TABLE IF NOT EXISTS documents (
            path TEXT PRIMARY KEY,
            content_hash TEXT NOT NULL,
            version INTEGER NOT NULL,
            language TEXT NOT NULL,
            module TEXT NOT NULL,
            indexed_at TEXT NOT NULL,
            technologies TEXT NOT NULL DEFAULT ''
        );
        CREATE TABLE IF NOT EXISTS chunks (
            chunk_id TEXT PRIMARY KEY,
            path TEXT NOT NULL,
            content_hash TEXT NOT NULL,
            kind TEXT NOT NULL,
            heading TEXT NOT NULL,
            start_line INTEGER NOT NULL,
            body TEXT NOT NULL
        );
        CREATE INDEX IF NOT EXISTS chunks_path ON chunks(path);
        CREATE VIRTUAL TABLE IF NOT EXISTS chunks_fts USING fts5(
            body,
            terms,
            chunk_id UNINDEXED,
            project_id UNINDEXED,
            tokenize = 'unicode61 remove_diacritics 2'
        );
        CREATE TABLE IF NOT EXISTS symbols (
            name TEXT NOT NULL,
            tail TEXT NOT NULL,
            chunk_id TEXT NOT NULL,
            path TEXT NOT NULL
        );
        CREATE INDEX IF NOT EXISTS symbols_name ON symbols(name);
        CREATE INDEX IF NOT EXISTS symbols_tail ON symbols(tail);
        CREATE INDEX IF NOT EXISTS symbols_path ON symbols(path);
        """
    )


def _replace(conn: sqlite3.Connection, project_id: str, item: dict, version: int) -> int:
    _delete(conn, item["path"])
    technologies, text = technologies_of(item["path"], item["text"])
    conn.execute(
        "INSERT INTO documents(path, content_hash, version, language, module, indexed_at, technologies) VALUES (?, ?, ?, ?, ?, ?, ?)",
        (item["path"], item["hash"], version, language_of(item["path"]), _module(item["path"]),
         datetime.now(timezone.utc).isoformat(timespec="seconds"), ",".join(technologies)),
    )
    written = 0
    for piece in chunk(item["path"], text):
        chunk_id = f"{item['path']}#{piece.anchor}"
        conn.execute(
            "INSERT INTO chunks(chunk_id, path, content_hash, kind, heading, start_line, body) VALUES (?, ?, ?, ?, ?, ?, ?)",
            (chunk_id, item["path"], item["hash"], piece.kind, piece.heading, piece.start_line, piece.body),
        )
        # 'terms' repete título e símbolos quebrados em palavras (ChargeRecurring -> charge recurring) com peso maior.
        terms = " ".join([piece.heading, *piece.symbols, *(part for name in piece.symbols for part in identifier_parts(name))])
        conn.execute(
            "INSERT INTO chunks_fts(body, terms, chunk_id, project_id) VALUES (?, ?, ?, ?)",
            (piece.body, terms, chunk_id, project_id),
        )
        for name in piece.symbols:
            lowered = name.lower()
            conn.execute(
                "INSERT INTO symbols(name, tail, chunk_id, path) VALUES (?, ?, ?, ?)",
                (lowered, lowered.rsplit(".", 1)[-1], chunk_id, item["path"]),
            )
        written += 1
    return written


def technologies_of(path: str, text: str) -> tuple[list[str], str]:
    """
    Em Markdown, a linha ``technologies:`` do frontmatter diz a que stacks o documento vale (a pasta Organização usa isso) e
    vira metadado, não texto do trecho. O resto do frontmatter fica como estava; se ele só tinha essa linha, sai inteiro, e as
    linhas viram linhas em branco para os números de linha dos trechos não mudarem.
    """
    if language_of(path) != "markdown":
        return [], text
    block = _FRONTMATTER.match(text)
    found = _TECHNOLOGIES.search(block.group("body")) if block else None
    if block is None or found is None:
        return [], text
    technologies = list(dict.fromkeys(
        value for value in (part.strip().lower() for part in found.group("value").split(",")) if _TECHNOLOGY_ID.match(value)
    ))
    rest = _TECHNOLOGIES.sub("", block.group("body"))
    blank = "\n" * block.group(0).count("\n")
    replaced = blank if not rest.strip() else block.group(0).replace(found.group(0), "")
    return technologies, replaced + text[block.end():]


def _log_skipped(error: BaseException) -> None:
    """Só o tipo e as linhas do código, como o servidor registra falhas: a mensagem do SQLite traz o id, que tem o caminho."""
    frames = "".join(traceback.format_tb(error.__traceback__))
    print(f"[ai] documento fora do índice: {type(error).__name__}\n{frames}", file=sys.stderr, flush=True)


def _delete(conn: sqlite3.Connection, path: str) -> None:
    ids = [row[0] for row in conn.execute("SELECT chunk_id FROM chunks WHERE path = ?", (path,))]
    for chunk_id in ids:
        conn.execute("DELETE FROM chunks_fts WHERE chunk_id = ?", (chunk_id,))
    conn.execute("DELETE FROM symbols WHERE path = ?", (path,))
    conn.execute("DELETE FROM chunks WHERE path = ?", (path,))
    conn.execute("DELETE FROM documents WHERE path = ?", (path,))


def _module(path: str) -> str:
    parts = path.split("/")
    if len(parts) == 1:
        return ""
    if parts[0] in {"src", "web", "app", "lib", "packages", "apps"} and len(parts) > 2:
        return f"{parts[0]}/{parts[1]}"
    return parts[0]
