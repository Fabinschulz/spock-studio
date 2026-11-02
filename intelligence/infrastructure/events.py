"""Eventos ai.event.v1 de uma execução.

Os nós chamam ``emit`` e o evento sai no fluxo ``custom`` do LangGraph no mesmo instante. A numeração continua depois de
uma pausa porque o primeiro número da retomada vem do checkpoint.
"""

from __future__ import annotations

from datetime import datetime, timezone
from typing import Callable

from langgraph.config import get_stream_writer


def now() -> str:
    return datetime.now(timezone.utc).isoformat(timespec="milliseconds")


class Emitter:
    def __init__(self, execution_id: str, first_seq: int = 0, writer: Callable[[dict], None] | None = None) -> None:
        self.execution_id = execution_id
        self.next_seq = first_seq
        self._writer = writer

    def event(self, type_: str, data: dict | None = None, agent_id: str | None = None) -> dict:
        seq = self.next_seq
        self.next_seq += 1
        event = {
            "schema": "ai.event.v1",
            "eventId": f"{self.execution_id}:{seq}",
            "executionId": self.execution_id,
            "localSeq": seq,
            "type": type_,
            "at": now(),
            "data": data or {},
        }
        if agent_id:
            event["agentId"] = agent_id
        return event

    def emit(self, type_: str, data: dict | None = None, agent_id: str | None = None) -> None:
        """Dentro de um nó: o evento vai para o fluxo do grafo. Fora dele, para o escritor dado na construção."""
        event = self.event(type_, data, agent_id)
        writer = self._writer or get_stream_writer()
        writer(event)
