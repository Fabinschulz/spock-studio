#!/usr/bin/env python3
"""Servidor local da inteligência do Mariliz.

Encaminha saúde, o workflow engineering.request.v1 e o índice de conhecimento.
Não lê o projeto, não grava arquivos e não chama modelo. O runtime .NET é quem
autoriza, aplica e publica eventos.
"""

from __future__ import annotations

import argparse
import hmac
import json
import os
import sys
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from knowledge.index import KnowledgeError, manifest
from workflows.engineering.graph import run_cancel, run_resume, run_start

MAX_BODY = 1_000_000


def main() -> None:
    parser = argparse.ArgumentParser(description="Servidor local da inteligência do Mariliz")
    parser.add_argument("--host", default="127.0.0.1")
    parser.add_argument("--port", type=int, required=True)
    args = parser.parse_args()
    token = os.environ.get("MARILIZ_AI_TOKEN", "")
    if args.host != "127.0.0.1":
        raise SystemExit("O runtime só escuta em 127.0.0.1.")
    if not token:
        raise SystemExit("Token de sessão ausente.")

    handler = make_handler(token)
    server = ThreadingHTTPServer((args.host, args.port), handler)
    server.serve_forever()


def make_handler(token: str):
    expected = "Bearer " + token

    class Handler(BaseHTTPRequestHandler):
        protocol_version = "HTTP/1.1"

        def log_message(self, _format: str, *_args) -> None:
            return

        def do_GET(self) -> None:  # noqa: N802
            if not self._authorized(expected):
                return
            if self.path.split("?", 1)[0] != "/health":
                self._send(404, {"error": "not_found"})
                return
            self._send(200, {"schema": "ai.health.v1", "status": "ok"})

        def do_POST(self) -> None:  # noqa: N802
            if not self._authorized(expected):
                return
            payload = self._read_json()
            if payload is None:
                return
            path = self.path.split("?", 1)[0]
            try:
                if path == "/v1/knowledge/manifest":
                    try:
                        files = manifest(_text(payload.get("indexRoot")), _text(payload.get("projectId")))
                    except KnowledgeError:
                        files = []
                    self._send(200, {"files": files})
                    return
                if path == "/v1/workflows":
                    self._send(200, run_start(payload))
                    return
                if path.startswith("/v1/workflows/") and path.endswith("/resume"):
                    self._send(200, run_resume(payload))
                    return
                if path.startswith("/v1/workflows/") and path.endswith("/cancel"):
                    run_cancel(_text(payload.get("runId")))
                    self._send(200, {"schema": "workflow.ack.v1", "status": "cancelled"})
                    return
            except Exception:
                self._send(500, {"error": "workflow_failed"})
                return
            self._send(404, {"error": "not_found"})

        def _authorized(self, expected_header: str) -> bool:
            header = self.headers.get("Authorization", "")
            if len(header) != len(expected_header) or not hmac.compare_digest(header, expected_header):
                self._send(401, {"error": "unauthorized"})
                return False
            return True

        def _read_json(self) -> dict | None:
            raw_length = self.headers.get("Content-Length", "0")
            try:
                length = int(raw_length)
            except ValueError:
                self._send(400, {"error": "invalid_body"})
                return None
            if length < 0 or length > MAX_BODY:
                self._send(413, {"error": "payload_too_large"})
                return None
            raw = self.rfile.read(length) if length else b"{}"
            try:
                payload = json.loads(raw.decode("utf-8"))
            except (UnicodeDecodeError, json.JSONDecodeError):
                self._send(400, {"error": "invalid_body"})
                return None
            if not isinstance(payload, dict):
                self._send(400, {"error": "invalid_body"})
                return None
            return payload

        def _send(self, status: int, payload: dict) -> None:
            body = json.dumps(payload, separators=(",", ":")).encode("utf-8")
            self.send_response(status)
            self.send_header("Content-Type", "application/json; charset=utf-8")
            self.send_header("Content-Length", str(len(body)))
            self.send_header("Connection", "close")
            self.end_headers()
            self.wfile.write(body)

    return Handler


def _text(value: object) -> str:
    return value if isinstance(value, str) else ""


if __name__ == "__main__":
    main()
