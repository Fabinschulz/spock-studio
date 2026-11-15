import os
import tempfile
import unittest
from pathlib import Path

import _paths  # noqa: F401

from contracts.messages import SEARCH_SCHEMA, ContractError, parse_search
from knowledge import index
from knowledge.lookup import SEARCH_ACK, lookup

PROJECT = "p-aaaaaaaaaaaa"


def _payload(root: str, query: str = "calculo de frete", limit: int = 5) -> dict:
    return {"schema": SEARCH_SCHEMA, "projectId": PROJECT, "query": query, "knowledgeRoot": root, "limit": limit}


class LookupTests(unittest.TestCase):
    def test_traz_o_trecho_com_caminho_linha_e_hash_do_que_foi_indexado(self) -> None:
        with tempfile.TemporaryDirectory() as root:
            index.sync(root, PROJECT, [
                {"path": "src/Frete.cs", "hash": "a" * 64, "text": "public class Frete\n{\n    // calculo de frete do carrinho\n}\n"},
                {"path": "docs/outro.md", "hash": "b" * 64, "text": "# Outro\nnada a ver com o pedido\n"},
            ])
            result = lookup(parse_search(_payload(root)))

        self.assertEqual(result["schema"], SEARCH_ACK)
        self.assertTrue(result["indexed"])
        self.assertEqual(result["hits"][0]["filePath"], "src/Frete.cs")
        self.assertEqual(result["hits"][0]["hash"], "a" * 64)
        self.assertIn("calculo de frete", result["hits"][0]["body"])
        self.assertNotIn("docs/outro.md", [hit["filePath"] for hit in result["hits"]])

    def test_sem_indice_responde_vazio_e_nao_cria_nada_no_disco(self) -> None:
        with tempfile.TemporaryDirectory() as root:
            result = lookup(parse_search(_payload(root)))
            created = [str(path) for path in Path(root).rglob("*")]

        self.assertEqual(result, {"schema": SEARCH_ACK, "indexed": False, "hits": []})
        self.assertEqual(created, [])

    def test_recusa_pedido_fora_do_contrato(self) -> None:
        good = _payload(os.getcwd())
        for broken in (
            {**good, "schema": "x"},
            {**good, "projectId": "../p"},
            {**good, "query": "   "},
            {**good, "query": "x" * 301},
            {**good, "limit": 0},
            {**good, "limit": 11},
            {**good, "limit": True},
            {**good, "knowledgeRoot": ""},
        ):
            with self.assertRaises(ContractError):
                parse_search(broken)


if __name__ == "__main__":
    unittest.main()
