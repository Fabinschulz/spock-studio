"""Busca no índice para quem está fora do Spock (a IDE pelo MCP). Só lê: não sincroniza nem cria o índice, então devolve o que
o aplicativo indexou da última vez. O motor confere cada caminho contra as regras de conhecimento antes de repassar.
"""

from __future__ import annotations

from contracts.messages import SearchRequest
from knowledge import index, retrieval

SEARCH_ACK = "ai.knowledge.search.ack.v1"


def lookup(request: SearchRequest) -> dict:
    hits = retrieval.search(request.knowledge_root, request.project_id, request.query, request.limit, request.technologies)
    bodies = index.chunk_bodies(request.knowledge_root, request.project_id, [hit["chunkId"] for hit in hits])
    return {
        "schema": SEARCH_ACK,
        "indexed": bool(index.manifest(request.knowledge_root, request.project_id)),
        "hits": [_hit(hit, bodies.get(hit["chunkId"], "")) for hit in hits],
    }


def _hit(hit: dict, body: str) -> dict:
    return {
        "filePath": hit["filePath"],
        "hash": hit["hash"],
        "heading": hit["heading"],
        "startLine": hit["startLine"],
        "kind": hit["kind"],
        "body": body,
    }
