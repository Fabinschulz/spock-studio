"""Recuperação híbrida: o ponto único que o grafo e a avaliação chamam.

Duas listas independentes — lexical (BM25 sobre texto, títulos e símbolos quebrados em palavras) e símbolos (nomes
declarados no código, chaves de configuração, ids de requisito) — fundidas por Reciprocal Rank Fusion. Um reordenamento
determinístico desempata pela cobertura dos termos do pedido. Vetores entram como terceira lista quando houver
embeddings (D3), sem mudar este contrato.
"""

from __future__ import annotations

import re

from knowledge import index

RRF_K = 60
_CANDIDATES = 20
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
    for entry in fused.values():
        body = index.chunk_body(index_root, project_id, entry["hit"]["chunkId"]) or ""
        entry["score"] += _coverage(f"{entry['hit'].get('heading', '')} {body}", terms)
    ranked = sorted(fused.values(), key=lambda entry: (-entry["score"], entry["hit"]["chunkId"]))
    results = []
    for entry in ranked[:limit]:
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
