"""Cliente do proxy de modelo do motor (``ai.model.request.v1`` e ``ai.model.response.v1``).

O runtime não tem chave, provedor nem modelo: só o endereço do proxy (sempre 127.0.0.1) e um token que vale para uma
execução. O motor decide o modelo, cobra o orçamento de tokens, redige segredos e conta o uso. O token nunca entra em
estado, checkpoint, evento, log ou mensagem de erro.
"""

from __future__ import annotations

import json
import re
import socket
import urllib.error
import urllib.request
from dataclasses import dataclass

URL = re.compile(r"^http://127\.0\.0\.1:[0-9]{1,5}/v1/model$")
_CODES = {"MODEL_NOT_CONFIGURED", "MODEL_BUDGET_EXCEEDED", "MODEL_TIMEOUT", "MODEL_UNAVAILABLE", "MODEL_FORBIDDEN"}
_MAX_RESPONSE = 256_000
_AGENT = re.compile(r"^[a-z][a-z0-9-]{0,31}$")
_PURPOSES = {"classify", "plan", "architecture", "judge", "rewrite-query"}


class ModelError(Exception):
    """A chamada não deu resposta utilizável. ``code`` é um dos códigos do contrato; nunca carrega texto do modelo."""

    def __init__(self, code: str) -> None:
        super().__init__(code)
        self.code = code


@dataclass(frozen=True)
class ModelReply:
    text: str
    model: str
    model_version: str
    input_tokens: int
    output_tokens: int


class ModelClient:
    def __init__(self, url: str, token: str, execution_id: str, timeout: float = 35.0) -> None:
        if not URL.match(url) or not 32 <= len(token) <= 128:
            raise ValueError("broker")
        self._url = url
        self._token = token
        self._execution_id = execution_id
        self._timeout = timeout
        # O ambiente pode ter proxy de saída; o proxy do motor está no loopback e nunca deve passar por ele.
        self._opener = urllib.request.build_opener(urllib.request.ProxyHandler({}))

    def complete(
        self,
        agent_id: str,
        purpose: str,
        messages: list[dict],
        max_tokens: int,
        response_schema: str | None = None,
    ) -> ModelReply:
        if not _AGENT.match(agent_id) or purpose not in _PURPOSES or not 1 <= max_tokens <= 8192:
            raise ModelError("MODEL_FORBIDDEN")
        body: dict = {
            "schema": "ai.model.request.v1",
            "executionId": self._execution_id,
            "agentId": agent_id,
            "purpose": purpose,
            "messages": messages,
            "maxTokens": max_tokens,
        }
        if response_schema:
            body["responseSchema"] = response_schema
        request = urllib.request.Request(
            self._url,
            data=json.dumps(body, ensure_ascii=False).encode("utf-8"),
            method="POST",
            headers={"Content-Type": "application/json; charset=utf-8", "Authorization": "Bearer " + self._token},
        )
        try:
            with self._opener.open(request, timeout=self._timeout) as response:
                raw = response.read(_MAX_RESPONSE + 1)
        except urllib.error.HTTPError as ex:
            raise ModelError("MODEL_FORBIDDEN" if ex.code in (401, 403) else "MODEL_UNAVAILABLE") from None
        except (socket.timeout, TimeoutError):
            raise ModelError("MODEL_TIMEOUT") from None
        except (urllib.error.URLError, OSError):
            raise ModelError("MODEL_UNAVAILABLE") from None
        return _reply(raw)


def _reply(raw: bytes) -> ModelReply:
    if len(raw) > _MAX_RESPONSE:
        raise ModelError("MODEL_UNAVAILABLE")
    try:
        document = json.loads(raw.decode("utf-8"))
    except (UnicodeDecodeError, json.JSONDecodeError):
        raise ModelError("MODEL_UNAVAILABLE") from None
    if not isinstance(document, dict) or document.get("schema") != "ai.model.response.v1":
        raise ModelError("MODEL_UNAVAILABLE")
    if document.get("status") == "error":
        error = document.get("error")
        code = error.get("code") if isinstance(error, dict) else None
        raise ModelError(code if code in _CODES else "MODEL_UNAVAILABLE")
    text, model, version = document.get("text"), document.get("model"), document.get("modelVersion")
    tokens = (document.get("inputTokens"), document.get("outputTokens"))
    if document.get("status") != "ok" or not all(isinstance(item, str) for item in (text, model, version)):
        raise ModelError("MODEL_UNAVAILABLE")
    if any(not isinstance(n, int) or isinstance(n, bool) or n < 0 for n in tokens):
        raise ModelError("MODEL_UNAVAILABLE")
    return ModelReply(text, model, version, tokens[0], tokens[1])
