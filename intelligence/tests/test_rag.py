import tempfile
import unittest

import _paths  # noqa: F401

from knowledge import index as rag

PROJECT_A = "p-aaaaaaaaaaaa"
PROJECT_B = "p-bbbbbbbbbbbb"
TOKEN = "alfa-mariliz-91"


class RagTests(unittest.TestCase):
    def test_atualiza_so_o_arquivo_mudado_e_remove_orfaos(self) -> None:
        with tempfile.TemporaryDirectory() as root:
            first = rag.sync(root, PROJECT_A, [{"path": "docs/a.md", "hash": "a" * 64, "text": "alfa mariliz exclusivo neste arquivo longo"}])
            self.assertEqual(first["updated"], 1)
            second = rag.sync(root, PROJECT_A, [{"path": "docs/a.md", "hash": "a" * 64}])
            self.assertEqual(second["updated"], 0)
            self.assertEqual(second["unchanged"], 1)
            changed = rag.sync(root, PROJECT_A, [{"path": "docs/b.md", "hash": "b" * 64, "text": "estoque e catalogo sem o termo exclusivo longo"}])
            self.assertEqual(changed["removed"], 1)
            self.assertEqual(changed["updated"], 1)
            self.assertEqual(rag.search(root, PROJECT_A, "alfa mariliz"), [])
            self.assertEqual(rag.search(root, PROJECT_A, "estoque catalogo")[0]["filePath"], "docs/b.md")

    def test_dois_projetos_nao_compartilham_trechos(self) -> None:
        with tempfile.TemporaryDirectory() as root:
            rag.sync(root, PROJECT_A, [{"path": "docs/nota.md", "hash": "a" * 64, "text": f"O identificador {TOKEN} fica somente neste projeto."}])
            rag.sync(root, PROJECT_B, [{"path": "docs/nota.md", "hash": "b" * 64, "text": "Este projeto fala de estoque e de catalogo geral."}])
            leaked = rag.search(root, PROJECT_B, TOKEN)
            self.assertEqual(leaked, [])
            found = rag.search(root, PROJECT_A, TOKEN)
            self.assertEqual(found[0]["filePath"], "docs/nota.md")
            self.assertIn(TOKEN, found[0]["excerpt"])
            self.assertNotEqual(found[0]["hash"], "")

    def test_recusa_caminho_de_segredo_e_projeto_invalido(self) -> None:
        with tempfile.TemporaryDirectory() as root:
            stats = rag.sync(root, PROJECT_A, [
                {"path": "../.env", "hash": "c" * 64, "text": "API_KEY=abcd1234efgh5678 nao pode entrar"},
                {"path": ".env", "hash": "d" * 64, "text": "API_KEY=abcd1234efgh5678 nao pode entrar"},
                {"path": "docs/ok.md", "hash": "e" * 64, "text": "Texto seguro e longo o bastante para indexar."},
            ])
            self.assertEqual(stats["documents"], 1)
            with self.assertRaises(rag.KnowledgeError):
                rag.sync(root, "../" + PROJECT_A, [])
            with self.assertRaises(rag.KnowledgeError):
                rag.search("/tmp/nao-importa", "p-1", "texto")
