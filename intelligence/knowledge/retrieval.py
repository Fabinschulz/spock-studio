from __future__ import annotations

import re

from knowledge import index
from knowledge.chunking import is_test_path

RRF_K = 60
_CANDIDATES = 50
# Num projeto grande, nomes de teste em português casam com as palavras da pergunta e empurravam a produção para fora do
# topo (validação real M1). Peso menor, não exclusão: sem nada melhor, o teste ainda aparece.
TEST_WEIGHT = 0.5
# Mais de dois trechos do mesmo arquivo gastam o contexto com repetição.
PER_FILE = 2
_ASKS_FOR_TESTS = ("teste", "test", "fixture", "spec")
_WORD = re.compile(r"[A-Za-z_$][\w$.\-]*[\w$]|[A-Za-z]")
_REQUIREMENT = re.compile(r"^[A-Z]{2,5}-\d{1,4}$")


def search(index_root: str, project_id: str, query: str, limit: int = 5) -> list[dict]:
    lexical = index.lexical(index_root, project_id, query, _CANDIDATES)
    symbols = index.symbols(index_root, project_id, identifiers(query), _CANDIDATES)
    fused: dict[str, dict] = {}
    for source, hits in (("lexical", lexical), ("symbol", symbols)):
        for rank, hit in enumerate(hits, start=1):
            entry = fused.setdefault(hit["chunkId"], {"hit": hit, "score": 0.0, "sources": set()})
            entry["score"] += 1 / (RRF_K + rank)
            entry["sources"].add(source)
            if source == "lexical":
                entry["hit"] = hit
    terms = index.query_terms(query)
    bodies = index.chunk_bodies(index_root, project_id, list(fused))
    about_tests = any(term.startswith(_ASKS_FOR_TESTS) for term in terms)
    for chunk_id, entry in fused.items():
        body = bodies.get(chunk_id, "")
        entry["score"] += _coverage(f"{entry['hit'].get('heading', '')} {body}", terms)
        if not about_tests and is_test_path(entry["hit"].get("filePath", "")):
            entry["score"] *= TEST_WEIGHT
    ranked = sorted(fused.values(), key=lambda entry: (-entry["score"], entry["hit"]["chunkId"]))
    results = []
    per_file: dict[str, int] = {}
    for entry in ranked:
        if len(results) == limit:
            break
        path = entry["hit"].get("filePath", "")
        if per_file.get(path, 0) == PER_FILE:
            continue
        per_file[path] = per_file.get(path, 0) + 1
        hit = dict(entry["hit"])
        hit["score"] = round(entry["score"], 6)
        hit["retriever"] = "fused" if len(entry["sources"]) > 1 else next(iter(entry["sources"]))
        results.append(hit)
    return results


def identifiers(query: str) -> list[str]:
    """Palavras do pedido com cara de identificador: camelCase, PascalCase no meio da frase, snake_case, nomes com ponto ou hífen, ids."""
    found: list[str] = []
    for position, raw in enumerate(_WORD.findall(query or "")):
        word = raw.strip(".-")
        if len(word) < 2:
            continue
        looks_like = (
            any(char.isupper() for char in word[1:])
            or "_" in word
            or ("." in word and not word.endswith("."))
            or ("-" in word)
            or _REQUIREMENT.match(word) is not None
            or (position > 0 and word[0].isupper() and len(word) >= 3)
        )
        if looks_like and word not in found:
            found.append(word)
    return found


def _coverage(text: str, terms: list[str]) -> float:
    """Desempate pequeno (menor que um degrau do RRF): quanto dos termos do pedido aparece no trecho e no título."""
    if not terms:
        return 0.0
    lowered = text.lower()
    return 0.0002 * sum(1 for term in terms if term in lowered) / len(terms)
