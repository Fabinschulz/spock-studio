"""O que os agentes com modelo mandariam ao provedor para um pedido, sem chamar modelo nem abrir execução. Usa a mesma busca e
o mesmo texto do fluxo: a prévia é o pedido de verdade, não uma aproximação dele.
"""

from __future__ import annotations

from agents import architecture, planning, prompts
from contracts.messages import PreviewRequest
from knowledge import retrieval
from knowledge.index import chunk_bodies, sync

PREVIEW_ACK = "ai.context.preview.ack.v1"


def preview(request: PreviewRequest) -> dict:
    stats = sync(request.knowledge_root, request.project_id, request.sources)
    found = retrieval.search(request.knowledge_root, request.project_id, request.goal, technologies=request.stacks)
    hits = prompts.with_bodies(found, chunk_bodies(request.knowledge_root, request.project_id, [hit["chunkId"] for hit in found]))
    paths = {hit["chunkId"]: hit["filePath"] for hit in hits}
    return {
        "schema": PREVIEW_ACK,
        "calls": [
            _call(planning.AGENT_ID, planning.MAX_OUTPUT_TOKENS, planning.messages(request.goal, request.stacks, hits)),
            _call(architecture.AGENT_ID, architecture.MAX_OUTPUT_TOKENS, architecture.messages(request.goal, request.stacks, hits)),
        ],
        "evidence": [{**entry, "filePath": paths.get(entry["chunkId"], "")} for entry in prompts.entries(hits)],
        "index": {"documents": stats["documents"], "pending": stats["pending"], "valid": stats["valid"]},
    }


def _call(agent_id: str, max_output_tokens: int, messages: list[dict]) -> dict:
    return {"agentId": agent_id, "maxOutputTokens": max_output_tokens, "messages": messages}
