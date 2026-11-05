"""O cliente do proxy de modelo, contra um servidor de teste em 127.0.0.1: formato do pedido, erros e o que nunca vaza."""

import json
import os
import threading
import time
import unittest
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

import _paths  # noqa: F401

from agents.model_client import ModelClient, ModelError

TOKEN = "t" * 43
EXECUTION = "ex-aaaaaaaaaaaa"
OK = {"schema": "ai.model.response.v1", "status": "ok", "text": "{}", "model": "m", "modelVersion": "m-1",
      "inputTokens": 10, "outputTokens": 5, "costUsd": 0}


class _Proxy:
    """Um proxy de teste: guarda o que recebeu e responde o que o teste mandou."""

    def __init__(self, status: int = 200, body: object = OK, delay: float = 0.0) -> None:
        self.status, self.body, self.delay = status, body, delay
        self.seen: list[dict] = []
        outer = self

        class Handler(BaseHTTPRequestHandler):
            def log_message(self, *_args) -> None:
                return

            def do_POST(self) -> None:  # noqa: N802
                length = int(self.headers.get("Content-Length", "0"))
                outer.seen.append({"path": self.path, "auth": self.headers.get("Authorization"),
                                   "body": json.loads(self.rfile.read(length).decode("utf-8"))})
                time.sleep(outer.delay)
                raw = outer.body if isinstance(outer.body, bytes) else json.dumps(outer.body).encode("utf-8")
                try:
                    self.send_response(outer.status)
                    self.send_header("Content-Length", str(len(raw)))
                    self.end_headers()
                    self.wfile.write(raw)
                except OSError:
                    pass

        self.server = ThreadingHTTPServer(("127.0.0.1", 0), Handler)
        self.url = f"http://127.0.0.1:{self.server.server_address[1]}/v1/model"
        threading.Thread(target=self.server.serve_forever, daemon=True).start()

    def close(self) -> None:
        self.server.shutdown()
        self.server.server_close()


MESSAGES = [{"role": "system", "content": "s"}, {"role": "user", "content": "u"}]


class ModelClientTests(unittest.TestCase):
    def _ask(self, proxy: _Proxy, timeout: float = 5.0):
        try:
            return ModelClient(proxy.url, TOKEN, EXECUTION, timeout).complete("planning", "plan", MESSAGES, 512, "engineering.plan.v1")
        finally:
            proxy.close()

    def test_pedido_segue_o_contrato_e_o_token_vai_so_no_cabecalho(self) -> None:
        proxy = _Proxy()
        reply = self._ask(proxy)

        sent = proxy.seen[0]
        self.assertEqual(sent["path"], "/v1/model")
        self.assertEqual(sent["auth"], "Bearer " + TOKEN)
        self.assertEqual(sent["body"], {
            "schema": "ai.model.request.v1", "executionId": EXECUTION, "agentId": "planning", "purpose": "plan",
            "messages": MESSAGES, "maxTokens": 512, "responseSchema": "engineering.plan.v1",
        })
        self.assertNotIn(TOKEN, json.dumps(sent["body"]))
        self.assertEqual((reply.text, reply.model, reply.model_version, reply.input_tokens, reply.output_tokens), ("{}", "m", "m-1", 10, 5))

    def test_erro_do_proxy_vira_o_codigo_do_contrato_e_codigo_desconhecido_vira_indisponivel(self) -> None:
        for code in ("MODEL_BUDGET_EXCEEDED", "MODEL_TIMEOUT", "MODEL_NOT_CONFIGURED", "MODEL_FORBIDDEN", "MODEL_UNAVAILABLE"):
            body = {"schema": "ai.model.response.v1", "status": "error", "error": {"code": code}}
            with self.assertRaises(ModelError) as raised:
                self._ask(_Proxy(body=body))
            self.assertEqual(raised.exception.code, code)

        body = {"schema": "ai.model.response.v1", "status": "error", "error": {"code": "ALGO_NOVO"}}
        with self.assertRaises(ModelError) as raised:
            self._ask(_Proxy(body=body))
        self.assertEqual(raised.exception.code, "MODEL_UNAVAILABLE")

    def test_http_de_recusa_e_resposta_fora_do_contrato(self) -> None:
        cases = [
            (401, {"error": "unauthorized"}, "MODEL_FORBIDDEN"),
            (403, {}, "MODEL_FORBIDDEN"),
            (500, {}, "MODEL_UNAVAILABLE"),
            (200, b"isto nao e json", "MODEL_UNAVAILABLE"),
            (200, {"schema": "outro"}, "MODEL_UNAVAILABLE"),
            (200, {**OK, "inputTokens": -1}, "MODEL_UNAVAILABLE"),
            (200, {**OK, "outputTokens": True}, "MODEL_UNAVAILABLE"),
            (200, {**OK, "text": 5}, "MODEL_UNAVAILABLE"),
            (200, {**OK, "status": "talvez"}, "MODEL_UNAVAILABLE"),
        ]
        for status, body, code in cases:
            with self.assertRaises(ModelError, msg=str(body)) as raised:
                self._ask(_Proxy(status, body))
            self.assertEqual(raised.exception.code, code, str(body))

    def test_demora_vira_timeout_e_servidor_fora_do_ar_vira_indisponivel(self) -> None:
        with self.assertRaises(ModelError) as raised:
            self._ask(_Proxy(delay=1.0), timeout=0.2)
        self.assertEqual(raised.exception.code, "MODEL_TIMEOUT")

        proxy = _Proxy()
        proxy.close()
        with self.assertRaises(ModelError) as refused:
            ModelClient(proxy.url, TOKEN, EXECUTION, 1.0).complete("planning", "plan", MESSAGES, 512)
        self.assertEqual(refused.exception.code, "MODEL_UNAVAILABLE")

    def test_so_aceita_o_proxy_do_motor_no_loopback(self) -> None:
        for url in ("http://localhost:49152/v1/model", "http://10.0.0.5:49152/v1/model", "https://127.0.0.1:49152/v1/model",
                    "http://127.0.0.1:49152/v1/outro", "http://127.0.0.1/v1/model", "http://127.0.0.1:49152/v1/model?x=1"):
            with self.assertRaises(ValueError, msg=url):
                ModelClient(url, TOKEN, EXECUTION)
        with self.assertRaises(ValueError):
            ModelClient("http://127.0.0.1:49152/v1/model", "curto", EXECUTION)

    def test_pedido_fora_do_contrato_nem_sai(self) -> None:
        client = ModelClient("http://127.0.0.1:49152/v1/model", TOKEN, EXECUTION)
        for args in (("Planning", "plan", MESSAGES, 10), ("planning", "invadir", MESSAGES, 10),
                     ("planning", "plan", MESSAGES, 0), ("planning", "plan", MESSAGES, 9000)):
            with self.assertRaises(ModelError):
                client.complete(*args)

    def test_proxy_de_saida_do_ambiente_nao_intercepta_o_loopback(self) -> None:
        saved = {k: os.environ.get(k) for k in ("HTTP_PROXY", "http_proxy")}
        os.environ["HTTP_PROXY"] = os.environ["http_proxy"] = "http://127.0.0.1:9"
        try:
            reply = self._ask(_Proxy())
        finally:
            for key, value in saved.items():
                if value is None:
                    os.environ.pop(key, None)
                else:
                    os.environ[key] = value
        self.assertEqual(reply.text, "{}")


if __name__ == "__main__":
    unittest.main()
