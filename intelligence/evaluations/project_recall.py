"""Recuperação medida num projeto de verdade: o próprio repositório do Spock, com perguntas que alguém faria sobre ele.

O conjunto sintético (``recall.py``) prova que a busca acha o que deve num corpus pequeno; este mede o que a validação real
mostrou que falta: num projeto grande, nomes de testes em português casam com as palavras da pergunta e empurram o
arquivo de produção para fora do topo. As fontes seguem as regras do motor (``KnowledgeSources``): arquivos do git,
mesmas extensões e pastas bloqueadas, texto cortado em 24.000 caracteres e no máximo 2.000 arquivos, os ``.md`` antes.

Uso: ``python intelligence/evaluations/project_recall.py [raiz do repositório]``.
"""

from __future__ import annotations

import hashlib
import json
import subprocess
import sys
import tempfile
from pathlib import Path, PurePosixPath
from typing import Any

if __package__ in (None, ""):
    sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from knowledge import index as rag  # noqa: E402
from knowledge import retrieval  # noqa: E402
from knowledge.chunking import is_test_path  # noqa: E402

_DATASET = Path(__file__).resolve().parent / "datasets" / "spock-questions.json"
_PROJECT = "p-5b0c4a7e1d2f"
_LIMIT = 5
_MAX_FILES = 2_000
_MAX_CHARS = 24_000
_BLOCKED_EXTENSIONS = {
    ".png", ".jpg", ".jpeg", ".gif", ".webp", ".ico", ".svg", ".exe", ".dll", ".pdb", ".so", ".dylib", ".zip", ".gz", ".tar",
    ".woff", ".woff2", ".ttf", ".pdf", ".bin", ".pem", ".key", ".p12", ".pfx", ".jks", ".keystore", ".map", ".lock", ".sqlite",
    ".db", ".icns",
}
_BLOCKED_NAMES = {".gitignore", "package-lock.json", "pnpm-lock.yaml", "yarn.lock", "packages.lock.json", "poetry.lock",
                  "Cargo.lock", "composer.lock"}
_BLOCKED_DIRS = {"node_modules", "bin", "obj", "dist", "build", "target", ".git", ".venv", "venv", "__pycache__", ".next"}


def project_sources(root: Path) -> list[dict]:
    listed = subprocess.run(["git", "-C", str(root), "ls-files"], capture_output=True, text=True, check=True).stdout.split("\n")
    paths = [path for path in listed if path and _allowed(path)]
    paths.sort(key=lambda path: (_rank(path), path))
    sources = []
    for path in paths:
        try:
            text = (root / path).read_text(encoding="utf-8")
        except (UnicodeDecodeError, OSError):
            continue
        if not text.strip():
            continue
        text = text[:_MAX_CHARS]
        sources.append({"path": path, "hash": hashlib.sha256(text.encode("utf-8")).hexdigest(), "text": text})
        if len(sources) == _MAX_FILES:
            break
    return sources


def run(root: Path, dataset: Path = _DATASET) -> dict[str, Any]:
    document = json.loads(dataset.read_text(encoding="utf-8"))
    sources = project_sources(root)
    with tempfile.TemporaryDirectory() as index_root:
        stats = rag.sync(index_root, _PROJECT, sources)
        cases = []
        for case in document["cases"]:
            hits = retrieval.search(index_root, _PROJECT, case["query"], _LIMIT)
            paths = [hit["filePath"] for hit in hits]
            rank = next((position for position, path in enumerate(paths, start=1) if path in case["expect"]), 0)
            cases.append({
                "id": case["id"],
                "rank": rank,
                "tests": sum(1 for path in paths if is_test_path(path)),
                "top": paths,
            })
    count = max(len(cases), 1)
    return {
        "files": len(sources),
        "chunks": stats["chunks"],
        "valid": stats["valid"],
        "recallAt5": round(sum(1 for case in cases if case["rank"]) / count, 4),
        "mrr": round(sum(1 / case["rank"] for case in cases if case["rank"]) / count, 4),
        "testShareAt5": round(sum(case["tests"] for case in cases) / (count * _LIMIT), 4),
        "cases": cases,
    }


def _allowed(path: str) -> bool:
    parts = PurePosixPath(path).parts
    name = parts[-1]
    if name in _BLOCKED_NAMES or PurePosixPath(name).suffix.lower() in _BLOCKED_EXTENSIONS:
        return False
    if name.startswith(".env") or name.endswith((".min.js", ".min.css")):
        return False
    return not any(part in _BLOCKED_DIRS for part in parts[:-1])


def _rank(path: str) -> int:
    suffix = PurePosixPath(path).suffix.lower()
    if suffix == ".md":
        return 0
    if suffix == ".cs":
        return 1
    if suffix in {".ts", ".tsx", ".js", ".jsx", ".py"}:
        return 2
    if suffix in {".json", ".yml", ".yaml"}:
        return 3
    return 4


if __name__ == "__main__":
    repo = Path(sys.argv[1]) if len(sys.argv) > 1 else Path(__file__).resolve().parents[2]
    report = run(repo)
    for case in report["cases"]:
        mark = f"#{case['rank']}" if case["rank"] else "fora"
        print(f"{mark:>5}  testes no topo: {case['tests']}  {case['id']}")
    summary = {key: report[key] for key in ("files", "chunks", "valid", "recallAt5", "mrr", "testShareAt5")}
    print(json.dumps(summary, ensure_ascii=False))
