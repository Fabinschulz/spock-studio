from __future__ import annotations

import contextlib
import io
import json
import sys
import threading
import unittest
import urllib.error
import urllib.request
from http.server import ThreadingHTTPServer
from pathlib import Path
from unittest import mock

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

import server  # noqa: E402

TOKEN = "t" * 32


class ServerTests(unittest.TestCase):
    def setUp(self) -> None:
        self.httpd = ThreadingHTTPServer(("127.0.0.1", 0), server.make_handler(TOKEN))
        self.thread = threading.Thread(target=self.httpd.serve_forever, daemon=True)
        self.thread.start()
        self.base = f"http://127.0.0.1:{self.httpd.server_address[1]}"

    def tearDown(self) -> None:
        self.httpd.shutdown()
        self.httpd.server_close()

    def call(self, path: str, body: dict | None = None, token: str | None = TOKEN) -> tuple[int, dict]:
        data = None if body is None else json.dumps(body).encode("utf-8")
        request = urllib.request.Request(self.base + path, data=data, method="GET" if body is None else "POST")
        if token is not None:
            request.add_header("Authorization", "Bearer " + token)
        try:
            with urllib.request.urlopen(request, timeout=10) as response:
                return response.status, json.loads(response.read() or b"{}")
        except urllib.error.HTTPError as error:
            return error.code, json.loads(error.read() or b"{}")

    def test_saude_exige_o_token(self) -> None:
        self.assertEqual(self.call("/health")[0], 200)
        self.assertEqual(self.call("/health", token=None)[0], 401)

    def test_recusa_por_token_chega_inteira_mesmo_com_corpo_grande(self) -> None:
        # Sem ler o corpo antes de fechar, o Windows manda RST e o cliente via ConnectionAbortedError em vez do 401.
        body = {"query": "x" * 600_000}
        for _ in range(60):
            self.assertEqual(self.call("/v1/knowledge/search", body, token="x" * 43), (401, {"error": "unauthorized"}))
        self.assertEqual(self.call("/health", token="x" * 32)[0], 401)

    def test_so_a_rota_exata_de_retomar_e_cancelar_existe(self) -> None:
        with mock.patch.object(server, "run_resume") as resume, mock.patch.object(server, "run_cancel") as cancel:
            self.assertEqual(self.call("/v1/workflows/a/b/resume", {})[0], 404)
            self.assertEqual(self.call("/v1/workflows//resume", {})[0], 404)
            self.assertEqual(self.call("/v1/workflows/run-1/cancel", {"runId": "run-1"})[0], 200)
        resume.assert_not_called()
        cancel.assert_called_once()

    def test_a_investigacao_responde_em_ndjson_e_termina_no_ack(self) -> None:
        from test_investigation import _payload

        request = urllib.request.Request(
            self.base + "/v1/investigations", data=json.dumps(_payload(model=False)).encode("utf-8"), method="POST"
        )
        request.add_header("Authorization", "Bearer " + TOKEN)
        with urllib.request.urlopen(request, timeout=10) as response:
            self.assertEqual(response.headers["Content-Type"], "application/x-ndjson; charset=utf-8")
            lines = [json.loads(line) for line in response.read().decode("utf-8").splitlines() if line]

        self.assertEqual(lines[0]["type"], "workflow.started")
        self.assertEqual(lines[-1]["schema"], "ai.investigation.ack.v1")
        self.assertEqual(lines[-1]["status"], "completed_with_limitations")
        self.assertEqual(self.call("/v1/investigations", {"schema": "x"})[0], 200)

    def test_previa_de_contexto_responde_json_e_recusa_pedido_invalido(self) -> None:
        import tempfile

        with tempfile.TemporaryDirectory() as root:
            body = {
                "schema": "ai.context.preview.v1",
                "projectId": "p-aaaaaaaaaaaa",
                "goal": "frete",
                "context": {"stacks": [], "knowledgeRoot": root, "sources": []},
            }
            status, result = self.call("/v1/context/preview", body)
            self.assertEqual(status, 200)
            self.assertEqual(result["schema"], "ai.context.preview.ack.v1")
            self.assertEqual(self.call("/v1/context/preview", {**body, "goal": ""}), (400, {"error": "invalid_request"}))

    def test_busca_no_indice_responde_json_e_recusa_pedido_invalido(self) -> None:
        import tempfile

        with tempfile.TemporaryDirectory() as root:
            body = {"schema": "ai.knowledge.search.v1", "projectId": "p-aaaaaaaaaaaa", "query": "frete", "knowledgeRoot": root, "limit": 5}
            self.assertEqual(self.call("/v1/knowledge/search", body), (200, {"schema": "ai.knowledge.search.ack.v1", "indexed": False, "hits": []}))
            self.assertEqual(self.call("/v1/knowledge/search", {**body, "limit": 50}), (400, {"error": "invalid_request"}))
            self.assertEqual(self.call("/v1/knowledge/search", body, token="x" * 32)[0], 401)

    def test_falha_do_grafo_deixa_rastro_sem_a_mensagem_da_excecao(self) -> None:
        captured = io.StringIO()
        with mock.patch.object(server, "run_cancel", side_effect=RuntimeError("C:/segredo/caminho")):
            with contextlib.redirect_stderr(captured):
                status, body = self.call("/v1/workflows/run-1/cancel", {"runId": "run-1"})
        self.assertEqual(status, 500)
        self.assertEqual(body, {"error": "workflow_failed"})
        log = captured.getvalue()
        self.assertIn("RuntimeError", log)
        self.assertNotIn("segredo", log)


if __name__ == "__main__":
    unittest.main()
