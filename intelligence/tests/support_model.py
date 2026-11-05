"""Modelo falso e montagem de execuções com modelo, para os testes dos agentes (Fase 6). Nada aqui fala com a rede."""

from __future__ import annotations

import json

import _paths  # noqa: F401

from agents.model_client import ModelError, ModelReply
from test_graph import EXECUTION, _start

TOKEN = "t" * 43
URL = "http://127.0.0.1:49152/v1/model"
SOURCE = {"path": "docs/cobranca.md", "hash": "a" * 64, "text": "O módulo de cobrança recorrente fatura assinaturas."}


class FakeModel:
    """Responde, na ordem, o que o teste roteirizou: texto (vira ``ModelReply``) ou uma exceção (``ModelError``)."""

    def __init__(self, *replies: object, input_tokens: int = 100, output_tokens: int = 50) -> None:
        self.replies = list(replies)
        self.input_tokens = input_tokens
        self.output_tokens = output_tokens
        self.calls: list[dict] = []

    def complete(self, agent_id, purpose, messages, max_tokens, response_schema=None) -> ModelReply:
        self.calls.append({"agent": agent_id, "purpose": purpose, "messages": messages, "maxTokens": max_tokens, "schema": response_schema})
        if not self.replies:
            raise ModelError("MODEL_UNAVAILABLE")
        reply = self.replies.pop(0)
        if isinstance(reply, ModelError):
            raise reply
        text = reply if isinstance(reply, str) else json.dumps(reply, ensure_ascii=False)
        return ModelReply(text, "gpt-4o-mini", "gpt-4o-mini-2024-07-18", self.input_tokens, self.output_tokens)


def with_model(root: str = "", *, max_tokens: int = 8000, sources: bool = True, **input_fields) -> dict:
    """Um ``ai.workflow.start.v2`` com modelo permitido, o proxy do motor e, por padrão, um trecho para recuperar."""
    payload = _start(root, **input_fields)
    payload["limits"] = {"maxIterations": 8, "maxMilliseconds": 60000, "maxTokens": max_tokens, "maxCostUsd": 0}
    payload["policy"] = {"allowedActions": [], "modelAllowed": True}
    payload["broker"] = {"url": URL, "token": TOKEN}
    if root and sources:
        payload["context"]["knowledgeRoot"] = root
        payload["context"]["sources"] = [dict(SOURCE)]
    return payload


def plan(*steps: tuple[str, str] | tuple[str, str, list[str]]) -> dict:
    items = []
    for step in steps:
        item = {"id": step[0], "title": step[1]}
        if len(step) == 3:
            item["evidence"] = step[2]
        items.append(item)
    return {"schema": "engineering.plan.v1", "steps": items}


def impact(areas: list[str], risks: list[str], evidence: list[str]) -> dict:
    return {"schema": "engineering.impact.v1", "areas": areas, "risks": risks, "evidence": evidence}


__all__ = ["EXECUTION", "FakeModel", "SOURCE", "TOKEN", "URL", "impact", "plan", "with_model"]
